# SE2 Script Runtime

An in-game C# scripting runtime plugin for Space Engineers 2, designed to load through Pulsar.

## Current status

**Early development / developer preview.** The repository provides a Pulsar client-plugin project, a Roslyn-based C# script host, script file management, compiler diagnostics, logging, cancellation, and an in-game C# editor. It has not yet been built or validated against a live SE2 installation in this development session.

## Current features

- Creates a per-plugin data directory and a `Scripts` folder.
- Lists `.csx` scripts and supports starting/stopping one or all scripts. Every launch path (individual start, Start All, and auto-start) compiles the script first and blocks launch on compiler errors; warnings are logged.
- In-game editor controls to load, edit, save, check, and run a selected script.
- Roslyn compiler errors and warnings with line and column locations. The **Save and Run Script** action checks the current editor contents first and blocks launch when compiler errors are present; warnings are shown but do not block launch. The runtime repeats this validation for scripts launched from disk, including Start All and auto-start, so those paths cannot bypass the compile gate.
- Per-script auto-start list, disabled by default. Add or remove the selected `.csx` file from the list; when enabled, only listed scripts are launched on plugin startup. The settings dialog saves the list to `Config.xml` when closed.
- Script management controls for enabling/disabling one or all scripts, starting all enabled scripts, and stopping all running scripts. Disabled scripts are blocked from individual starts, bulk starts, editor Save and Run, and auto-start. Disabling running scripts requests cooperative cancellation; they may not stop promptly if they do not observe their cancellation token. The settings dialog saves the disabled list when closed.
- Dedicated script-management window with filename search, name/status sorting, multi-selection, and selected-script enable, disable, start, stop, and auto-start actions. Runtime state, auto-start labels, and summary counts refresh every two seconds while the window is open. Changes to startup and disabled lists are saved when the window closes.
- Per-script status display showing enabled/stopped, running, or disabled state.
- Script logging and a bounded recent-output buffer.
- Small host API for logging, delays, cancellation checks, and script name.

The editor uses AvaloniaEdit with line numbers, word wrapping, and TextMate C# syntax highlighting directly in the editable surface. Compiler diagnostics are shown in their own panel with severity, line, and column. The completion picker now changes its candidates when the source ends in `Api.`, `Api.CancellationToken.`, or `TimeSpan.`; it filters members by a partially typed identifier and replaces that unfinished member access when **Insert Selected Completion** is used. Outside those recognized contexts, it offers the existing code templates. This is a limited host-API completion helper, not full Roslyn semantic IntelliSense: it does not track the caret inside the buffer, resolve arbitrary symbols, or provide inline error underlining or syntax coloring in the editable surface. The editor's syntax colors come from the bundled TextMate C# grammar; compiler diagnostics continue to come from Roslyn.

## Important security note

C# scripts execute with the permissions of the game process. **They are trusted code, not sandboxed code.** Only run scripts you wrote or have reviewed. A cancellation request is cooperative: a script that never awaits or checks its cancellation token may not stop promptly. Do not use this runtime to run unknown scripts.

The initial host deliberately does not expose SE2 world, grid, block, or player objects. Those APIs must be bridged only after their current public interfaces have been verified against the installed game assemblies.

## Installation / Pulsar listing

This repository includes `SE2ScriptRuntime.xml`, a PluginHub/Pulsar source registration descriptor. Add the repository as a Pulsar source/development folder using that descriptor, then enable **SE2 Script Runtime** in Pulsar's plugin list. A public GitHub repository alone does not automatically make a plugin appear in the list: the descriptor must be registered through the relevant PluginHub/source workflow.

For development, use Pulsar's Modern launcher and a development source folder. See the [client plugin template documentation](https://github.com/CometWorks/client2-plugin-template) for the current workflow.

## Build prerequisites

- Space Engineers 2 installed
- Pulsar
- .NET 10 SDK

Create `Directory.Build.props.user` in the repository root and set `Game2` to the game's `Game2` directory. The file is ignored by Git.

Example:

```xml
<Project>
  <PropertyGroup>
    <Game2>C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers2\Game2</Game2>
  </PropertyGroup>
</Project>
```

Build:

```bash
dotnet build ClientPlugin/ClientPlugin.csproj -c Release
```

## Using the in-game editor

1. Open **SE2 Script Runtime** from the Pulsar plugin list and open its settings.
2. Enter a filename such as `Example.csx`.
3. Select **Load Script** to load an existing file. If it is a new filename, enter source in the editor and use **Save Script** to create it.
4. Select **Check Script** to see compiler errors and warnings.
5. Select **Save and Run Script** to save the editor contents and start the script.
6. Use **Stop Selected Script** to request cancellation.
7. To configure startup scripts, select a filename, choose **Add Selected Script to Auto-start**, and enable **Enable configured auto-start**. Remove entries with **Remove Selected Script from Auto-start**. The list is saved when the settings window closes.
8. Open **Script Management Window** for searchable, sortable multi-selection. Select scripts and use the actions to enable, disable, start, stop, or add/remove them from auto-start. The window shows each script's enabled, disabled, running, and auto-start state; changes save when it closes. The main settings panel also retains the Enable All, Disable All, Start All Enabled, and Stop All actions.

Scripts are stored in the plugin's `Scripts` data folder. The plugin logs the full folder path when it starts.

## Example script

```csharp
Api.Log("Hello from an SE2 script!");
while (!Api.CancellationToken.IsCancellationRequested)
{
    await Api.Delay(TimeSpan.FromSeconds(5));
    Api.Log("Script heartbeat");
}
```

## Roadmap

- Add inline Roslyn diagnostic markers and caret-aware semantic IntelliSense to the editor.
- Better cancellation and execution diagnostics.
- Verified, version-specific SE2 API bridge.
- Programmable Block-style compatibility layer where SE2 supports it.
- CI/build validation against an installed SE2 SDK/game directory.

## License

See [LICENSE](LICENSE).
