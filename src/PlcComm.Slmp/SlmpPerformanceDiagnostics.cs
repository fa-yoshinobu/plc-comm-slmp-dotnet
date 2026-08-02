namespace PlcComm.Slmp;

internal static class SlmpPerformanceDiagnostics
{
    private static readonly AsyncLocal<Action<string>?> CurrentSink = new();

    internal static Action<string>? Sink
    {
        get => CurrentSink.Value;
        set => CurrentSink.Value = value;
    }

    internal static void Report(string eventName) => CurrentSink.Value?.Invoke(eventName);
}
