using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using SE2ScriptRuntime.Settings.Elements;

namespace SE2ScriptRuntime;

public sealed class Config : INotifyPropertyChanged
{
    private bool autoStartScripts;
    private string selectedScript = "Example.csx";
    private string scriptStatus = "Loading script list...";
    private string editorSource = string.Empty;
    private string editorStatus = "Load a script to begin editing.";
    private string editorDiagnostics = "No diagnostics yet. Select Check Script to compile the editor contents.";

    [XmlIgnore]
    public readonly string Title = "SE2 Script Manager";

    [Separator("Startup")]
    [Checkbox(label: "Auto-start scripts", description: "When enabled, all .csx files are started when the plugin loads. Only enable this for scripts you trust.")]
    public bool AutoStartScripts { get => autoStartScripts; set => SetField(ref autoStartScripts, value); }

    [Separator("In-game script editor")]
    [Textbox(label: "Script filename", description: "Use a filename such as Example.csx. Files are stored in the plugin Scripts folder.")]
    public string SelectedScript { get => selectedScript; set => SetField(ref selectedScript, value); }

    [MultilineTextbox(label: "C# script source", description: "Edit the selected .csx file here. Save before running.")]
    public string EditorSource { get => editorSource; set => SetField(ref editorSource, value); }

    [CodePreview(label: "Syntax preview", description: "A read-only colorized preview of common C# tokens. The editing field remains plain text.")]
    public string HighlightedPreview { get => EditorSource; set => EditorSource = value; }

    [MultilineTextbox(label: "Compiler diagnostics", description: "Errors and warnings from the most recent Check Script operation.")]
    public string EditorDiagnostics { get => editorDiagnostics; set => SetField(ref editorDiagnostics, value); }

    [MultilineTextbox(label: "Editor messages", description: "Load/save results and compiler diagnostics appear here.")]
    public string EditorStatus { get => editorStatus; set => SetField(ref editorStatus, value); }

    [Button(label: "Load Script", description: "Loads the selected script file into the editor.")]
    public void LoadEditorScript()
    {
        try { EditorSource = Plugin.Instance.Scripts.ReadScript(NormalizeScriptName(SelectedScript)); EditorDiagnostics = "Loaded file. Run Check Script to refresh diagnostics."; EditorStatus = $"Loaded {NormalizeScriptName(SelectedScript)}."; }
        catch (Exception ex) { EditorStatus = $"Load failed: {ex.Message}"; }
    }

    [Button(label: "Save Script", description: "Saves the editor contents to the selected .csx file.")]
    public void SaveEditorScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); Plugin.Instance.Scripts.SaveScript(name, EditorSource); EditorStatus = $"Saved {name}."; EditorDiagnostics = "Saved. Run Check Script to refresh diagnostics."; RefreshStatus(); }
        catch (Exception ex) { EditorStatus = $"Save failed: {ex.Message}"; }
    }

    [Button(label: "Check Script", description: "Compiles the editor contents and displays errors and warnings without running it.")]
    public void CheckEditorScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            var diagnostics = Plugin.Instance.Scripts.Validate(EditorSource);
            EditorDiagnostics = diagnostics.Length == 0 ? $"{name}: no compiler errors or warnings." : string.Join("\n", diagnostics); EditorStatus = $"Check complete for {name}. {diagnostics.Length} diagnostic(s).";
        }
        catch (Exception ex) { EditorStatus = $"Check failed: {ex.Message}"; }
    }

    [Button(label: "Save and Run Script", description: "Saves the current editor contents, then starts that script.")]
    public void SaveAndRunEditorScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            Plugin.Instance.Scripts.SaveScript(name, EditorSource);
            var started = Plugin.Instance.Scripts.Start(name);
            EditorStatus = started ? $"Saved and started {name}." : $"{name} is already running."; EditorDiagnostics = "Saved before launch. Use Check Script to review compiler diagnostics.";
            RefreshStatus();
        }
        catch (Exception ex) { EditorStatus = $"Run failed: {ex.Message}"; }
    }


    [Button(label: "Insert Log Snippet", description: "Adds an Api.Log call at the end of the editor contents.")]
    public void InsertLogSnippet() => EditorSource += (string.IsNullOrEmpty(EditorSource) ? string.Empty : Environment.NewLine) + "Api.Log(\"Your message here\");";

    [Button(label: "Insert Cancellation Loop", description: "Adds a cooperative loop template that can be stopped by the manager.")]
    public void InsertCancellationLoop() => EditorSource += (string.IsNullOrEmpty(EditorSource) ? string.Empty : Environment.NewLine) +
        "while (!Api.CancellationToken.IsCancellationRequested)\n{\n    await Api.Delay(TimeSpan.FromSeconds(1));\n    Api.Log(\"Running\");\n}";

    [Separator("Script controls")]
    [Button(label: "Refresh Script List", description: "Rescan the Scripts folder and update runtime status.")]
    public void RefreshScriptList() => RefreshStatus();

    [Button(label: "Start Selected Script", description: "Starts the selected .csx script from disk.")]
    public void StartSelectedScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); var started = Plugin.Instance.Scripts.Start(name); ScriptStatus = started ? $"Started: {name}" : $"Could not start {name}. It may already be running or the file does not exist."; RefreshStatus(); }
        catch (Exception ex) { ScriptStatus = $"Start failed: {ex.Message}"; }
    }

    [Button(label: "Stop Selected Script", description: "Requests cancellation of the selected script.")]
    public void StopSelectedScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); var stopped = Plugin.Instance.Scripts.Stop(name); ScriptStatus = stopped ? $"Stop requested: {name}" : $"Not running: {name}"; RefreshStatus(); }
        catch (Exception ex) { ScriptStatus = $"Stop failed: {ex.Message}"; }
    }

    [Button(label: "Start All Scripts", description: "Starts every .csx file in the Scripts folder.")]
    public void StartAllScripts() { var count = Plugin.Instance.Scripts.StartAll(); ScriptStatus = $"Start requested for {count} script(s)."; RefreshStatus(); }

    [Button(label: "Stop All Scripts", description: "Requests cancellation of every running script.")]
    public void StopAllScripts() { var count = Plugin.Instance.Scripts.StopAll(); ScriptStatus = $"Stop requested for {count} script(s)."; RefreshStatus(); }

    public void RefreshStatus()
    {
        if (Plugin.Instance?.Scripts == null) return;
        var files = Plugin.Instance.Scripts.ListScripts();
        var running = Plugin.Instance.Scripts.RunningScripts;
        ScriptStatus = $"Scripts folder: {Plugin.Instance.Scripts.ScriptDirectory}\nAvailable ({files.Length}): {string.Join(", ", files)}\nRunning ({running.Length}): {string.Join(", ", running)}\n\nRecent output:\n{string.Join("\n", ScriptLogger.Snapshot(30))}";
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