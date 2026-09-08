"""Compile and exercise sample cleanup guards with offline write/readback probes."""
from __future__ import annotations

import re
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAMESPACE = "PlcComm.Slmp"
PROJECT = "src/PlcComm.Slmp/PlcComm.Slmp.csproj"
UNKNOWN = "new SlmpOperationOutcomeUnknownException(SlmpOutcomeUnknownReason.Timeout, new TimeoutException())"
BLOCK = re.compile(
    r"^(?P<indent> *)(?P<flags>bool write\d+Confirmed = false;\n(?: *bool write\d+Confirmed = false;\n)*)"
    r" *(?P<unknown>bool outcomeUnknown\d+ = false;)\n"
    r"(?P=indent)try\n(?P=indent)\{\n(?P<body>.*?)"
    r"^(?P=indent)\}\n(?P<tail>(?P=indent)catch .*?^(?P=indent)finally\n(?P=indent)\{\n.*?^(?P=indent)\})",
    re.M | re.S,
)
WRITE = re.compile(r"await client\.Write\w+\(.*?\);", re.S)


def main() -> None:
    methods = []
    entries = []
    paths = [*sorted((ROOT / "docsrc/user").glob("*.md"))]
    for folder in ("samples", "examples"):
        paths.extend(sorted((ROOT / folder).rglob("Program.cs")))
    for path in paths:
        if path.name == "API_REFERENCE.md":
            continue
        for match in BLOCK.finditer(path.read_text(encoding="utf-8-sig")):
            flags = re.findall(r"bool (write\d+Confirmed)", match["flags"])
            body = []
            sequence = []
            for write in WRITE.finditer(match["body"]):
                assignment = re.match(r"\s*(write\d+Confirmed = true;)", match["body"][write.end():])
                assert assignment, (path, write[0])
                flag = assignment[1].split()[0]
                sequence.append(flag)
                body.extend([f'await probe.Write("{flag}");', assignment[1]])
            assert sequence, path
            tail = match["tail"]
            cleanup_start = tail.index("finally")
            handler, cleanup = tail[:cleanup_start], tail[cleanup_start:]
            # Keep the real finally guards/order. Only replace PLC helper calls.
            def replace(write: re.Match[str]) -> str:
                prefix = cleanup[:write.start()]
                flag = re.findall(r"if \((write\d+Confirmed)\)", prefix)[-1]
                return f'await probe.Restore("{flag}");'
            cleanup = WRITE.sub(replace, cleanup)
            index = len(methods)
            label = f"{path.relative_to(ROOT).as_posix()}:{match.start()}"
            methods.append(
                f"static async Task Example{index}(Probe probe) {{\n"
                + match["flags"] + match["unknown"] + "\ntry {\n"
                + "\n".join(body) + '\nprobe.Readback();\n}\n'
                + handler + cleanup + "\n}\n"
            )
            entries.append(f'await Verify("{label}", Example{index}, new string[] {{'
                           + ",".join(f'"{flag}"' for flag in sequence) + "});")
    assert methods, "No sample cleanup blocks found"
    harness = r'''
static async Task Verify(string label, Func<Probe, Task> example, string[] writes) {
    var scenarios = new List<(string, int, Exception?)> { ("none", 0, null), ("readback", 1, new InvalidOperationException("readback")) };
    foreach (var phase in new[] { "write", "restore" }) {
        int count = phase == "write" ? writes.Length : writes.Distinct().Count();
        for (int i = 1; i <= count; i++) {
            scenarios.Add((phase, i, Unknown()));
            scenarios.Add((phase, i, new InvalidOperationException("rejected")));
        }
    }
    foreach (var (phase, at, error) in scenarios) {
        var probe = new Probe(phase, at, error);
        Exception? caught = null;
        try { await example(probe); } catch (Exception ex) { caught = ex; }
        if (!ReferenceEquals(caught, error)) throw new Exception($"{label} {phase}/{at}: primary exception changed", caught);
        var expected = probe.Completed.AsEnumerable().Reverse();
        if (phase == "write" && error?.GetType() == Unknown().GetType()) expected = Array.Empty<string>();
        if (phase == "restore") expected = expected.Take(at);
        if (!probe.Restored.SequenceEqual(expected)) throw new Exception($"{label} {phase}/{at}: incorrect restoration sequence");
    }
}

sealed class Probe(string phase, int at, Exception? error) {
    readonly Dictionary<string, int> counts = new();
    public readonly List<string> Completed = new();
    public readonly List<string> Restored = new();
    void Step(string kind) {
        counts[kind] = counts.GetValueOrDefault(kind) + 1;
        if (phase == kind && counts[kind] == at) throw error!;
    }
    public Task Write(string flag) {
        Step("write"); if (!Completed.Contains(flag)) Completed.Add(flag); return Task.CompletedTask;
    }
    public Task Restore(string flag) {
        Restored.Add(flag); Step("restore"); return Task.CompletedTask;
    }
    public void Readback() => Step("readback");
}
'''
    source = f"using {NAMESPACE};\n" + "\n".join(entries) + "\n" + "\n".join(methods)
    source += f"static Exception Unknown() => {UNKNOWN};\n" + harness
    project = f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
</PropertyGroup><ItemGroup><ProjectReference Include="{(ROOT / PROJECT).as_posix()}" /></ItemGroup></Project>'''
    with tempfile.TemporaryDirectory(prefix="sample-cleanup-") as tmp:
        folder = Path(tmp)
        (folder / "Probe.csproj").write_text(project, encoding="utf-8")
        (folder / "Program.cs").write_text(source, encoding="utf-8")
        result = subprocess.run(["dotnet", "run", "--project", str(folder / "Probe.csproj"), "--verbosity", "quiet"], capture_output=True, text=True, encoding="utf-8", errors="replace")
        assert result.returncode == 0, result.stdout + result.stderr
    print(f"{len(methods)} maintained C# cleanup blocks passed all offline failure scenarios")


if __name__ == "__main__":
    main()
