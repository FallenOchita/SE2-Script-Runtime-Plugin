# SE2 Script Runtime

An in-game C# scripting runtime plugin for Space Engineers 2, designed to load through Pulsar.

## Current status

**Early development / developer preview.** The repository provides a Pulsar client-plugin project, a Roslyn-based C# script host, script file management, compiler diagnostics, logging, cancellation, and an in-game plain-text editor. It has not yet been built or validated against a live SE2 installation in this development session.

## Current features

- Creates a per-plugin data directory and a `Scripts` folder.
- Lists `.csx` scripts and supports starting/stopping one or all scripts.
- In-game editor controls to load, edit, save, check, and run a selected script.
- Roslyn compiler errors and warnings with line and column locations.
- Optional auto-start, disabled by default.
- Script logging and a bounded recent-output buffer.
- Small host API for logging, delays, cancellation checks, and script name.

The editor is currently a plain multiline text editor. Syntax highlighting, autocomplete, and inline error underlining are not implemented yet.

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

- Syntax highlighting, autocomplete, and inline diagnostics.
- Per-script enable/disable and persisted startup settings.
- Better cancellation and execution diagnostics.
- Verified, version-specific SE2 API bridge.
- Programmable Block-style compatibility layer where SE2 supports it.
- CI/build validation against an installed SE2 SDK/game directory.

## License

See [LICENSE](LICENSE).
