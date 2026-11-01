using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using SE2ScriptRuntime.Settings.Elements;

namespace SE2ScriptRuntime;

public sealed class Config : INotifyPropertyChanged
{
    private bool autoStartScripts;
    private string autoStartScriptNames = string.Empty;
    private string disabledScriptNames = string.Empty;
    private string selectedScript = "Example.csx";
    private string scriptStatus = "Loading script list...";
    private string editorSource = string.Empty;
    private string editorStatus = "Load a script to begin editing.";
    private string editorDiagnostics = "No diagnostics yet. Select Check Script to compile the editor contents.";
    private string editorDiagnosticsSummary = "Not checked yet.";
    private string selectedCompletion = "Api.Log(\"Your message here\");";

    [XmlIgnore]
    public readonly string Title = "SE2 Script Manager";

    [Separator("Startup")]
    [Checkbox(label: "Enable configured auto-start", description: "When enabled, only scripts listed below start when the plugin loads.")]
    public bool AutoStartScripts { get => autoStartScripts; set => SetField(ref autoStartScripts, value); }


    [MultilineTextbox(label: "Auto-start script list", description: "One .csx filename per line. Changes are saved when you close the settings window.")]
    public string AutoStartScriptNames { get => autoStartScriptNames; set => SetField(ref autoStartScriptNames, value); }

    [Button(label: "Add Selected Script to Auto-start", description: "Adds the selected filename to the startup list without duplicating it.")]
    public void AddSelectedScriptToAutoStart()
    {
        var name = NormalizeScriptName(SelectedScript);
        if (!Plugin.Instance.Scripts.ListScripts().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            ScriptStatus = $"Cannot add {name}: the script does not exist in the Scripts folder.";
            return;
        }
        var names = GetAutoStartScriptNames();
        if (Array.Exists(names, item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
        {
            ScriptStatus = $"{name} is already in the auto-start list.";
            return;
        }
        AutoStartScriptNames = string.Join(Environment.NewLine, names.Concat(new[] { name }));
        ScriptStatus = $"Added {name} to auto-start. Close settings to save changes.";
    }

    [Button(label: "Remove Selected Script from Auto-start", description: "Removes the selected filename from the startup list.")]
    public void RemoveSelectedScriptFromAutoStart()
    {
        var name = NormalizeScriptName(SelectedScript);
        var names = GetAutoStartScriptNames();
        var remaining = names.Where(item => !string.Equals(item, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        AutoStartScriptNames = string.Join(Environment.NewLine, remaining);
        ScriptStatus = remaining.Length == names.Length
            ? $"{name} was not in the auto-start list."
            : $"Removed {name} from auto-start. Close settings to save changes.";
    }

    [Separator("Per-script controls")]
    [MultilineTextbox(label: "Disabled scripts", description: "One .csx filename per line. Disabled scripts cannot be started until enabled again. Saved when settings close.")]
    public string DisabledScriptNames { get => disabledScriptNames; set => SetField(ref disabledScriptNames, value); }

    [Button(label: "Disable Selected Script", description: "Prevents the selected script from launching and requests cancellation if it is running.")]
    public void DisableSelectedScript()
    {
        var name = NormalizeScriptName(SelectedScript);
        if (!Plugin.Instance.Scripts.ListScripts().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            ScriptStatus = $"Cannot disable {name}: the script does not exist in the Scripts folder.";
            return;
        }

        var names = GetDisabledScriptNames();
        if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            DisabledScriptNames = string.Join(Environment.NewLine, names.Concat(new[] { name }));
        }

        var stopRequested = Plugin.Instance.Scripts.Stop(name);
        ScriptStatus = stopRequested
            ? $"Disabled {name}; cancellation requested. It may take time to stop if it does not check cancellation."
            : $"Disabled {name}. It will not start until enabled again.";
        RefreshStatus();
    }

    [Button(label: "Enable Selected Script", description: "Allows the selected script to be started again.")]
    public void EnableSelectedScript()
    {
        var name = NormalizeScriptName(SelectedScript);
        var names = GetDisabledScriptNames();
        var remaining = names.Where(item => !string.Equals(item, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        DisabledScriptNames = string.Join(Environment.NewLine, remaining);
        ScriptStatus = remaining.Length == names.Length
            ? $"{name} is already enabled."
            : $"Enabled {name}. Close settings to save changes.";
        RefreshStatus();
    }

    public string[] GetDisabledScriptNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (DisabledScriptNames ?? string.Empty)
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var name = NormalizeScriptName(entry.Trim());
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
            catch (InvalidOperationException)
            {
                // Ignore malformed entries so a bad config line cannot prevent plugin startup.
            }
        }
        return names.ToArray();
    }

    public bool IsScriptDisabled(string name)
    {
        try
        {
            var normalized = NormalizeScriptName(name);
            return GetDisabledScriptNames().Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public string[] GetAutoStartScriptNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (AutoStartScriptNames ?? string.Empty)
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var name = NormalizeScriptName(entry.Trim());
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
            catch (InvalidOperationException)
            {
                // Ignore malformed entries so a bad config line cannot prevent plugin startup.
            }
        }
        return names.ToArray();
    }

    [Separator("In-game script editor")]
    [Textbox(label: "Script filename", description: "Use a filename such as Example.csx. Files are stored in the plugin Scripts folder.")]
    public string SelectedScript { get => selectedScript; set => SetField(ref selectedScript, value); }

    [MultilineTextbox(label: "C# script source", description: "Edit the selected .csx file here. Save before running.")]
    public string EditorSource { get => editorSource; set => SetField(ref editorSource, value); }

    [CodePreview(label: "Syntax preview", description: "A read-only colorized preview of common C# tokens. The editing field remains plain text.")]
    public string HighlightedPreview { get => EditorSource; set => EditorSource = value; }

    [CompletionPicker(label: "Code completion helpers", description: "Choose a known host-API snippet, then insert it into the editor. These are templates, not context-aware IntelliSense.")]
    public string SelectedCompletion { get => selectedCompletion; set => SetField(ref selectedCompletion, value); }

    [Button(label: "Insert Selected Completion", description: "Appends the selected code template to the editor source.")]
    public void InsertSelectedCompletion()
    {
        if (string.IsNullOrWhiteSpace(SelectedCompletion)) return;

        var source = EditorSource ?? string.Empty;
        var memberContexts = new[]
        {
            (Pattern: @"Api\\.$", Prefix: "Api."),
            (Pattern: @"TimeSpan\\.$", Prefix: "TimeSpan."),
            (Pattern: @"Api\\.CancellationToken\\.$", Prefix: "Api.CancellationToken."),
        };

        foreach (var context in memberContexts)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(source, context.Pattern))
            {
                EditorSource = System.Text.RegularExpressions.Regex.Replace(source, context.Pattern,
                    System.Text.RegularExpressions.Match.Empty.Success ? string.Empty : context.Prefix + SelectedCompletion);
                EditorStatus = $"Inserted {context.Prefix}{SelectedCompletion}. Review it before saving or running.";
                return;
            }
        }

        EditorSource += (string.IsNullOrEmpty(source) ? string.Empty : Environment.NewLine) +
            SelectedCompletion.Replace("\\n", Environment.NewLine);
        EditorStatus = "Inserted code template. Review it before saving or running.";
    }

    [Textbox(label: "Diagnostic summary", description: "Counts from the most recent compile check.")]
    public string EditorDiagnosticsSummary { get => editorDiagnosticsSummary; set => SetField(ref editorDiagnosticsSummary, value); }

    [MultilineTextbox(label: "Compiler diagnostics", description: "Errors and warnings from the most recent Check Script operation.")]
    public string EditorDiagnostics { get => editorDiagnostics; set => SetField(ref editorDiagnostics, value); }

    [MultilineTextbox(label: "Editor messages", description: "Load/save results and compiler diagnostics appear here.")]
    public string EditorStatus { get => editorStatus; set => SetField(ref editorStatus, value); }

    [Button(label: "Load Script", description: "Loads the selected script file into the editor.")]
    public void LoadEditorScript()
    {
        try { EditorSource = Plugin.Instance.Scripts.ReadScript(NormalizeScriptName(SelectedScript)); EditorDiagnosticsSummary = "Not checked yet."; EditorDiagnostics = "Loaded file. Run Check Script to refresh diagnostics."; EditorStatus = $"Loaded {NormalizeScriptName(SelectedScript)}."; }
        catch (Exception ex) { EditorStatus = $"Load failed: {ex.Message}"; }
    }

    [Button(label: "Save Script", description: "Saves the editor contents to the selected .csx file.")]
    public void SaveEditorScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); Plugin.Instance.Scripts.SaveScript(name, EditorSource); EditorStatus = $"Saved {name}."; EditorDiagnosticsSummary = "Not checked yet."; EditorDiagnostics = "Saved. Run Check Script to refresh diagnostics."; RefreshStatus(); }
        catch (Exception ex) { EditorStatus = $"Save failed: {ex.Message}"; }
    }

    [Button(label: "Check Script", description: "Compiles the editor contents and displays errors and warnings without running it.")]
    public void CheckEditorScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            var diagnostics = Plugin.Instance.Scripts.Validate(EditorSource);
            UpdateDiagnosticSummary(diagnostics);
            EditorDiagnostics = diagnostics.Length == 0 ? $"{name}: no compiler errors or warnings." : string.Join("\n", diagnostics);
            EditorStatus = $"Check complete for {name}. {diagnostics.Length} diagnostic(s).";
        }
        catch (Exception ex) { EditorStatus = $"Check failed: {ex.Message}"; }
    }

    [Button(label: "Save and Run Script", description: "Saves the current editor contents, then starts that script.")]
    public void SaveAndRunEditorScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            var diagnostics = Plugin.Instance.Scripts.Validate(EditorSource);
            UpdateDiagnosticSummary(diagnostics);
            EditorDiagnostics = diagnostics.Length == 0 ? $"{name}: no compiler errors or warnings." : string.Join("\n", diagnostics);
            var errors = CountDiagnostics(diagnostics, "Error ");
            if (errors > 0)
            {
                EditorStatus = $"Run blocked: {errors} compiler error(s). Fix the errors and try again.";
                return;
            }

            Plugin.Instance.Scripts.SaveScript(name, EditorSource);
            var started = Plugin.Instance.Scripts.Start(name);
            var warnings = CountDiagnostics(diagnostics, "Warning ");
            EditorStatus = started
                ? warnings > 0 ? $"Saved and started {name} with {warnings} compiler warning(s)." : $"Saved and started {name}."
                : Config.Current.IsScriptDisabled(name) ? $"Run blocked: {name} is disabled. Enable it in Script controls first." : $"{name} is already running or could not be started.";
            RefreshStatus();
        }
        catch (Exception ex) { EditorStatus = $"Run failed: {ex.Message}"; }
    }


    [Button(label: "Insert Log Snippet", description: "Adds an Api.Log call at the end of the editor contents.")]
    public void InsertLogSnippet() => EditorSource += (string.IsNullOrEmpty(EditorSource) ? string.Empty : Environment.NewLine) + "Api.Log(\"Your message here\");";

    [Button(label: "Insert Cancellation Loop", description: "Adds a cooperative loop template that can be stopped by the manager.")]
    public void InsertCancellationLoop() => EditorSource += (string.IsNullOrEmpty(EditorSource) ? string.Empty : Environment.NewLine) +
        "while (!Api.CancellationToken.IsCancellationRequested)\n{\n    await Api.Delay(TimeSpan.FromSeconds(1));\n    Api.Log(\"Running\");\n}";

    [Separator("Script management")]
    [Button(label: "Open Script Management Window", description: "Opens a searchable, sortable list with multi-select enable, disable, start, stop, and auto-start actions.")]
    public void OpenScriptManagementWindow() => Plugin.Instance.OpenScriptManagementDialog();

    [Button(label: "Refresh Script List", description: "Rescan the Scripts folder and update runtime status.")]
    public void RefreshScriptList() => RefreshStatus();

    [Button(label: "Start Selected Script", description: "Starts the selected .csx script from disk.")]
    public void StartSelectedScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); var started = Plugin.Instance.Scripts.Start(name); ScriptStatus = started ? $"Started: {name}" : Config.Current.IsScriptDisabled(name) ? $"Start blocked: {name} is disabled. Enable it first." : $"Could not start {name}. It may already be running or the file does not exist."; RefreshStatus(); }
        catch (Exception ex) { ScriptStatus = $"Start failed: {ex.Message}"; }
    }

    [Button(label: "Stop Selected Script", description: "Requests cancellation of the selected script.")]
    public void StopSelectedScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); var stopped = Plugin.Instance.Scripts.Stop(name); ScriptStatus = stopped ? $"Stop requested: {name}" : $"Not running: {name}"; RefreshStatus(); }
        catch (Exception ex) { ScriptStatus = $"Stop failed: {ex.Message}"; }
    }

    [Button(label: "Enable All Scripts", description: "Clears the disabled list so every script can be started again.")]
    public void EnableAllScripts()
    {
        var previous = GetDisabledScriptNames();
        DisabledScriptNames = string.Empty;
        ScriptStatus = previous.Length == 0
            ? "All scripts were already enabled."
            : $"Enabled all scripts. Cleared {previous.Length} disabled entry/entries. Close settings to save changes.";
        RefreshStatus();
    }

    [Button(label: "Disable All Scripts", description: "Disables every script currently in the Scripts folder and requests cancellation of running scripts.")]
    public void DisableAllScripts()
    {
        var files = Plugin.Instance.Scripts.ListScripts();
        DisabledScriptNames = string.Join(Environment.NewLine, files);
        var stopped = Plugin.Instance.Scripts.StopAll();
        ScriptStatus = $"Disabled {files.Length} script(s). Cancellation requested for {stopped} running script(s). Close settings to save changes.";
        RefreshStatus();
    }

    [Button(label: "Start All Enabled Scripts", description: "Starts scripts that are not disabled. Compiler errors still block launch.")]
    public void StartAllScripts() { var count = Plugin.Instance.Scripts.StartAll(); ScriptStatus = $"Start requested for {count} enabled script(s). Disabled scripts were skipped."; RefreshStatus(); }

    [Button(label: "Stop All Scripts", description: "Requests cancellation of every running script.")]
    public void StopAllScripts() { var count = Plugin.Instance.Scripts.StopAll(); ScriptStatus = $"Stop requested for {count} script(s)."; RefreshStatus(); }

    private void UpdateDiagnosticSummary(string[] diagnostics)
    {
        var errors = CountDiagnostics(diagnostics, "Error ");
        var warnings = CountDiagnostics(diagnostics, "Warning ");
        EditorDiagnosticsSummary = $"Errors: {errors} | Warnings: {warnings}";
    }

    private static int CountDiagnostics(string[] diagnostics, string prefix)
        => Array.FindAll(diagnostics, item => item.StartsWith(prefix, StringComparison.Ordinal)).Length;

    public void RefreshStatus()
    {
        if (Plugin.Instance?.Scripts == null) return;
        var files = Plugin.Instance.Scripts.ListScripts();
        var running = Plugin.Instance.Scripts.RunningScripts;
        var disabled = GetDisabledScriptNames();
        var statusLines = files.Select(file =>
        {
            var state = disabled.Contains(file, StringComparer.OrdinalIgnoreCase)
                ? "DISABLED"
                : running.Contains(file, StringComparer.OrdinalIgnoreCase) ? "RUNNING" : "ENABLED / STOPPED";
            return $"  [{state}] {file}";
        });
        var disabledFileCount = files.Count(file => disabled.Contains(file, StringComparer.OrdinalIgnoreCase));
        var enabledCount = files.Length - disabledFileCount;
        ScriptStatus = $"Scripts folder: {Plugin.Instance.Scripts.ScriptDirectory}\nSummary: {files.Length} total | {enabledCount} enabled | {disabledFileCount} disabled | {running.Length} running\nScript status:\n{string.Join("\n", statusLines)}\n\nRunning ({running.Length}): {string.Join(", ", running)}\nDisabled ({disabled.Length} configured): {string.Join(", ", disabled)}\n\nRecent output:\n{string.Join("\n", ScriptLogger.Snapshot(30))}";
    }

    private static string NormalizeScriptName(string value)
    {
        var name = System.IO.Path.GetFileName(value?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enter a script filename.");
        if (!name.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)) name += ".csx";
        if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException("The script filename contains invalid characters.");
        return name;
    }

    public static Config Default => new Config();
    public static Config Current { get; set; }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
}