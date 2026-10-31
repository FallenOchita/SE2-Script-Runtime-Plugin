using System;
using System.Threading;
using System.Threading.Tasks;
using Keen.VRage.Library.Diagnostics;

namespace SE2ScriptRuntime;

public sealed class ScriptGlobals
{
    private readonly string _scriptName;
    private readonly CancellationToken _cancellationToken;

    public ScriptGlobals(string scriptName, CancellationToken cancellationToken)
    {
        _scriptName = scriptName;
        _cancellationToken = cancellationToken;
    }

    public ScriptApi Api => new ScriptApi(_scriptName, _cancellationToken);
}

public sealed class ScriptApi
{
    private readonly string _scriptName;
    public CancellationToken CancellationToken { get; }

    internal ScriptApi(string scriptName, CancellationToken cancellationToken)
    {
        _scriptName = scriptName;
        CancellationToken = cancellationToken;
    }

    public void Log(string message)
    {
        Log.Default.WriteLine($"[SE2ScriptRuntime:{_scriptName}] {message}");
    }

    public Task Delay(TimeSpan duration)
    {
        return Task.Delay(duration, CancellationToken);
    }

    public void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }
}
