using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Keen.VRage.Library.Diagnostics;

namespace SE2ScriptRuntime;

public sealed class ScriptManager
{
    private readonly ConcurrentDictionary<string, RunningScript> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScriptOptions _options;

    public string ScriptDirectory { get; }
    public int RunningCount => _running.Count;

    public ScriptManager(string scriptDirectory)
    {
        ScriptDirectory = scriptDirectory;
        Directory.CreateDirectory(ScriptDirectory);
        _options = ScriptOptions.Default
            .WithImports("System", "System.Threading", "System.Threading.Tasks", "SE2ScriptRuntime")
            .WithReferences(typeof(ScriptGlobals).Assembly, typeof(Task).Assembly);
        EnsureExampleScript();
    }

    public void StartAll()
    {
        foreach (var file in Directory.EnumerateFiles(ScriptDirectory, "*.csx").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            Start(file);
    }

    public bool Start(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(Path.GetFullPath(ScriptDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Scripts must be located inside the configured Scripts directory.");
        if (!File.Exists(fullPath))
            return false;

        var name = Path.GetFileName(fullPath);
        var cts = new CancellationTokenSource();
        var state = new RunningScript(cts);
        if (!_running.TryAdd(name, state))
        {
            cts.Dispose();
            return false;
        }

        _ = ExecuteAsync(fullPath, name, state);
        return true;
    }

    public bool Stop(string name)
    {
        if (!_running.TryGetValue(name, out var state))
            return false;
        state.Cancellation.Cancel();
        return true;
    }

    public void StopAll()
    {
        foreach (var state in _running.Values)
            state.Cancellation.Cancel();
    }

    private async Task ExecuteAsync(string path, string name, RunningScript state)
    {
        try
        {
            var source = await File.ReadAllTextAsync(path, state.Cancellation.Token).ConfigureAwait(false);
            var globals = new ScriptGlobals(name, state.Cancellation.Token);
            await CSharpScript.RunAsync(source, _options, globals, typeof(ScriptGlobals), state.Cancellation.Token).ConfigureAwait(false);
            Log.Default.WriteLine($"[SE2ScriptRuntime:{name}] Completed.");
        }
        catch (OperationCanceledException)
        {
            Log.Default.WriteLine($"[SE2ScriptRuntime:{name}] Cancellation requested.");
        }
        catch (Exception ex)
        {
            Log.Default.WriteLine(LogSeverity.Error, $"[SE2ScriptRuntime:{name}] {ex}");
        }
        finally
        {
            _running.TryRemove(name, out _);
            state.Cancellation.Dispose();
        }
    }

    private void EnsureExampleScript()
    {
        var path = Path.Combine(ScriptDirectory, "Example.csx");
        if (File.Exists(path))
            return;

        File.WriteAllText(path,
            "Api.Log(\"Example script started. Edit or remove this file to change startup behavior.\");\n" +
            "while (!Api.CancellationToken.IsCancellationRequested)\n" +
            "{\n" +
            "    await Api.Delay(TimeSpan.FromSeconds(10));\n" +
            "    Api.Log(\"Example script is running.\");\n" +
            "}\n");
    }

    private sealed class RunningScript
    {
        public CancellationTokenSource Cancellation { get; }
        public RunningScript(CancellationTokenSource cancellation) => Cancellation = cancellation;
    }
}
