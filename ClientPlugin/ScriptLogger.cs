using System;
using System.Collections.Concurrent;
using System.Linq;

namespace SE2ScriptRuntime;

public static class ScriptLogger
{
    private const int MaxEntries = 250;
    private static readonly ConcurrentQueue<string> Entries = new();

    public static void Write(string scriptName, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{scriptName}] {message}";
        Entries.Enqueue(line);
        while (Entries.Count > MaxEntries)
            Entries.TryDequeue(out _);
    }

    public static string[] Snapshot(int count = 40)
    {
        return Entries.ToArray().TakeLast(Math.Clamp(count, 1, MaxEntries)).ToArray();
    }
}
