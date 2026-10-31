# SE2 Script Runtime

An in-game C# scripting runtime plugin for Space Engineers 2, designed to be loaded and listed by Pulsar.

## Current status

**Early development / developer preview.** This repository establishes the Pulsar client-plugin project, script host, script directory, logging, cancellation, and an example script. It is not yet a complete Programmable Block replacement and has not been validated against every SE2 game build.

## What it does now

- Loads as an SE2 client plugin through Pulsar.
- Creates a per-plugin data directory and a `Scripts` folder.
- Finds `.csx` scripts and runs them when the plugin starts.
- Provides scripts with a small host API for logging, delays, cancellation checks, and their script name.
- Tracks running scripts and requests cancellation when the runtime shuts down.
- Writes runtime output to the Pulsar/game log.

## Important security note

C# scripts execute with the permissions of the game process. **They are trusted code, not sandboxed code.** Only run scripts you wrote or have reviewed. A cancellation request is cooperative: a script that never awaits or checks its cancellation token may not stop promptly. Do not use this runtime to run unknown scripts.

The initial host deliberately does not expose SE2 world, grid, block, or player objects. Those APIs must be bridged only after their current public interfaces have been verified against the installed game assemblies. This avoids pretending that an unsupported Programmable Block API already exists.

## Installation / Pulsar listing

This repository includes `SE2ScriptRuntime.xml`, a PluginHub/Pulsar source registration descriptor. Add the repository as a Pulsar source/development folder using that descriptor, then enable **SE2 Script Runtime** in Pulsar's plugin list. A public GitHub repository alone does not automatically make a plugin appear in the list: the descriptor must be registered through the relevant PluginHub/source workflow.

For development, use Pulsar's Modern launcher and a development source folder. See the [client plugin template documentation](https://github.com/CometWorks/client2-plugin-template) for the current workflow.

## Build prerequisites

- Space Engineers 2 installed
- Pulsar
- .NET 10 SDK
- Windows or Linux development environment

Create `Directory.Build.props.user` in the repository root and set `Game2` to the game's `Game2` directory. The file is ignored by Git.

Example:

```xml
<Project>
  <PropertyGroup>
    <Game2>C:\\Program Files (x86)\\Steam\\steamapps\\common\\SpaceEngineers2\\Game2</Game2>
  </PropertyGroup>
</Project>
```

Build:

```bash
dotnet build ClientPlugin/ClientPlugin.csproj -c Release
```

## Writing a script

Put a `.csx` file in the runtime's `Scripts` folder. For example:

```csharp
Api.Log("Hello from an SE2 script!");
while (!Api.CancellationToken.IsCancellationRequested)
{
    await Api.Delay(TimeSpan.FromSeconds(5));
    Api.Log("Script heartbeat");
}
```

The data folder is supplied by Pulsar through its plugin configuration path. The plugin logs the exact Scripts directory when it starts.

## Roadmap

- In-game script manager and editor UI (list, start, stop, reload, output).
- Per-script enable/disable and persisted settings.
- Better cancellation and execution diagnostics.
- Verified, version-specific SE2 API bridge.
- Programmable Block-style compatibility layer where SE2 supports it.
- CI/build validation against an installed SE2 SDK/game directory.

## License

See [LICENSE](LICENSE).
