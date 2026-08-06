#!/usr/bin/env python3
"""Compare candidate package APIs with an immutable stable-package baseline."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import tempfile
import urllib.request
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any

try:
    from .generate_api_reference import run_inspectors
except ImportError:  # Direct script execution places this directory on sys.path.
    from generate_api_reference import run_inspectors

ALLOWED_CATEGORIES = {
    "documented-contract",
    "undocumented-public",
    "additive",
    "generated-or-noncontract",
}

STATE_MACHINE_ATTRIBUTE_RE = re.compile(
    r"(?P<prefix>System\.Runtime\.CompilerServices\."
    r"(?:AsyncStateMachine|IteratorStateMachine|AsyncIteratorStateMachine)"
    r"Attribute\(typeof\()(?P<type>[^)]+)(?P<suffix>\)\))"
)
STATE_MACHINE_ORDINAL_RE = re.compile(r"(?<=d__)\d+(?=(?:`\d+)?$)")


def normalize_compiler_generated_state_machine_types(signature: str) -> str:
    """Ignore unstable compiler state-machine ordinals without hiding contract changes."""

    def normalize_attribute(match: re.Match[str]) -> str:
        state_machine_type = STATE_MACHINE_ORDINAL_RE.sub("*", match.group("type"))
        return f"{match.group('prefix')}{state_machine_type}{match.group('suffix')}"

    return STATE_MACHINE_ATTRIBUTE_RE.sub(normalize_attribute, signature)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-root", type=Path, required=True)
    parser.add_argument("--policy", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument(
        "--enforce-candidate-major",
        action="store_true",
        help="Require the repository version to satisfy every documented-contract major disposition.",
    )
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def validate_sha256(path: Path, expected: str) -> None:
    actual = sha256(path)
    normalized_expected = expected.upper()
    if actual != normalized_expected:
        raise ValueError(f"baseline digest mismatch: expected {normalized_expected}, got {actual}")


def flatten_api(api: list[dict[str, Any]]) -> dict[str, str]:
    flattened: dict[str, str] = {}
    for type_entry in api:
        type_id = str(type_entry["DocId"])
        if type_id in flattened:
            raise ValueError(f"duplicate public API identifier: {type_id}")
        flattened[type_id] = normalize_compiler_generated_state_machine_types(
            str(type_entry.get("ContractSignature", type_entry["Signature"]))
        )
        for member in type_entry["Members"]:
            member_id = str(member["DocId"])
            if member_id in flattened:
                raise ValueError(f"duplicate public API identifier: {member_id}")
            flattened[member_id] = normalize_compiler_generated_state_machine_types(
                str(member.get("ContractSignature", member["Signature"]))
            )
    return flattened


def api_changes(baseline: dict[str, str], candidate: dict[str, str], tfm: str) -> list[dict[str, Any]]:
    changes: list[dict[str, Any]] = []
    for api_id in sorted(baseline.keys() - candidate.keys()):
        changes.append(
            {
                "key": f"{tfm}|removed|{api_id}",
                "tfm": tfm,
                "change": "removed",
                "api": api_id,
                "before": baseline[api_id],
                "after": None,
            }
        )
    for api_id in sorted(candidate.keys() - baseline.keys()):
        changes.append(
            {
                "key": f"{tfm}|added|{api_id}",
                "tfm": tfm,
                "change": "added",
                "api": api_id,
                "before": None,
                "after": candidate[api_id],
            }
        )
    for api_id in sorted(baseline.keys() & candidate.keys()):
        if baseline[api_id] != candidate[api_id]:
            changes.append(
                {
                    "key": f"{tfm}|changed|{api_id}",
                    "tfm": tfm,
                    "change": "changed",
                    "api": api_id,
                    "before": baseline[api_id],
                    "after": candidate[api_id],
                }
            )
    return changes


def validate_classification(entry: dict[str, Any], change: dict[str, Any], repository_root: Path) -> None:
    category = entry.get("category")
    if category not in ALLOWED_CATEGORIES:
        raise ValueError(f"{change['key']}: invalid category {category!r}")
    rationale = entry.get("rationale")
    if not isinstance(rationale, str) or not rationale.strip():
        raise ValueError(f"{change['key']}: classification requires rationale")
    evidence = entry.get("evidence")
    if not isinstance(evidence, list) or not evidence:
        raise ValueError(f"{change['key']}: classification requires evidence paths")
    for value in evidence:
        path = repository_root / str(value)
        if not path.is_file():
            raise ValueError(f"{change['key']}: evidence path does not exist: {value}")
    for field in ("before", "after"):
        if field not in entry:
            raise ValueError(f"{change['key']}: classification {field} signature does not match the detected change")
        classified_signature = entry[field]
        detected_signature = change.get(field)
        if isinstance(classified_signature, str):
            classified_signature = normalize_compiler_generated_state_machine_types(classified_signature)
        if isinstance(detected_signature, str):
            detected_signature = normalize_compiler_generated_state_machine_types(detected_signature)
        if classified_signature != detected_signature:
            raise ValueError(f"{change['key']}: classification {field} signature does not match the detected change")
    if category == "additive" and change["change"] != "added":
        raise ValueError(f"{change['key']}: additive is valid only for added API")
    if category == "documented-contract":
        for field in ("decision", "migration", "changelog"):
            if not isinstance(entry.get(field), str) or not entry[field].strip():
                raise ValueError(f"{change['key']}: documented-contract requires {field}")
        disposition = entry.get("version_disposition")
        if not isinstance(disposition, dict):
            raise ValueError(f"{change['key']}: documented-contract requires a version_disposition object")
        if disposition.get("kind") != "major" or not isinstance(disposition.get("target_major"), int):
            raise ValueError(f"{change['key']}: documented-contract requires a numeric major-version disposition")
    if category == "undocumented-public":
        terms = entry.get("search_terms")
        if not isinstance(terms, list) or not terms or not all(isinstance(term, str) and term for term in terms):
            raise ValueError(f"{change['key']}: undocumented-public requires non-empty baseline search_terms")


def load_baseline_documentation(policy: dict[str, Any], repository_root: Path) -> dict[str, str]:
    baseline_docs = policy.get("baseline_documentation")
    if not isinstance(baseline_docs, dict):
        raise ValueError("baseline_documentation must be an object")
    commit = baseline_docs.get("commit")
    paths = baseline_docs.get("paths")
    if not isinstance(commit, str) or len(commit) != 40:
        raise ValueError("baseline_documentation.commit must be a full 40-character commit ID")
    if not isinstance(paths, list) or not paths or not all(isinstance(path, str) and path for path in paths):
        raise ValueError("baseline_documentation.paths must be a non-empty string array")
    command = ["git", "ls-tree", "-r", "--name-only", commit, "--", *paths]
    listed = subprocess.run(command, cwd=repository_root, check=True, capture_output=True, text=True)
    file_paths = [line.strip() for line in listed.stdout.splitlines() if line.strip()]
    if not file_paths:
        raise ValueError("baseline documentation selection is empty")
    documents: dict[str, str] = {}
    for path in file_paths:
        result = subprocess.run(
            ["git", "show", f"{commit}:{path}"],
            cwd=repository_root,
            check=True,
            capture_output=True,
        )
        documents[path] = result.stdout.decode("utf-8-sig", errors="replace")
    return documents


def api_symbol(api_id: str) -> str:
    body = api_id.split(":", 1)[-1].split("(", 1)[0]
    symbol = body.rsplit(".", 1)[-1]
    if symbol == "#ctor":
        parent = body.rsplit(".", 1)[0]
        return parent.rsplit(".", 1)[-1].split("`", 1)[0]
    return symbol.split("`", 1)[0]


def validate_baseline_contract_search(
    entry: dict[str, Any], change: dict[str, Any], baseline_documents: dict[str, str]
) -> None:
    category = entry.get("category")
    if category not in {"documented-contract", "undocumented-public"}:
        return
    configured_terms = [str(term) for term in entry.get("search_terms", [])]
    required_symbol = api_symbol(str(change["api"]))
    terms = list(dict.fromkeys([required_symbol, *configured_terms]))
    matches = [
        f"{path}:{term}"
        for path, content in baseline_documents.items()
        for term in terms
        if term in content
    ]
    if category == "undocumented-public" and matches:
        raise ValueError(
            f"{change['key']}: undocumented-public conflicts with stable user documentation/example matches: {matches}"
        )
    if category == "documented-contract" and not matches:
        raise ValueError(
            f"{change['key']}: documented-contract has no matching stable user documentation/example evidence"
        )


def validate_version_dispositions(
    policy: dict[str, Any], classifications: list[dict[str, Any]], repository_root: Path, enforce_candidate: bool
) -> None:
    baseline_major = int(str(policy["baseline"]["version"]).split(".", 1)[0])
    required_majors: list[int] = []
    for entry in classifications:
        if entry.get("category") != "documented-contract":
            continue
        disposition = entry["version_disposition"]
        target_major = int(disposition["target_major"])
        if target_major <= baseline_major:
            raise ValueError(f"{entry.get('key')}: target major must be greater than baseline major {baseline_major}")
        required_majors.append(target_major)
    if not enforce_candidate or not required_majors:
        return
    props = ET.parse(repository_root / "Directory.Build.props").getroot()
    version_node = props.find(".//Version")
    if version_node is None or not version_node.text:
        raise ValueError("Directory.Build.props does not define Version")
    candidate_major = int(version_node.text.split(".", 1)[0])
    required_major = max(required_majors)
    if candidate_major < required_major:
        raise ValueError(f"candidate major {candidate_major} does not satisfy required major {required_major}")


def validate_classification_set(
    changes: list[dict[str, Any]],
    classifications: Any,
    repository_root: Path,
    baseline_documents: dict[str, str],
) -> None:
    if not isinstance(classifications, list):
        raise ValueError("classifications must be an array")
    by_key = {str(entry.get("key")): entry for entry in classifications}
    if len(classifications) != len(by_key):
        raise ValueError("classification keys must be unique")
    change_keys = {change["key"] for change in changes}
    stale = sorted(set(by_key) - change_keys)
    if stale:
        raise ValueError(f"stale classifications without a matching API change: {stale}")
    unclassified = [change for change in changes if change["key"] not in by_key]
    if unclassified:
        print(json.dumps(unclassified, indent=2, ensure_ascii=False))
        raise ValueError(f"{len(unclassified)} API changes require explicit classification")
    for change in changes:
        validate_classification(by_key[change["key"]], change, repository_root)
        validate_baseline_contract_search(by_key[change["key"]], change, baseline_documents)


def main() -> int:
    args = parse_args()
    policy = json.loads(args.policy.read_text(encoding="utf-8"))
    baseline = policy["baseline"]
    tfms = [str(value) for value in baseline["tfms"]]
    with tempfile.TemporaryDirectory(prefix="plc_api_diff_") as temp_dir:
        temp = Path(temp_dir)
        package_path = temp / "baseline.nupkg"
        urllib.request.urlretrieve(str(baseline["url"]), package_path)
        validate_sha256(package_path, str(baseline["sha256"]))
        with zipfile.ZipFile(package_path) as archive:
            archive.extractall(temp / "baseline")

        assembly_pairs: list[tuple[str, Path, Path]] = []
        for tfm in tfms:
            baseline_assembly = temp / "baseline" / "lib" / tfm / str(baseline["assembly"])
            candidate_assembly = args.candidate_root / tfm / str(baseline["assembly"])
            if not baseline_assembly.is_file() or not candidate_assembly.is_file():
                raise FileNotFoundError(f"missing {tfm} baseline or candidate assembly")
            assembly_pairs.append((tfm, baseline_assembly, candidate_assembly))
        assembly_paths = [
            path
            for _, baseline_path, candidate_path in assembly_pairs
            for path in (baseline_path, candidate_path)
        ]
        inspected = run_inspectors(assembly_paths, "net10.0", include_editor_hidden=True)
        changes: list[dict[str, Any]] = []
        for index, (tfm, _, _) in enumerate(assembly_pairs):
            changes.extend(
                api_changes(flatten_api(inspected[index * 2]), flatten_api(inspected[index * 2 + 1]), tfm)
            )

    baseline_documents = load_baseline_documentation(policy, args.repository_root)
    classifications = policy.get("classifications")
    validate_classification_set(
        changes,
        classifications,
        args.repository_root,
        baseline_documents,
    )
    validate_version_dispositions(
        policy,
        classifications,
        args.repository_root,
        args.enforce_candidate_major,
    )
    print(f"Documented API diff passed: {len(changes)} classified change(s) across {len(tfms)} TFM(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
