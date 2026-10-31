using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using SE2ScriptRuntime.Settings.Elements;

namespace SE2ScriptRuntime;

public sealed class Config : INotifyPropertyChanged
{
    private bool autoStartScripts;
    private string selectedScript = "Example.csx";
    private string scriptStatus = "Loading script list...";

    [XmlIgnore]
    public readonly string Title = "SE2 Script Manager";

    [Separator("Startup")]
    [Checkbox(label: "Auto-start scripts", description: "When enabled, all .csx files are started when the plugin loads. Only enable this for scripts you trust.")]
    public bool AutoStartScripts
    {
        get => autoStartScripts;
        set => SetField(ref autoStartScripts, value);
    }

    [Separator("Script controls")]
    [Textbox(label: "Selected script", description: "Enter a .csx filename from the Scripts folder.")]
    public string SelectedScript
    {
        get => selectedScript;
        set => SetField(ref selectedScript, value);
    }

    [MultilineTextbox(label: "Scripts and status", description: "Read the available script names and runtime state here. Use Refresh List after changing files.")]
    public string ScriptStatus
    {
        get => scriptStatus;
        set => SetField(ref scriptStatus, value);
    }

    [Button(label: "Refresh Script List", description: "Rescan the Scripts folder and update the status.")]
    public void RefreshScriptList() => RefreshStatus();

    [Button(label: "Start Selected Script", description: "Starts the selected .csx script.")]
    public void StartSelectedScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            var path = System.IO.Path.Combine(Plugin.Instance.Scripts.ScriptDirectory, name);
            var started = Plugin.Instance.Scripts.Start(path);
            ScriptStatus = started ? $"Started: {name}" : $"Could not start {name}. It may already be running or the file does not exist.";
        }
        catch (Exception ex) { ScriptStatus = $"Start failed: {ex.Message}"; }
    }

    [Button(label: "Stop Selected Script", description: "Requests cancellation of the selected script.")]
    public void StopSelectedScript()
    {
        var name = NormalizeScriptName(SelectedScript);
        ScriptStatus = Plugin.Instance.Scripts.Stop(name) ? $"Stop requested: {name}" : $"Not running: {name}";
    }

    [Button(label: "Start All Scripts", description: "Starts every .csx file in the Scripts folder.")]
    public void StartAllScripts()
    {
        var count = Plugin.Instance.Scripts.StartAll();
        ScriptStatus = $"Start requested for {count} script(s).";
    }

    [Button(label: "Stop All Scripts", description: "Requests cancellation of every running script.")]
    public void StopAllScripts()
    {
        var count = Plugin.Instance.Scripts.StopAll();
        ScriptStatus = $"Stop requested for {count} script(s).";
    }

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
        return name;
    }

    public static Config Default => new Config();
    public static Config Current { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
