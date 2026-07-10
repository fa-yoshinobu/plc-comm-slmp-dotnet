#!/usr/bin/env python3
"""Focused regression tests for API-reference XML documentation rendering."""

from __future__ import annotations

import unittest
import xml.etree.ElementTree as ET

from generate_api_reference import cref_label, node_text


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


if __name__ == "__main__":
    unittest.main()
