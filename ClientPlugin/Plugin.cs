using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Keen.VRage.Core.Plugins;
using Keen.VRage.Library.Diagnostics;
using SE2ScriptRuntime.Settings;
using SE2ScriptRuntime.Tools;

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
        Config.Current = ConfigStorage.Load();
        Scripts = new ScriptManager(Path.Combine(DataDir, "Scripts"));
        Config.Current.RefreshStatus();

        Log.Default.WriteLine($"[{Name}] Loaded. Script folder: {Scripts.ScriptDirectory}");
        var harmony = new Harmony(Name);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        if (Config.Current.AutoStartScripts)
        {
            var configuredScripts = Config.Current.GetAutoStartScriptNames();
            var started = Scripts.StartConfigured(configuredScripts);
            Log.Default.WriteLine($"[{Name}] Auto-start requested for {started} of {configuredScripts.Length} configured script(s).");
        }
    }

    public void OpenConfigDialog()
    {
        try
        {
            var sharedUi = GameAccess.GetSharedUI();
            if (sharedUi == null)
            {
                Log.Default.WriteLine(LogSeverity.Warning, $"[{Name}] SharedUIComponent not available");
                return;
            }

            var generator = new SettingsGenerator();
            var viewModel = new SettingsScreenViewModel(
                generator.Title,
                panel => generator.PopulateContent(panel),
                () => ConfigStorage.Save(Config.Current));

            sharedUi.CreateScreen<SettingsScreen>(viewModel, showCursor: true);
        }
        catch (Exception e)
        {
            Log.Default.WriteLine(LogSeverity.Error, $"[{Name}] OpenConfigDialog failed: {e}");
        }
    }
}
