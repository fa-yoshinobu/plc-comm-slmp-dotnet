#!/usr/bin/env python3
"""Generate a Markdown API reference from a .NET assembly and XML docs."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import tempfile
import textwrap
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path


CSHARP_INSPECTOR = r'''
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static string CleanName(string name) => name.Replace('+', '.');

static string DocTypeName(Type type)
{
    var fullName = type.FullName ?? type.Name;
    var tick = fullName.IndexOf('`');
    if (tick >= 0)
        fullName = fullName[..tick];
    return CleanName(fullName);
}

static string DocParamName(Type type)
{
    if (type.IsByRef)
        type = type.GetElementType()!;
    if (type.IsGenericParameter)
        return "`" + type.GenericParameterPosition;
    if (type.IsArray)
        return DocParamName(type.GetElementType()!) + "[]";
    if (type.IsGenericType)
    {
        var definition = type.GetGenericTypeDefinition();
        var name = definition.FullName ?? definition.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];
        return CleanName(name) + "{" + string.Join(",", type.GetGenericArguments().Select(DocParamName)) + "}";
    }
    return CleanName(type.FullName ?? type.Name);
}

static string MemberDocId(MemberInfo member)
{
    return member switch
    {
        Type type => "T:" + DocTypeName(type),
        ConstructorInfo ctor => "M:" + DocTypeName(ctor.DeclaringType!) + ".#ctor" + DocParams(ctor),
        MethodInfo method => "M:" + DocTypeName(method.DeclaringType!) + "." + method.Name + GenericSuffix(method) + DocParams(method),
        PropertyInfo prop => "P:" + DocTypeName(prop.DeclaringType!) + "." + prop.Name,
        FieldInfo field => "F:" + DocTypeName(field.DeclaringType!) + "." + field.Name,
        EventInfo ev => "E:" + DocTypeName(ev.DeclaringType!) + "." + ev.Name,
        _ => "",
    };
}

static string GenericSuffix(MethodInfo method)
{
    return method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length : "";
}

static string DocParams(MethodBase method)
{
    var parameters = method.GetParameters();
    if (parameters.Length == 0)
        return "";
    return "(" + string.Join(",", parameters.Select(p => DocParamName(p.ParameterType))) + ")";
}

static string TypeName(Type type)
{
    if (type.IsByRef)
        return TypeName(type.GetElementType()!) + "&";
    if (type == typeof(void)) return "void";
    if (type == typeof(bool)) return "bool";
    if (type == typeof(byte)) return "byte";
    if (type == typeof(sbyte)) return "sbyte";
    if (type == typeof(short)) return "short";
    if (type == typeof(ushort)) return "ushort";
    if (type == typeof(int)) return "int";
    if (type == typeof(uint)) return "uint";
    if (type == typeof(long)) return "long";
    if (type == typeof(ulong)) return "ulong";
    if (type == typeof(float)) return "float";
    if (type == typeof(double)) return "double";
    if (type == typeof(decimal)) return "decimal";
    if (type == typeof(char)) return "char";
    if (type == typeof(string)) return "string";
    if (type == typeof(object)) return "object";
    if (type.IsGenericParameter) return type.Name;
    if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
    if (type.IsGenericType)
    {
        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(Nullable<>))
            return TypeName(type.GetGenericArguments()[0]) + "?";
        var name = definition.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];
        return name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
    }
    return type.Name;
}

static string TypeKind(Type type)
{
    if (type.IsEnum) return "enum";
    if (type.IsInterface) return "interface";
    if (type.IsValueType) return "struct";
    return "class";
}

static string TypeSignature(Type type)
{
    var modifiers = new List<string> { "public" };
    if (type.IsAbstract && type.IsSealed)
        modifiers.Add("static");
    else if (type.IsAbstract && !type.IsInterface)
        modifiers.Add("abstract");
    else if (type.IsSealed && !type.IsValueType && !type.IsEnum)
        modifiers.Add("sealed");

    var name = type.Name;
    var tick = name.IndexOf('`');
    if (tick >= 0)
        name = name[..tick] + "<" + string.Join(", ", type.GetGenericArguments().Select(a => a.Name)) + ">";
    return string.Join(" ", modifiers.Append(TypeKind(type)).Append(name));
}

static string MethodModifiers(MethodInfo method)
{
    var parts = new List<string> { "public" };
    if (method.IsStatic)
        parts.Add("static");
    if (method.IsAbstract)
        parts.Add("abstract");
    else if (method.IsVirtual && !method.IsFinal)
        parts.Add("virtual");
    return string.Join(" ", parts);
}

static string FormatParameter(ParameterInfo parameter)
{
    var prefix = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : "";
    var parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
    var text = prefix + TypeName(parameterType) + " " + parameter.Name;
    if (parameter.HasDefaultValue)
    {
        var value = parameter.DefaultValue;
        var defaultText = value is null
            ? parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) is null ? "default" : "null"
            : value is string s ? "\"" + s + "\""
            : value is bool b ? (b ? "true" : "false")
            : value.ToString();
        text += " = " + defaultText;
    }
    return text;
}

static string MethodSignature(MethodInfo method)
{
    var name = method.Name;
    if (method.IsGenericMethodDefinition)
        name += "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">";
    return $"{MethodModifiers(method)} {TypeName(method.ReturnType)} {name}({string.Join(", ", method.GetParameters().Select(FormatParameter))})";
}

static string ConstructorSignature(ConstructorInfo ctor)
{
    return $"public {ctor.DeclaringType!.Name.Split('`')[0]}({string.Join(", ", ctor.GetParameters().Select(FormatParameter))})";
}

static string PropertySignature(PropertyInfo prop)
{
    var accessors = new List<string>();
    if (prop.GetMethod?.IsPublic == true) accessors.Add("get;");
    if (prop.SetMethod?.IsPublic == true) accessors.Add("set;");
    var staticText = ((prop.GetMethod ?? prop.SetMethod)?.IsStatic == true) ? " static" : "";
    return $"public{staticText} {TypeName(prop.PropertyType)} {prop.Name} {{ {string.Join(" ", accessors)} }}";
}

static string FieldSignature(FieldInfo field)
{
    var parts = new List<string> { "public" };
    if (field.IsLiteral) parts.Add("const");
    else if (field.IsStatic) parts.Add("static");
    if (field.IsInitOnly) parts.Add("readonly");
    parts.Add(TypeName(field.FieldType));
    parts.Add(field.Name);
    return string.Join(" ", parts);
}

static string EventSignature(EventInfo ev)
{
    return $"public event {TypeName(ev.EventHandlerType ?? typeof(object))} {ev.Name}";
}

static bool IsCompilerGenerated(MemberInfo member)
{
    return member.GetCustomAttribute<CompilerGeneratedAttribute>() is not null || member.Name.Contains('<');
}

static bool IsDeclaredPublic(MemberInfo member)
{
    return member switch
    {
        ConstructorInfo ctor => ctor.IsPublic,
        MethodInfo method => method.IsPublic && !method.IsSpecialName,
        PropertyInfo prop => prop.GetMethod?.IsPublic == true || prop.SetMethod?.IsPublic == true,
        FieldInfo field => field.IsPublic && !field.IsSpecialName,
        EventInfo ev => ev.AddMethod?.IsPublic == true,
        _ => false,
    };
}

var assemblyPath = args[0];
var assembly = Assembly.LoadFrom(assemblyPath);
var types = assembly.GetExportedTypes()
    .Where(t => !IsCompilerGenerated(t))
    .OrderBy(t => t.Namespace)
    .ThenBy(t => t.Name)
    .Select(type => new
    {
        Namespace = type.Namespace ?? "",
        Name = type.Name.Split('`')[0],
        FullName = CleanName(type.FullName ?? type.Name),
        Kind = TypeKind(type),
        Signature = TypeSignature(type),
        DocId = MemberDocId(type),
        Members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !IsCompilerGenerated(m) && IsDeclaredPublic(m))
            .OrderBy(m => m.MetadataToken)
            .Select(m => new
            {
                Kind = m.MemberType.ToString(),
                Name = m is ConstructorInfo ? "#ctor" : m.Name,
                Signature = m switch
                {
                    ConstructorInfo ctor => ConstructorSignature(ctor),
                    MethodInfo method => MethodSignature(method),
                    PropertyInfo prop => PropertySignature(prop),
                    FieldInfo field => FieldSignature(field),
                    EventInfo ev => EventSignature(ev),
                    _ => m.ToString() ?? m.Name,
                },
                DocId = MemberDocId(m),
            })
            .ToArray(),
    })
    .ToArray();

Console.WriteLine(JsonSerializer.Serialize(types, new JsonSerializerOptions { WriteIndented = true }));
'''


@dataclass(frozen=True)
class DocEntry:
    summary: str
    remarks: str
    returns: str
    parameters: dict[str, str]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Markdown API reference from .NET XML documentation.")
    parser.add_argument("--assembly", required=True, type=Path, help="Built library assembly path")
    parser.add_argument("--xml", required=True, type=Path, help="XML documentation path")
    parser.add_argument("--output", required=True, type=Path, help="Markdown output path")
    parser.add_argument("--title", required=True, help="Markdown page title")
    parser.add_argument("--package", required=True, help="NuGet package ID")
    parser.add_argument("--check", action="store_true", help="Fail if output is not up to date")
    return parser.parse_args()


def normalize_text(text: str) -> str:
    text = re.sub(r"\s+", " ", text).strip()
    return text.replace(" </", "</")


def cref_label(value: str) -> str:
    value = value.split(":", 1)[-1]
    value = value.replace("`1", "").replace("`2", "").replace("``1", "").replace("``2", "")
    return value.split(".")[-1]


def node_text(node: ET.Element | None) -> str:
    if node is None:
        return ""
    parts: list[str] = []
    if node.text:
        parts.append(node.text)
    for child in list(node):
        if child.tag == "see":
            parts.append(f"`{cref_label(child.attrib.get('cref') or child.attrib.get('langword', ''))}`")
        elif child.tag in {"c", "paramref"}:
            value = child.text or child.attrib.get("name", "")
            parts.append(f"`{value}`")
        else:
            parts.append(node_text(child))
        if child.tail:
            parts.append(child.tail)
    return normalize_text("".join(parts))


def load_docs(xml_path: Path) -> dict[str, DocEntry]:
    root = ET.parse(xml_path).getroot()
    docs: dict[str, DocEntry] = {}
    for member in root.findall("./members/member"):
        name = member.attrib.get("name")
        if not name:
            continue
        docs[name] = DocEntry(
            summary=node_text(member.find("summary")),
            remarks=node_text(member.find("remarks")),
            returns=node_text(member.find("returns")),
            parameters={
                param.attrib["name"]: node_text(param)
                for param in member.findall("param")
                if "name" in param.attrib
            },
        )
    return docs


def doc_for(docs: dict[str, DocEntry], doc_id: str) -> DocEntry:
    if doc_id in docs:
        return docs[doc_id]
    prefix = doc_id.split("(", 1)[0]
    matches = [value for key, value in docs.items() if key == prefix or key.startswith(prefix + "(")]
    if matches:
        return matches[0]
    return DocEntry("", "", "", {})


def run_inspector(assembly_path: Path) -> list[dict[str, object]]:
    with tempfile.TemporaryDirectory(prefix="dotnet_api_ref_") as temp_dir:
        temp = Path(temp_dir)
        (temp / "ApiInspector.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
            "<Nullable>enable</Nullable></PropertyGroup></Project>",
            encoding="utf-8",
        )
        (temp / "Program.cs").write_text(CSHARP_INSPECTOR, encoding="utf-8")
        result = subprocess.run(
            ["dotnet", "run", "--project", str(temp / "ApiInspector.csproj"), "--", str(assembly_path.resolve())],
            check=True,
            capture_output=True,
            text=True,
        )
    return json.loads(result.stdout)


def render_doc(entry: DocEntry, *, include_parameters: bool) -> list[str]:
    lines: list[str] = []
    if entry.summary:
        lines.extend([entry.summary, ""])
    if entry.remarks:
        lines.extend([f"Remarks: {entry.remarks}", ""])
    if entry.returns:
        lines.extend([f"Returns: {entry.returns}", ""])
    if include_parameters and entry.parameters:
        lines.append("Parameters:")
        for name, text in entry.parameters.items():
            lines.append(f"- `{name}`: {text}")
        lines.append("")
    return lines


def render_markdown(title: str, package: str, docs: dict[str, DocEntry], api: list[dict[str, object]]) -> str:
    lines = [
        f"# {title}",
        "",
        f"This page is generated from the `{package}` assembly public API and XML documentation comments.",
        "",
        "Run `python scripts/generate_api_reference.py --help` from the repository root to regenerate it.",
        "",
    ]
    current_namespace = None
    for api_type in api:
        namespace = str(api_type["Namespace"])
        if namespace != current_namespace:
            lines.extend([f"## {namespace}", ""])
            current_namespace = namespace

        lines.extend([f"### {api_type['Name']}", "", "```csharp", str(api_type["Signature"]), "```", ""])
        lines.extend(render_doc(doc_for(docs, str(api_type["DocId"])), include_parameters=False))

        members = api_type.get("Members", [])
        if members:
            lines.extend(["#### Members", ""])
            for member in members:  # type: ignore[assignment]
                member_dict = dict(member)
                display_name = str(member_dict["Name"]).replace("#ctor", str(api_type["Name"]))
                lines.extend([f"##### {display_name}", "", "```csharp", str(member_dict["Signature"]), "```", ""])
                lines.extend(render_doc(doc_for(docs, str(member_dict["DocId"])), include_parameters=True))

    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    args = parse_args()
    if not args.assembly.is_file():
        print(f"Assembly not found: {args.assembly}", file=sys.stderr)
        return 1
    if not args.xml.is_file():
        print(f"XML documentation not found: {args.xml}", file=sys.stderr)
        return 1

    docs = load_docs(args.xml)
    api = run_inspector(args.assembly)
    markdown = render_markdown(args.title, args.package, docs, api)

    if args.check:
        current = args.output.read_text(encoding="utf-8") if args.output.exists() else ""
        if current != markdown:
            print(f"{args.output} is not up to date. Regenerate the API reference.", file=sys.stderr)
            return 1
        print(f"[OK] {args.output} is up to date.")
        return 0

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(markdown, encoding="utf-8")
    print(f"Generated {args.output} from {len(api)} public types.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
