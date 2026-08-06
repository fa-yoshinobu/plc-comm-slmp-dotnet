#!/usr/bin/env python3
"""Focused regression tests for API-reference XML documentation rendering."""

from __future__ import annotations

import unittest
import xml.etree.ElementTree as ET
import subprocess
import tempfile
from pathlib import Path

from generate_api_reference import cref_label, node_text, run_inspector


class CrefLabelTests(unittest.TestCase):
    def test_method_parameter_list_is_not_rendered_as_the_label(self) -> None:
        cref = "M:PlcComm.Slmp.SlmpClient.ReadDevicesAsync(System.String,System.Threading.CancellationToken)"
        self.assertEqual(cref_label(cref), "ReadDevicesAsync")

    def test_generic_arity_is_removed_from_type_label(self) -> None:
        self.assertEqual(cref_label("T:PlcComm.Slmp.Result`1"), "Result")

    def test_rendered_see_label_does_not_end_in_a_parenthesis(self) -> None:
        node = ET.fromstring(
            '<summary>Use <see cref="M:PlcComm.Slmp.Parser.Parse(PlcComm.Slmp.SlmpDeviceAddress)"/>.</summary>'
        )
        rendered = node_text(node)
        self.assertEqual(rendered, "Use `Parse`.")
        self.assertNotRegex(rendered, r"`[^`]*\)`")

    def test_generator_honors_editor_browsable_never(self) -> None:
        source = __import__("generate_api_reference").CSHARP_INSPECTOR
        self.assertIn("EditorBrowsableState.Never", source)
        self.assertIn("IsDocumented(m)", source)

    def test_generator_distinguishes_init_only_from_mutable_properties(self) -> None:
        repo_root = Path(__file__).resolve().parents[1]
        scratch_root = repo_root / "local_folder"
        scratch_root.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="api-property-fixture-", dir=scratch_root
        ) as temp_dir:
            fixture = Path(temp_dir)
            (fixture / "PropertyFixture.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                "<TargetFramework>net8.0</TargetFramework>"
                "</PropertyGroup></Project>",
                encoding="utf-8",
            )
            (fixture / "PropertyFixture.cs").write_text(
                "namespace GeneratorFixture;\n"
                "public sealed class PropertyFixture\n"
                "{\n"
                "    public int InitOnly { get; init; }\n"
                "    public int Mutable { get; set; }\n"
                "}\n",
                encoding="utf-8",
            )
            subprocess.run(
                [
                    "dotnet",
                    "build",
                    fixture / "PropertyFixture.csproj",
                    "-c",
                    "Release",
                    "--nologo",
                ],
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
            )
            api = run_inspector(
                fixture / "bin" / "Release" / "net8.0" / "PropertyFixture.dll"
            )

        members = {
            member["Name"]: member["Signature"]
            for api_type in api
            if api_type["Name"] == "PropertyFixture"
            for member in api_type["Members"]
        }
        self.assertEqual(members["InitOnly"], "public int InitOnly { get; init; }")
        self.assertEqual(members["Mutable"], "public int Mutable { get; set; }")

    def test_member_order_is_semantic_and_signature_changes_remain_visible(self) -> None:
        repo_root = Path(__file__).resolve().parents[1]
        scratch_root = repo_root / "local_folder"
        scratch_root.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="api-order-fixture-", dir=scratch_root) as temp_dir:
            fixture = Path(temp_dir)
            project = fixture / "OrderFixture.csproj"
            source = fixture / "OrderFixture.cs"
            project.write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                "<TargetFramework>net8.0</TargetFramework>"
                "</PropertyGroup></Project>",
                encoding="utf-8",
            )

            def inspect(source_text: str) -> list[tuple[str, str]]:
                source.write_text(source_text, encoding="utf-8")
                subprocess.run(
                    ["dotnet", "build", project, "-c", "Release", "-t:Rebuild", "--nologo"],
                    check=True,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                )
                api = run_inspector(fixture / "bin" / "Release" / "net8.0" / "OrderFixture.dll")
                return [
                    (member["DocId"], member["ContractSignature"])
                    for api_type in api
                    if api_type["Name"] == "OrderFixture"
                    for member in api_type["Members"]
                ]

            first = inspect(
                "namespace GeneratorFixture;\n"
                "public sealed class OrderFixture\n"
                "{\n"
                "    public static void Alpha(int value) { }\n"
                "    public static void Beta(string value) { }\n"
                "}\n"
            )
            declaration_order_only = inspect(
                "namespace GeneratorFixture;\n"
                "public sealed class OrderFixture\n"
                "{\n"
                "    public static void Beta(string value) { }\n"
                "    public static void Alpha(int value) { }\n"
                "}\n"
            )
            meaningful_change = inspect(
                "namespace GeneratorFixture;\n"
                "public sealed class OrderFixture\n"
                "{\n"
                "    public static void Beta(string value) { }\n"
                "    public static void Alpha(long value) { }\n"
                "}\n"
            )

        self.assertEqual(first, declaration_order_only)
        self.assertNotEqual(declaration_order_only, meaningful_change)
        self.assertTrue(any("System.Int64" in doc_id for doc_id, _ in meaningful_change))

    def test_contract_mode_tracks_surface_not_rendered_by_user_reference(self) -> None:
        source = __import__("generate_api_reference").CSHARP_INSPECTOR
        for required in (
            "ContractSignature",
            "GetRawConstantValue",
            "Enum.GetUnderlyingType",
            "GetRequiredCustomModifiers",
            "GetGenericParameterConstraints",
            "BindingFlags.NonPublic",
            "IsNestedFamily",
            'StartsWith("op_"',
            "GetIndexParameters",
            "CustomAttributes",
        ):
            self.assertIn(required, source)
        self.assertIn("OrderBy(m => MemberDocId(m), StringComparer.Ordinal)", source)
        self.assertNotIn("m.MetadataToken", source)


if __name__ == "__main__":
    unittest.main()
