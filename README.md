# SE2 Script Runtime

An in-game C# scripting runtime plugin for Space Engineers 2, designed to load through Pulsar.

## Current status

**Early development / developer preview.** The repository provides a Pulsar client-plugin project, a Roslyn-based C# script host, script file management, compiler diagnostics, logging, cancellation, and an in-game C# editor. It has not yet been built or validated against a live SE2 installation in this development session.

## Current features

- Creates a per-plugin data directory and a `Scripts` folder.
- Lists `.csx` scripts and supports starting/stopping one or all scripts. Every launch path (individual start, Start All, and auto-start) compiles the script first and blocks launch on compiler errors; warnings are logged.
- In-game editor controls to load, edit, save, check, and run a selected script.
- One-click whole-document C# formatting using Roslyn syntax normalization, with configurable spaces or tabs and 2-, 4-, or 8-character indentation width. Built-in formatting profiles include **Spaces (4)**, **Spaces (2)**, and **Tabs**; custom profiles can be saved, applied, and deleted by name. **Preview Formatting** displays a read-only preview, and **Apply Formatting Preview** copies that preview into the editor without saving the script. An optional **Format on save** setting (off by default) applies the same formatter before **Save Script** and **Save and Run Script**. Formatting clears stale diagnostics; run **Check Script** afterward. Formatting can change whitespace throughout the file, so review before saving or running.
- Roslyn compiler errors and warnings with line and column locations. The **Save and Run Script** action checks the current editor contents first and blocks launch when compiler errors are present; warnings are shown but do not block launch. The runtime repeats this validation for scripts launched from disk, including Start All and auto-start, so those paths cannot bypass the compile gate.
- Per-script auto-start list, disabled by default. Add or remove the selected `.csx` file from the list; when enabled, only listed scripts are launched on plugin startup. The settings dialog saves the list to `Config.xml` when closed.
- Script management controls for enabling/disabling one or all scripts, starting all enabled scripts, and stopping all running scripts. Disabled scripts are blocked from individual starts, bulk starts, editor Save and Run, and auto-start. Disabling running scripts requests cooperative cancellation; they may not stop promptly if they do not observe their cancellation token. The settings dialog saves the disabled list when closed.
- Dedicated script-management window with filename search, name/status sorting, multi-selection, and selected-script enable, disable, start, stop, and auto-start actions. Runtime state, auto-start labels, and summary counts refresh every two seconds while the window is open. Changes to startup and disabled lists are saved when the window closes.
- Per-script status display showing enabled/stopped, running, or disabled state.
- Script logging and a bounded recent-output buffer.
- Small host API for logging, delays, cancellation checks, and script name.

The editor uses AvaloniaEdit with line numbers, word wrapping, and TextMate C# syntax highlighting directly in the editable surface. Compiler diagnostics are shown in their own panel with severity, line, and column; after **Check Script** or **Save and Run Script**, Roslyn error and warning spans are also highlighted inline in the editor (red for errors, amber for warnings). The compiler diagnostics list is clickable: selecting an entry scrolls the editor to its source line and places the caret at the reported offset. Hovering over a highlighted diagnostic span in the editor opens a tooltip with its severity, Roslyn ID, line, column, and full message. For a small set of common missing-token errors, the list also offers conservative insertion quick fixes (semicolon, braces, closing parenthesis, and comma); each fix only inserts the suggested token and leaves the rest of the source untouched. Applying a fix clears the stale diagnostics so you can run Check Script again. The completion picker now changes its candidates when the source ends in `Api.`, `Api.CancellationToken.`, or `TimeSpan.`; it filters members by a partially typed identifier and replaces that unfinished member access when **Insert Selected Completion** is used. Outside those recognized contexts, it offers the existing code templates. This is a limited host-API completion helper, not full Roslyn semantic IntelliSense: it does not track the caret inside the buffer, resolve arbitrary symbols, or provide caret-aware semantic IntelliSense. Inline compiler diagnostics use source-span background highlights, hover tooltips, and clickable line navigation; they are not a full IDE-style squiggle, code-action, or refactoring system; quick fixes are limited to the explicit missing-token cases listed above. The editor's syntax colors come from the bundled TextMate C# grammar; compiler diagnostics continue to come from Roslyn.

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
5. Under **Editor formatting**, choose spaces or tabs and an indentation width of 2, 4, or 8. Enter **Spaces (4)**, **Spaces (2)**, or **Tabs** in Formatting profile name and select **Apply Formatting Profile** for a preset. To create a custom profile, set the indentation options, enter a name, and select **Save Current as Profile**; enter a saved custom name and select **Delete Custom Profile** to remove it. Select **Preview Formatting** to inspect the result without changing the editor, then **Apply Formatting Preview** to use it; alternatively, **Format Editor Source** applies formatting immediately. Run **Check Script** to refresh diagnostics. Optionally enable **Format on save** to apply the formatter automatically before saving or saving and running; it is off by default. Editor formatting preferences are saved to Config.xml immediately when changed.
6. Select **Save and Run Script** to save the editor contents and start the script.
7. Use **Stop Selected Script** to request cancellation.
8. To configure startup scripts, select a filename, choose **Add Selected Script to Auto-start**, and enable **Enable configured auto-start**. Remove entries with **Remove Selected Script from Auto-start**. The list is saved when the settings window closes.
9. Open **Script Management Window** for searchable, sortable multi-selection. Select scripts and use the actions to enable, disable, start, stop, or add/remove them from auto-start. The window shows each script's enabled, disabled, running, and auto-start state; changes save when it closes. The main settings panel also retains the Enable All, Disable All, Start All Enabled, and Stop All actions.

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

- Add caret-aware semantic IntelliSense and richer diagnostic navigation/quick fixes to the editor.
- Better cancellation and execution diagnostics.
- Verified, version-specific SE2 API bridge.
- Programmable Block-style compatibility layer where SE2 supports it.
- CI/build validation against an installed SE2 SDK/game directory.

## License

See [LICENSE](LICENSE).
