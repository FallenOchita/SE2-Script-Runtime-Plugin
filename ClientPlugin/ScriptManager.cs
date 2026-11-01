using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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
    public string[] RunningScripts => _running.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    public string[] ListScripts() => Directory.EnumerateFiles(ScriptDirectory, "*.csx").Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public ScriptManager(string scriptDirectory)
    {
        ScriptDirectory = Path.GetFullPath(scriptDirectory);
        Directory.CreateDirectory(ScriptDirectory);
        _options = ScriptOptions.Default.WithImports("System", "System.Threading", "System.Threading.Tasks", "SE2ScriptRuntime")
            .WithReferences(typeof(ScriptGlobals).Assembly, typeof(Task).Assembly);
        EnsureExampleScript();
    }

    private string ResolveScriptPath(string name)
    {
        var safeName = Path.GetFileName(name?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeName)) throw new InvalidOperationException("Enter a script filename.");
        if (!safeName.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)) safeName += ".csx";
        if (safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || safeName.Contains(Path.DirectorySeparatorChar) || safeName.Contains(Path.AltDirectorySeparatorChar))
            throw new InvalidOperationException("Invalid script filename.");
        return Path.Combine(ScriptDirectory, safeName);
    }

    public string ReadScript(string name)
    {
        var path = ResolveScriptPath(name);
        if (!File.Exists(path)) throw new FileNotFoundException("The selected script does not exist.", path);
        return File.ReadAllText(path);
    }

    public void SaveScript(string name, string source)
    {
        var path = ResolveScriptPath(name);
        File.WriteAllText(path, source ?? string.Empty);
        ScriptLogger.Write(Path.GetFileName(path), "Saved from in-game editor.");
    }

    public ScriptDiagnosticInfo[] ValidateDetailed(string source)
    {
        var diagnostics = CSharpScript.Create(source ?? string.Empty, _options, typeof(ScriptGlobals)).Compile();
        return diagnostics
            .Where(d => (d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
                && d.Location != Location.None && d.Location.IsInSource)
            .Select(d =>
            {
                var span = d.Location.GetLineSpan();
                return new ScriptDiagnosticInfo(
                    d.Severity.ToString(),
                    d.Id,
                    d.Location.SourceSpan.Start,
                    d.Location.SourceSpan.Length,
                    span.StartLinePosition.Line + 1,
                    span.StartLinePosition.Character + 1,
                    d.GetMessage());
            }).ToArray();
    }

    public string[] Validate(string source)
        => ValidateDetailed(source).Select(d => d.ToString()).ToArray();

    public int StartAll()
    {
        var started = 0;
        foreach (var file in Directory.EnumerateFiles(ScriptDirectory, "*.csx").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (Start(file)) started++;
        return started;
    }

    public int StartConfigured(string[] scriptNames)
    {
        var started = 0;
        foreach (var name in (scriptNames ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (Start(name))
                    started++;
                else
                    ScriptLogger.Write(name, "Auto-start skipped: script is missing, already running, or failed validation.");
            }
            catch (Exception ex)
            {
                ScriptLogger.Write(name, $"Auto-start failed: {ex.Message}");
            }
        }
        return started;
    }

    public bool Start(string pathOrName)
    {
        var fullPath = Path.IsPathRooted(pathOrName) ? Path.GetFullPath(pathOrName) : ResolveScriptPath(pathOrName);
        var root = ScriptDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Scripts must be located inside the configured Scripts directory.");
        if (!File.Exists(fullPath)) return false;
        var name = Path.GetFileName(fullPath);
        if (Config.Current?.IsScriptDisabled(name) == true)
        {
            ScriptLogger.Write(name, "Launch blocked: this script is disabled in Script controls.");
            return false;
        }
        if (_running.ContainsKey(name)) return false;

        // Apply the same compile gate to every launch path, including Start All
        // and auto-start. Editor-only validation would leave those paths unchecked.
        var diagnostics = Validate(File.ReadAllText(fullPath));
        var errors = diagnostics.Where(d => d.StartsWith("Error ", StringComparison.Ordinal)).ToArray();
        if (errors.Length > 0)
        {
            ScriptLogger.Write(name, $"Launch blocked: {errors.Length} compiler error(s).");
            foreach (var diagnostic in errors)
                ScriptLogger.Write(name, diagnostic);
            return false;
        }

        var warnings = diagnostics.Where(d => d.StartsWith("Warning ", StringComparison.Ordinal)).ToArray();
        foreach (var warning in warnings)
            ScriptLogger.Write(name, warning);

        var cts = new CancellationTokenSource();
        var state = new RunningScript(cts);
        if (!_running.TryAdd(name, state)) { cts.Dispose(); return false; }
        ScriptLogger.Write(name, "Start requested.");
        _ = Task.Run(() => ExecuteAsync(fullPath, name, state));
        return true;
    }

    public bool Stop(string name)
    {
        if (!_running.TryGetValue(name, out var state)) return false;
        try { state.Cancellation.Cancel(); }
        catch (ObjectDisposedException) { return false; }
        ScriptLogger.Write(name, "Stop requested.");
        return true;
    }

    public int StopAll()
    {
        var stopped = 0;
        foreach (var pair in _running)
            if (Stop(pair.Key)) stopped++;
        return stopped;
    }

    private async Task ExecuteAsync(string path, string name, RunningScript state)
    {
        try
        {
            var source = await File.ReadAllTextAsync(path, state.Cancellation.Token).ConfigureAwait(false);
            var globals = new ScriptGlobals(name, state.Cancellation.Token);
            await CSharpScript.RunAsync(source, _options, globals, typeof(ScriptGlobals), state.Cancellation.Token).ConfigureAwait(false);
            ScriptLogger.Write(name, "Completed.");
            Log.Default.WriteLine($"[SE2ScriptRuntime:{name}] Completed.");
        }
        catch (OperationCanceledException) { ScriptLogger.Write(name, "Cancellation requested."); Log.Default.WriteLine($"[SE2ScriptRuntime:{name}] Cancellation requested."); }
        catch (Exception ex) { ScriptLogger.Write(name, $"ERROR: {ex}"); Log.Default.WriteLine(LogSeverity.Error, $"[SE2ScriptRuntime:{name}] {ex}"); }
        finally { _running.TryRemove(name, out _); state.Cancellation.Dispose(); }
    }

    private void EnsureExampleScript()
    {
        var path = Path.Combine(ScriptDirectory, "Example.csx");
        if (File.Exists(path)) return;
        File.WriteAllText(path,
            "Api.Log(\"Example script started. Edit or remove this file to change startup behavior.\");\n" +
            "while (!Api.CancellationToken.IsCancellationRequested)\n{\n" +
            "    await Api.Delay(TimeSpan.FromSeconds(10));\n" +
            "    Api.Log(\"Example script is running.\");\n}\n");
    }

    private sealed class RunningScript
    {
        public CancellationTokenSource Cancellation { get; }
        public RunningScript(CancellationTokenSource cancellation) => Cancellation = cancellation;
    }
}