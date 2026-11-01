using System;

namespace SE2ScriptRuntime;

/// <summary>
/// Provides deliberately narrow text insertions for common missing-token diagnostics.
/// These fixes never replace or delete user-authored text.
/// </summary>
public static class ScriptDiagnosticQuickFix
{
    public static bool TryGetInsertion(ScriptDiagnosticInfo diagnostic, out string insertion, out string title)
    {
        insertion = string.Empty;
        title = string.Empty;

        if (diagnostic == null || !string.Equals(diagnostic.Severity, "Error", StringComparison.Ordinal))
            return false;

        switch (diagnostic.Id)
        {
            case "CS1002" when diagnostic.Message.Contains("';' expected", StringComparison.Ordinal):
                insertion = ";";
                title = "Insert missing semicolon";
                return true;

            case "CS1513" when diagnostic.Message.Contains("'}' expected", StringComparison.Ordinal):
                insertion = "}";
                title = "Insert missing closing brace";
                return true;

            case "CS1514" when diagnostic.Message.Contains("'{' expected", StringComparison.Ordinal):
                insertion = "{";
                title = "Insert missing opening brace";
                return true;

            case "CS1026" when diagnostic.Message.Contains("')' expected", StringComparison.Ordinal):
                insertion = ")";
                title = "Insert missing closing parenthesis";
                return true;

            case "CS1003" when diagnostic.Message.Contains("',' expected", StringComparison.Ordinal):
                insertion = ",";
                title = "Insert missing comma";
                return true;

            default:
                return false;
        }
    }
}
