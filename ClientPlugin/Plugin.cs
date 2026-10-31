using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Keen.VRage.Core.Plugins;
using Keen.VRage.Library.Diagnostics;

namespace SE2ScriptRuntime;

public sealed class Plugin : IPlugin
{
    public const string Name = "SE2ScriptRuntime";
    public static Plugin Instance { get; private set; }

#pragma warning disable CS0649
    private static Func<string, string, string> GetConfigPath;
#pragma warning restore CS0649

    public string DataDir { get; } = GetConfigPath(Name, null);
    public ScriptManager Scripts { get; private set; }

    public Plugin()
    {
        Instance = this;
        Directory.CreateDirectory(DataDir);
        Scripts = new ScriptManager(Path.Combine(DataDir, "Scripts"));
        Log.Default.WriteLine($"[{Name}] Loaded. Script folder: {Scripts.ScriptDirectory}");
        var harmony = new Harmony(Name);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Scripts.StartAll();
    }

    // Pulsar invokes this method from the plugin's settings button.
    // The full in-game manager screen is planned for the next milestone.
    public void OpenConfigDialog()
    {
        Log.Default.WriteLine($"[{Name}] Scripts are stored in: {Scripts.ScriptDirectory}");
        Log.Default.WriteLine($"[{Name}] Running scripts: {Scripts.RunningCount}");
    }
}
