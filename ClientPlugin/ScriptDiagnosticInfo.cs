using System;

namespace SE2ScriptRuntime;

/// <summary>
/// A compiler diagnostic with a source span suitable for editor annotations.
/// </summary>
public sealed class ScriptDiagnosticInfo
{
    public string Severity { get; }
    public string Id { get; }
    public int StartOffset { get; }
    public int Length { get; }
    public int Line { get; }
    public int Column { get; }
    public string Message { get; }

    public ScriptDiagnosticInfo(string severity, string id, int startOffset, int length, int line, int column, string message)
    {
        Severity = severity ?? "Info";
        Id = id ?? string.Empty;
        StartOffset = Math.Max(0, startOffset);
        Length = Math.Max(0, length);
        Line = Math.Max(1, line);
        Column = Math.Max(1, column);
        Message = message ?? string.Empty;
    }

    public override string ToString()
        => $"{Severity} {Id} (line {Line}, column {Column}): {Message}";
}
