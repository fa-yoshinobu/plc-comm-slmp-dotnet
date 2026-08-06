"""Unit tests for documented API diff classification policy."""

from __future__ import annotations

import hashlib
import tempfile
import unittest
from pathlib import Path

from scripts.check_documented_api_diff import (
    api_changes,
    flatten_api,
    normalize_compiler_generated_state_machine_types,
    validate_baseline_contract_search,
    validate_classification,
    validate_classification_set,
    validate_sha256,
    validate_version_dispositions,
)


class ApiDiffPolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "evidence.md").write_text("decision evidence", encoding="utf-8")

    def tearDown(self) -> None:
        self.temp.cleanup()

    @staticmethod
    def change(kind: str = "added") -> dict[str, object]:
        before = "old signature" if kind != "added" else None
        after = "new signature" if kind != "removed" else None
        return {
            "key": f"net10.0|{kind}|T:Example.Widget",
            "tfm": "net10.0",
            "change": kind,
            "api": "T:Example.Widget",
            "before": before,
            "after": after,
        }

    def entry(self, category: str, change: dict[str, object]) -> dict[str, object]:
        return {
            "key": change["key"],
            "category": category,
            "rationale": "reviewed classification",
            "evidence": ["evidence.md"],
            "before": change["before"],
            "after": change["after"],
        }

    def test_changes_report_added_removed_and_changed_surface(self) -> None:
        changes = api_changes(
            {"T:Old": "public class Old", "M:Same": "public void Same(int value)"},
            {"T:New": "public class New", "M:Same": "public void Same(string value)"},
            "net10.0",
        )
        self.assertEqual({change["change"] for change in changes}, {"added", "removed", "changed"})
        self.assertTrue(all("before" in change and "after" in change for change in changes))

    def test_state_machine_ordinals_do_not_create_api_changes(self) -> None:
        baseline = {
            "M:Example.Widget.RunAsync": (
                "method|attrs=[System.Runtime.CompilerServices.AsyncStateMachineAttribute("
                "typeof(Example.Widget+<RunAsync>d__12))]"
            ),
            "M:Example.Widget.StreamAsync": (
                "method|attrs=[System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute("
                "typeof(Example.Widget+<StreamAsync>d__13))]"
            ),
            "M:Example.Widget.Items": (
                "method|attrs=[System.Runtime.CompilerServices.IteratorStateMachineAttribute("
                "typeof(Example.Widget+<Items>d__14`1))]"
            ),
        }
        candidate = {
            "M:Example.Widget.RunAsync": baseline["M:Example.Widget.RunAsync"].replace("d__12", "d__159"),
            "M:Example.Widget.StreamAsync": baseline["M:Example.Widget.StreamAsync"].replace(
                "d__13", "d__160"
            ),
            "M:Example.Widget.Items": baseline["M:Example.Widget.Items"].replace("d__14", "d__161"),
        }

        normalize = normalize_compiler_generated_state_machine_types
        normalized_baseline = {key: normalize(value) for key, value in baseline.items()}
        normalized_candidate = {key: normalize(value) for key, value in candidate.items()}
        self.assertEqual(api_changes(normalized_baseline, normalized_candidate, "net10.0"), [])

    def test_state_machine_normalization_preserves_meaningful_attribute_changes(self) -> None:
        normalize = normalize_compiler_generated_state_machine_types
        async_signature = (
            "attrs=[System.Runtime.CompilerServices.AsyncStateMachineAttribute("
            "typeof(Example.Widget+<RunAsync>d__12))]"
        )
        iterator_signature = async_signature.replace("AsyncStateMachine", "IteratorStateMachine")
        other_method_signature = async_signature.replace("<RunAsync>", "<OtherAsync>")
        user_attribute_signature = "attrs=[Example.StateAttribute(typeof(Example.Widget+<RunAsync>d__12))]"

        self.assertNotEqual(normalize(async_signature), normalize(iterator_signature))
        self.assertNotEqual(normalize(async_signature), normalize(other_method_signature))
        self.assertEqual(normalize(user_attribute_signature), user_attribute_signature)

    def test_flatten_api_normalizes_state_machine_ordinals(self) -> None:
        api = [
            {
                "DocId": "T:Example.Widget",
                "ContractSignature": "type",
                "Signature": "public class Widget",
                "Members": [
                    {
                        "DocId": "M:Example.Widget.RunAsync",
                        "ContractSignature": (
                            "method|attrs=[System.Runtime.CompilerServices.AsyncStateMachineAttribute("
                            "typeof(Example.Widget+<RunAsync>d__321))]"
                        ),
                        "Signature": "public Task RunAsync()",
                    }
                ],
            }
        ]

        flattened = flatten_api(api)
        self.assertIn("<RunAsync>d__*", flattened["M:Example.Widget.RunAsync"])

    def test_all_four_categories_accept_valid_entries(self) -> None:
        additive_change = self.change("added")
        additive = self.entry("additive", additive_change)
        validate_classification(additive, additive_change, self.root)

        generated_change = self.change("removed")
        generated = self.entry("generated-or-noncontract", generated_change)
        validate_classification(generated, generated_change, self.root)

        documented_change = self.change("changed")
        documented = self.entry("documented-contract", documented_change)
        documented.update(
            decision="GOAL-1",
            migration="migration.md",
            changelog="CHANGELOG.md",
            version_disposition={"kind": "major", "target_major": 5},
        )
        validate_classification(documented, documented_change, self.root)
        validate_baseline_contract_search(
            documented,
            documented_change,
            {"docsrc/user/API_REFERENCE.md": "Example.Widget"},
        )

        undocumented_change = self.change("removed")
        undocumented = self.entry("undocumented-public", undocumented_change)
        undocumented["search_terms"] = ["Example.Widget"]
        validate_classification(undocumented, undocumented_change, self.root)
        validate_baseline_contract_search(undocumented, undocumented_change, {"README.md": "other API"})

    def test_additive_cannot_classify_a_removal(self) -> None:
        change = self.change("removed")
        with self.assertRaisesRegex(ValueError, "additive"):
            validate_classification(self.entry("additive", change), change, self.root)

    def test_documented_contract_requires_decision_migration_changelog_and_version(self) -> None:
        change = self.change("removed")
        with self.assertRaisesRegex(ValueError, "decision"):
            validate_classification(self.entry("documented-contract", change), change, self.root)

    def test_signature_drift_rejects_a_previously_valid_classification(self) -> None:
        change = self.change("changed")
        entry = self.entry("generated-or-noncontract", change)
        entry["after"] = "later unreviewed signature"
        with self.assertRaisesRegex(ValueError, "after signature"):
            validate_classification(entry, change, self.root)

    def test_classification_signature_accepts_only_state_machine_ordinal_drift(self) -> None:
        change = self.change("changed")
        change["before"] = (
            "old|attrs=[System.Runtime.CompilerServices.AsyncStateMachineAttribute("
            "typeof(Example.Widget+<RunAsync>d__*))]"
        )
        change["after"] = (
            "new|attrs=[System.Runtime.CompilerServices.AsyncStateMachineAttribute("
            "typeof(Example.Widget+<RunAsync>d__*))]"
        )
        entry = self.entry("generated-or-noncontract", change)
        entry["before"] = str(change["before"]).replace("d__*", "d__12")
        entry["after"] = str(change["after"]).replace("d__*", "d__159")
        validate_classification(entry, change, self.root)

        entry["after"] = str(entry["after"]).replace("<RunAsync>", "<OtherAsync>")
        with self.assertRaisesRegex(ValueError, "after signature"):
            validate_classification(entry, change, self.root)

    def test_undocumented_public_rejects_prior_stable_documentation_match(self) -> None:
        change = self.change("removed")
        entry = self.entry("undocumented-public", change)
        entry["search_terms"] = ["Widget"]
        with self.assertRaisesRegex(ValueError, "stable user documentation"):
            validate_baseline_contract_search(
                entry,
                change,
                {"docsrc/user/API_REFERENCE.md": "Use Example.Widget here."},
            )

    def test_documented_contract_rejects_missing_prior_stable_evidence(self) -> None:
        change = self.change("removed")
        entry = self.entry("documented-contract", change)
        entry.update(
            decision="GOAL-1",
            migration="migration.md",
            changelog="CHANGELOG.md",
            version_disposition={"kind": "major", "target_major": 5},
        )
        with self.assertRaisesRegex(ValueError, "no matching stable"):
            validate_baseline_contract_search(entry, change, {"README.md": "other API"})

    def test_classification_set_rejects_unclassified_stale_and_duplicate_entries(self) -> None:
        change = self.change("added")
        with self.assertRaisesRegex(ValueError, "require explicit classification"):
            validate_classification_set([change], [], self.root, {})

        entry = self.entry("additive", change)
        stale = dict(entry, key="net10.0|added|T:Example.Stale")
        with self.assertRaisesRegex(ValueError, "stale"):
            validate_classification_set([change], [stale], self.root, {})

        with self.assertRaisesRegex(ValueError, "unique"):
            validate_classification_set([change], [entry, dict(entry)], self.root, {})

    def test_flatten_api_rejects_duplicate_doc_ids(self) -> None:
        api = [
            {
                "DocId": "T:Example.Widget",
                "Signature": "type",
                "Members": [
                    {"DocId": "M:Example.Widget.Run", "Signature": "first"},
                    {"DocId": "M:Example.Widget.Run", "Signature": "second"},
                ],
            }
        ]
        with self.assertRaisesRegex(ValueError, "duplicate public API identifier"):
            flatten_api(api)

    def test_baseline_digest_mismatch_is_rejected(self) -> None:
        package = self.root / "baseline.nupkg"
        package.write_bytes(b"immutable package")
        expected = hashlib.sha256(b"different package").hexdigest()
        with self.assertRaisesRegex(ValueError, "baseline digest mismatch"):
            validate_sha256(package, expected)

    def test_release_version_policy_requires_declared_and_actual_major(self) -> None:
        policy = {"baseline": {"version": "4.0.1"}}
        classification = {
            "key": "net10.0|removed|T:Example.Widget",
            "category": "documented-contract",
            "version_disposition": {"kind": "major", "target_major": 5},
        }
        (self.root / "Directory.Build.props").write_text(
            "<Project><PropertyGroup><Version>4.1.0</Version></PropertyGroup></Project>",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(ValueError, "does not satisfy"):
            validate_version_dispositions(policy, [classification], self.root, True)
        validate_version_dispositions(policy, [classification], self.root, False)


if __name__ == "__main__":
    unittest.main()
