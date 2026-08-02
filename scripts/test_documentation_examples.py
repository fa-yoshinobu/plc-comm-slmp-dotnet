#!/usr/bin/env python3
"""Regression tests for executable commands and contracts in user documentation."""

from __future__ import annotations

import re
import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
USAGE = (ROOT / "docsrc/user/USAGE_GUIDE.md").read_text(encoding="utf-8")
OPTIONS_SOURCE = (ROOT / "src/PlcComm.Slmp/SlmpConnectionOptions.cs").read_text(
    encoding="utf-8"
)
API_REFERENCE = (ROOT / "docsrc/user/API_REFERENCE.md").read_text(encoding="utf-8")


class DocumentationExamplesTests(unittest.TestCase):
    def test_multiplc_command_supplies_target_and_passes_dry_run(self) -> None:
        command = next(
            line
            for line in USAGE.splitlines()
            if line.startswith("dotnet run")
            and "PlcComm.Slmp.MultiPlcMonitorSample" in line
        )
        plc_specs = re.findall(r"--plc\s+(\S+)", command)
        self.assertEqual(len(plc_specs), 2)
        for spec in plc_specs:
            fields = spec.split("=", 1)[1].split(",")
            self.assertEqual(len(fields), 5)
            self.assertEqual(fields[-1], "SELF")

        args = command.split()
        args.insert(2, "--no-build")
        args.append("--dry-run")
        result = subprocess.run(
            args,
            cwd=ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_recommended_entry_table_is_contiguous(self) -> None:
        section = USAGE.split("## Connection", 1)[0]
        lines = section.splitlines()
        table_indexes = [
            index for index, line in enumerate(lines) if line.startswith("|")
        ]
        self.assertGreaterEqual(len(table_indexes), 3)
        self.assertEqual(
            table_indexes, list(range(table_indexes[0], table_indexes[-1] + 1))
        )

    def test_timeout_lifecycle_matches_source_and_generated_reference(self) -> None:
        expected = (
            "deadline used to open the transport and complete each admitted request"
        )
        self.assertIn(expected, OPTIONS_SOURCE)
        self.assertIn(expected, API_REFERENCE)
        self.assertNotIn("after the session is opened", OPTIONS_SOURCE)
        self.assertNotIn("after the session is opened", API_REFERENCE)

    def test_state_changing_examples_require_explicit_opt_in(self) -> None:
        active_lines = {
            line.strip()
            for line in USAGE.splitlines()
            if not line.lstrip().startswith("//")
        }
        self.assertNotIn(
            "await client.WriteWordsExtendedAsync(module, new ushort[] { 1, 2, 3, 4 });",
            active_lines,
        )
        self.assertNotIn("await client.ClearErrorAsync();", active_lines)
        self.assertIn("outcome-unknown failure", USAGE)
        self.assertIn("Clear Error changes PLC state", USAGE)


if __name__ == "__main__":
    unittest.main()
