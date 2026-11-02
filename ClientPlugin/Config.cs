using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Text.Json;
using SE2ScriptRuntime.Settings.Elements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SE2ScriptRuntime;

public enum ScriptIndentationStyle
{
    Spaces,
    Tabs,
}

public enum ScriptIndentationSize
{
    Two = 2,
    Four = 4,
    Eight = 8,
}

public sealed class ScriptFormattingProfile
{
    public string Name { get; set; } = string.Empty;
    public ScriptIndentationStyle IndentationStyle { get; set; } = ScriptIndentationStyle.Spaces;
    public ScriptIndentationSize IndentationSize { get; set; } = ScriptIndentationSize.Four;
}


public sealed class Config : INotifyPropertyChanged
{
    private bool autoStartScripts;
    private string autoStartScriptNames = string.Empty;
    private string disabledScriptNames = string.Empty;
    private string selectedScript = "Example.csx";
    private string scriptStatus = "Loading script list...";
    private string editorSource = string.Empty;
    private string editorStatus = "Load a script to begin editing.";
    private string editorDiagnostics = "No diagnostics yet. Select Check Script to compile the editor contents.";
    private string editorDiagnosticsSummary = "Not checked yet.";
    private ScriptDiagnosticInfo[] editorDiagnosticMarkers = Array.Empty<ScriptDiagnosticInfo>();
    private int editorNavigationOffset;
    private int editorNavigationVersion;
    private string selectedCompletion = "Api.Log(\"Your message here\");";
    private ScriptIndentationStyle editorIndentationStyle = ScriptIndentationStyle.Spaces;
    private ScriptIndentationSize editorIndentationSize = ScriptIndentationSize.Four;
    private bool formatOnSave;
    private string editorFormattingPreview = string.Empty;
    private bool hasFormattingPreview;
    private string formattingProfileName = "Spaces (4)";
    private string selectedFormattingProfile = "Spaces (4)";
    private string formattingProfilesImportJson = string.Empty;
    private string formattingProfilesExportJson = string.Empty;

    [XmlIgnore]
    public readonly string Title = "SE2 Script Manager";

    [Separator("Startup")]
    [Checkbox(label: "Enable configured auto-start", description: "When enabled, only scripts listed below start when the plugin loads.")]
    public bool AutoStartScripts { get => autoStartScripts; set => SetField(ref autoStartScripts, value); }


    [MultilineTextbox(label: "Auto-start script list", description: "One .csx filename per line. Changes are saved when you close the settings window.")]
    public string AutoStartScriptNames { get => autoStartScriptNames; set => SetField(ref autoStartScriptNames, value); }

    [Button(label: "Add Selected Script to Auto-start", description: "Adds the selected filename to the startup list without duplicating it.")]
    public void AddSelectedScriptToAutoStart()
    {
        var name = NormalizeScriptName(SelectedScript);
        if (!Plugin.Instance.Scripts.ListScripts().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            ScriptStatus = $"Cannot add {name}: the script does not exist in the Scripts folder.";
            return;
        }
        var names = GetAutoStartScriptNames();
        if (Array.Exists(names, item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
        {
            ScriptStatus = $"{name} is already in the auto-start list.";
            return;
        }
        AutoStartScriptNames = string.Join(Environment.NewLine, names.Concat(new[] { name }));
        ScriptStatus = $"Added {name} to auto-start. Close settings to save changes.";
    }

    [Button(label: "Remove Selected Script from Auto-start", description: "Removes the selected filename from the startup list.")]
    public void RemoveSelectedScriptFromAutoStart()
    {
        var name = NormalizeScriptName(SelectedScript);
        var names = GetAutoStartScriptNames();
        var remaining = names.Where(item => !string.Equals(item, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        AutoStartScriptNames = string.Join(Environment.NewLine, remaining);
        ScriptStatus = remaining.Length == names.Length
            ? $"{name} was not in the auto-start list."
            : $"Removed {name} from auto-start. Close settings to save changes.";
    }

    [Separator("Per-script controls")]
    [MultilineTextbox(label: "Disabled scripts", description: "One .csx filename per line. Disabled scripts cannot be started until enabled again. Saved when settings close.")]
    public string DisabledScriptNames { get => disabledScriptNames; set => SetField(ref disabledScriptNames, value); }

    [Button(label: "Disable Selected Script", description: "Prevents the selected script from launching and requests cancellation if it is running.")]
    public void DisableSelectedScript()
    {
        var name = NormalizeScriptName(SelectedScript);
        if (!Plugin.Instance.Scripts.ListScripts().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            ScriptStatus = $"Cannot disable {name}: the script does not exist in the Scripts folder.";
            return;
        }

        var names = GetDisabledScriptNames();
        if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            DisabledScriptNames = string.Join(Environment.NewLine, names.Concat(new[] { name }));
        }

        var stopRequested = Plugin.Instance.Scripts.Stop(name);
        ScriptStatus = stopRequested
            ? $"Disabled {name}; cancellation requested. It may take time to stop if it does not check cancellation."
            : $"Disabled {name}. It will not start until enabled again.";
        RefreshStatus();
    }

    [Button(label: "Enable Selected Script", description: "Allows the selected script to be started again.")]
    public void EnableSelectedScript()
    {
        var name = NormalizeScriptName(SelectedScript);
        var names = GetDisabledScriptNames();
        var remaining = names.Where(item => !string.Equals(item, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        DisabledScriptNames = string.Join(Environment.NewLine, remaining);
        ScriptStatus = remaining.Length == names.Length
            ? $"{name} is already enabled."
            : $"Enabled {name}. Close settings to save changes.";
        RefreshStatus();
    }

    public string[] GetDisabledScriptNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (DisabledScriptNames ?? string.Empty)
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var name = NormalizeScriptName(entry.Trim());
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
            catch (InvalidOperationException)
            {
                // Ignore malformed entries so a bad config line cannot prevent plugin startup.
            }
        }
        return names.ToArray();
    }

    public bool IsScriptDisabled(string name)
    {
        try
        {
            var normalized = NormalizeScriptName(name);
            return GetDisabledScriptNames().Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public string[] GetAutoStartScriptNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (AutoStartScriptNames ?? string.Empty)
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var name = NormalizeScriptName(entry.Trim());
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
            catch (InvalidOperationException)
            {
                // Ignore malformed entries so a bad config line cannot prevent plugin startup.
            }
        }
        return names.ToArray();
    }

    [Separator("In-game script editor")]
    [Textbox(label: "Script filename", description: "Use a filename such as Example.csx. Files are stored in the plugin Scripts folder.")]
    public string SelectedScript { get => selectedScript; set => SetField(ref selectedScript, value); }

    [CodeEditor(label: "C# script source", description: "C# editor with line numbers and TextMate syntax highlighting. Save before running.")]
    public string EditorSource
    {
        get => editorSource;
        set
        {
            if (SetField(ref editorSource, value))
            {
                ApplyEditorDiagnostics(Array.Empty<ScriptDiagnosticInfo>());
                ClearFormattingPreview();
            }
        }
    }

    [CompletionPicker(label: "Code completion helpers", description: "Choose a host-API member suggestion or code template, then insert it into the editor.")]
    public string SelectedCompletion { get => selectedCompletion; set => SetField(ref selectedCompletion, value); }

    [Button(label: "Insert Selected Completion", description: "Appends the selected code template to the editor source.")]
    public void InsertSelectedCompletion()
    {
        if (string.IsNullOrWhiteSpace(SelectedCompletion)) return;

        var source = EditorSource ?? string.Empty;
        var memberContexts = new[]
        {
            (Pattern: @"Api\.CancellationToken\.[A-Za-z_]\w*$", Prefix: "Api.CancellationToken."),
            (Pattern: @"TimeSpan\.[A-Za-z_]\w*$", Prefix: "TimeSpan."),
            (Pattern: @"Api\.[A-Za-z_]\w*$", Prefix: "Api."),
        };

        foreach (var context in memberContexts)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(source, context.Pattern))
            {
                EditorSource = System.Text.RegularExpressions.Regex.Replace(source, context.Pattern,
                    context.Prefix + SelectedCompletion);
                EditorStatus = $"Inserted {context.Prefix}{SelectedCompletion}. Review it before saving or running.";
                return;
            }
        }

        EditorSource += (string.IsNullOrEmpty(source) ? string.Empty : Environment.NewLine) +
            SelectedCompletion.Replace("\\n", Environment.NewLine);
        EditorStatus = "Inserted code template. Review it before saving or running.";
    }

    [XmlIgnore]
    public ScriptDiagnosticInfo[] EditorDiagnosticMarkers
    {
        get => editorDiagnosticMarkers;
        private set => SetField(ref editorDiagnosticMarkers, value ?? Array.Empty<ScriptDiagnosticInfo>());
    }

    [XmlIgnore]
    public int EditorNavigationOffset
    {
        get => editorNavigationOffset;
        private set => SetField(ref editorNavigationOffset, value);
    }

    [XmlIgnore]
    public int EditorNavigationVersion
    {
        get => editorNavigationVersion;
        private set => SetField(ref editorNavigationVersion, value);
    }

    public void NavigateToDiagnostic(ScriptDiagnosticInfo diagnostic)
    {
        if (diagnostic == null) return;
        EditorNavigationOffset = diagnostic.StartOffset;
        EditorNavigationVersion++;
    }

    public void ApplyDiagnosticQuickFix(ScriptDiagnosticInfo diagnostic)
    {
        if (diagnostic == null || !ScriptDiagnosticQuickFix.TryGetInsertion(diagnostic, out var insertion, out var title))
            return;

        var source = EditorSource ?? string.Empty;
        var offset = Math.Clamp(diagnostic.StartOffset, 0, source.Length);
        EditorSource = source.Insert(offset, insertion);
        EditorStatus = $"Applied quick fix: {title}. Review the change, then run Check Script again.";
        EditorNavigationOffset = Math.Min(offset, EditorSource.Length);
        EditorNavigationVersion++;
    }

    private void ApplyEditorDiagnostics(ScriptDiagnosticInfo[] diagnostics)
    {
        EditorDiagnosticMarkers = diagnostics ?? Array.Empty<ScriptDiagnosticInfo>();
    }

    [Textbox(label: "Diagnostic summary", description: "Counts from the most recent compile check.")]
    public string EditorDiagnosticsSummary { get => editorDiagnosticsSummary; set => SetField(ref editorDiagnosticsSummary, value); }

    public string EditorDiagnostics { get => editorDiagnostics; set => SetField(ref editorDiagnostics, value); }

    [DiagnosticList(label: "Compiler diagnostics", description: "Select a diagnostic to jump to its source location in the editor.")]
    public ScriptDiagnosticInfo[] EditorDiagnosticItemsForDisplay
    {
        get => editorDiagnosticMarkers;
        set => ApplyEditorDiagnostics(value);
    }

    [MultilineTextbox(label: "Editor messages", description: "Load/save results and compiler diagnostics appear here.")]
    public string EditorStatus { get => editorStatus; set => SetField(ref editorStatus, value); }

    [Button(label: "Load Script", description: "Loads the selected script file into the editor.")]
    public void LoadEditorScript()
    {
        try { EditorSource = Plugin.Instance.Scripts.ReadScript(NormalizeScriptName(SelectedScript)); EditorDiagnosticsSummary = "Not checked yet."; EditorDiagnostics = "Loaded file. Run Check Script to refresh diagnostics."; EditorStatus = $"Loaded {NormalizeScriptName(SelectedScript)}."; }
        catch (Exception ex) { EditorStatus = $"Load failed: {ex.Message}"; }
    }

    [Button(label: "Save Script", description: "Saves the editor contents to the selected .csx file.")]
    public void SaveEditorScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            if (FormatOnSave)
            {
                EditorSource = FormatSource(EditorSource);
                EditorNavigationOffset = 0;
                EditorNavigationVersion++;
            }

            Plugin.Instance.Scripts.SaveScript(name, EditorSource);
            EditorStatus = FormatOnSave ? $"Formatted and saved {name}." : $"Saved {name}.";
            EditorDiagnosticsSummary = "Not checked yet.";
            EditorDiagnostics = FormatOnSave
                ? "Formatted and saved. Run Check Script to refresh diagnostics."
                : "Saved. Run Check Script to refresh diagnostics.";
            RefreshStatus();
        }
        catch (Exception ex) { EditorStatus = $"Save failed: {ex.Message}"; }
    }

    [Separator("Editor formatting")]
    [FormattingProfileDropdown(label: "Select formatting profile", description: "Choose a built-in or saved custom profile to apply. The list updates when profiles are saved or deleted.")]
    public string SelectedFormattingProfile
    {
        get => selectedFormattingProfile;
        set
        {
            if (!SetField(ref selectedFormattingProfile, value ?? string.Empty)) return;
            FormattingProfileName = selectedFormattingProfile;
        }
    }

    [Textbox(label: "Custom profile name", description: "Enter a name here when saving or deleting a custom profile.")]
    public string FormattingProfileName
    {
        get => formattingProfileName;
        set => SetField(ref formattingProfileName, value ?? string.Empty);
    }

    public List<ScriptFormattingProfile> FormattingProfiles { get; set; } = new();

    [XmlIgnore]
    [MultilineTextbox(label: "Profile import JSON", description: "Paste a profile export JSON document here. Import validates the entire document before changing saved profiles.")]
    public string FormattingProfilesImportJson
    {
        get => formattingProfilesImportJson;
        set => SetField(ref formattingProfilesImportJson, value ?? string.Empty);
    }

    [XmlIgnore]
    [CodePreview(label: "Profile export JSON", description: "Generate an export below, then copy this read-only JSON to share your custom formatting profiles.")]
    public string FormattingProfilesExportJson
    {
        get => formattingProfilesExportJson;
        private set => SetField(ref formattingProfilesExportJson, value ?? string.Empty);
    }

    private sealed class FormattingProfileTransfer
    {
        public string Name { get; set; } = string.Empty;
        public string IndentationStyle { get; set; } = string.Empty;
        public int IndentationSize { get; set; }
    }

    [Button(label: "Generate Profile Export", description: "Creates a JSON export containing custom profiles only. Built-in presets are never exported.")]
    public void ExportFormattingProfiles()
    {
        try
        {
            var profiles = (FormattingProfiles ?? new List<ScriptFormattingProfile>())
                .Where(profile => profile != null)
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(profile => new FormattingProfileTransfer
                {
                    Name = profile.Name,
                    IndentationStyle = profile.IndentationStyle.ToString(),
                    IndentationSize = (int)profile.IndentationSize,
                })
                .ToArray();

            FormattingProfilesExportJson = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
            EditorStatus = $"Generated JSON export for {profiles.Length} custom formatting profile(s). Copy it from Profile export JSON.";
        }
        catch (Exception ex)
        {
            EditorStatus = $"Profile export failed: {ex.Message}";
        }
    }

    [Button(label: "Import Formatting Profiles", description: "Imports profiles from the JSON field. Existing custom profiles with matching names are updated; built-in names are rejected.")]
    public void ImportFormattingProfiles()
    {
        const int maxJsonLength = 65536;
        const int maxProfileCount = 100;
        var json = FormattingProfilesImportJson ?? string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            EditorStatus = "Paste a profile JSON export before importing.";
            return;
        }
        if (json.Length > maxJsonLength)
        {
            EditorStatus = $"Profile import rejected: JSON exceeds the {maxJsonLength} character limit.";
            return;
        }

        try
        {
            var imported = JsonSerializer.Deserialize<FormattingProfileTransfer[]>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (imported == null || imported.Length == 0)
            {
                EditorStatus = "Profile import contains no profiles.";
                return;
            }
            if (imported.Length > maxProfileCount)
            {
                EditorStatus = $"Profile import rejected: maximum {maxProfileCount} profiles per file.";
                return;
            }

            var validated = new List<ScriptFormattingProfile>(imported.Length);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in imported)
            {
                var name = item?.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) || name.Length > 80
                    || name.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
                    throw new InvalidOperationException("Every profile must have a name of 1–80 characters without line breaks or tabs.");
                if (!names.Add(name))
                    throw new InvalidOperationException($"The import contains duplicate profile name '{name}'.");
                if (GetBuiltInFormattingProfiles().Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"'{name}' is a built-in profile name and cannot be imported.");
                if (!Enum.TryParse<ScriptIndentationStyle>(item.IndentationStyle, true, out var style)
                    || !Enum.IsDefined(typeof(ScriptIndentationStyle), style)
                    || !(string.Equals(item.IndentationStyle, nameof(ScriptIndentationStyle.Spaces), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.IndentationStyle, nameof(ScriptIndentationStyle.Tabs), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Profile '{name}' has an unsupported indentation style.");
                if (!Enum.IsDefined(typeof(ScriptIndentationSize), item.IndentationSize))
                    throw new InvalidOperationException($"Profile '{name}' has an unsupported indentation size.");

                validated.Add(new ScriptFormattingProfile
                {
                    Name = name,
                    IndentationStyle = style,
                    IndentationSize = (ScriptIndentationSize)item.IndentationSize,
                });
            }

            FormattingProfiles ??= new List<ScriptFormattingProfile>();
            var added = 0;
            var updated = 0;
            foreach (var profile in validated)
            {
                var existing = FormattingProfiles.FirstOrDefault(item => item != null
                    && string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    FormattingProfiles.Add(profile);
                    added++;
                }
                else
                {
                    existing.Name = profile.Name;
                    existing.IndentationStyle = profile.IndentationStyle;
                    existing.IndentationSize = profile.IndentationSize;
                    updated++;
                }
            }

            OnPropertyChanged(nameof(FormattingProfiles));
            PersistEditorPreferences();
            EditorStatus = $"Imported {added} new formatting profile(s) and updated {updated} existing profile(s).";
        }
        catch (JsonException ex)
        {
            EditorStatus = $"Profile import failed: invalid JSON. {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            EditorStatus = $"Profile import rejected: {ex.Message}";
        }
    }

    public string[] GetFormattingProfileNames()
    {
        return GetBuiltInFormattingProfiles().Select(profile => profile.Name)
            .Concat((FormattingProfiles ?? new List<ScriptFormattingProfile>())
                .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.Name))
                .Select(profile => profile.Name.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [Button(label: "Apply Formatting Profile", description: "Applies a built-in preset or a saved custom profile.")]
    public void ApplyFormattingProfile()
    {
        var name = (SelectedFormattingProfile ?? string.Empty).Trim();
        var profile = GetBuiltInFormattingProfiles().Concat(FormattingProfiles ?? new List<ScriptFormattingProfile>())
            .FirstOrDefault(item => item != null && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            EditorStatus = $"Formatting profile '{name}' was not found. Built-ins: Spaces (4), Spaces (2), Tabs.";
            return;
        }

        if (!Enum.IsDefined(typeof(ScriptIndentationStyle), profile.IndentationStyle)
            || !Enum.IsDefined(typeof(ScriptIndentationSize), profile.IndentationSize))
        {
            EditorStatus = $"Formatting profile '{name}' contains unsupported indentation settings.";
            return;
        }

        var changed = SetField(ref editorIndentationStyle, profile.IndentationStyle, nameof(ScriptIndentationStyle));
        changed |= SetField(ref editorIndentationSize, profile.IndentationSize, nameof(ScriptIndentationSize));
        ClearFormattingPreview();
        if (changed) PersistEditorPreferences();
        EditorStatus = $"Applied formatting profile '{profile.Name}'.";
    }

    [Button(label: "Save Current as Profile", description: "Saves the current indentation settings under the entered custom profile name.")]
    public void SaveFormattingProfile()
    {
        var name = (FormattingProfileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            EditorStatus = "Enter a name before saving a formatting profile.";
            return;
        }
        if (GetBuiltInFormattingProfiles().Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            EditorStatus = "That name is reserved for a built-in profile. Choose a different custom name.";
            return;
        }

        FormattingProfiles ??= new List<ScriptFormattingProfile>();
        var existing = FormattingProfiles.FirstOrDefault(item => item != null && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            FormattingProfiles.Add(new ScriptFormattingProfile
            {
                Name = name,
                IndentationStyle = ScriptIndentationStyle,
                IndentationSize = ScriptIndentationSize,
            });
        }
        else
        {
            existing.IndentationStyle = ScriptIndentationStyle;
            existing.IndentationSize = ScriptIndentationSize;
        }

        OnPropertyChanged(nameof(FormattingProfiles));
        SelectedFormattingProfile = name;
        PersistEditorPreferences();
        EditorStatus = $"Saved custom formatting profile '{name}'.";
    }

    [Button(label: "Duplicate Selected Profile", description: "Copies the selected built-in or custom profile into your custom profiles. Enter a destination name first, or leave it blank to generate a copy name.")]
    public void DuplicateFormattingProfile()
    {
        var sourceName = (SelectedFormattingProfile ?? string.Empty).Trim();
        var source = GetBuiltInFormattingProfiles().Concat(FormattingProfiles ?? new List<ScriptFormattingProfile>())
            .FirstOrDefault(profile => profile != null && string.Equals(profile.Name, sourceName, StringComparison.OrdinalIgnoreCase));
        if (source == null)
        {
            EditorStatus = "Select an existing formatting profile to duplicate.";
            return;
        }

        var targetName = (FormattingProfileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            var baseName = $"Copy of {source.Name}";
            targetName = baseName;
            var suffix = 2;
            while (GetFormattingProfileNames().Contains(targetName, StringComparer.OrdinalIgnoreCase))
                targetName = $"{baseName} ({suffix++})";
        }

        if (!IsValidCustomFormattingProfileName(targetName, out var error))
        {
            EditorStatus = error;
            return;
        }
        if (GetFormattingProfileNames().Contains(targetName, StringComparer.OrdinalIgnoreCase))
        {
            EditorStatus = $"A formatting profile named '{targetName}' already exists. Choose a different name.";
            return;
        }

        FormattingProfiles ??= new List<ScriptFormattingProfile>();
        FormattingProfiles.Add(new ScriptFormattingProfile
        {
            Name = targetName,
            IndentationStyle = source.IndentationStyle,
            IndentationSize = source.IndentationSize,
        });
        OnPropertyChanged(nameof(FormattingProfiles));
        SelectedFormattingProfile = targetName;
        PersistEditorPreferences();
        EditorStatus = $"Duplicated '{source.Name}' as custom profile '{targetName}'.";
    }

    [Button(label: "Rename Selected Custom Profile", description: "Renames the selected custom profile using the Custom profile name field. Built-in profiles cannot be renamed.")]
    public void RenameFormattingProfile()
    {
        var sourceName = (SelectedFormattingProfile ?? string.Empty).Trim();
        var targetName = (FormattingProfileName ?? string.Empty).Trim();
        var source = (FormattingProfiles ?? new List<ScriptFormattingProfile>())
            .FirstOrDefault(profile => profile != null && string.Equals(profile.Name, sourceName, StringComparison.OrdinalIgnoreCase));
        if (source == null)
        {
            EditorStatus = "Select a custom profile to rename. Built-in profiles cannot be renamed.";
            return;
        }
        if (!IsValidCustomFormattingProfileName(targetName, out var error))
        {
            EditorStatus = error;
            return;
        }
        if (GetBuiltInFormattingProfiles().Any(profile => string.Equals(profile.Name, targetName, StringComparison.OrdinalIgnoreCase)))
        {
            EditorStatus = $"'{targetName}' is reserved for a built-in profile.";
            return;
        }
        if (!string.Equals(sourceName, targetName, StringComparison.OrdinalIgnoreCase)
            && GetFormattingProfileNames().Contains(targetName, StringComparer.OrdinalIgnoreCase))
        {
            EditorStatus = $"A formatting profile named '{targetName}' already exists.";
            return;
        }

        source.Name = targetName;
        OnPropertyChanged(nameof(FormattingProfiles));
        SelectedFormattingProfile = targetName;
        PersistEditorPreferences();
        EditorStatus = $"Renamed custom formatting profile to '{targetName}'.";
    }

    private static bool IsValidCustomFormattingProfileName(string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80
            || name.IndexOfAny(new[] { '\\r', '\\n', '\\t' }) >= 0)
        {
            error = "Enter a profile name of 1–80 characters without line breaks or tabs.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    [Button(label: "Delete Custom Profile", description: "Deletes the custom profile matching the entered name. Built-in profiles cannot be deleted.")]
    public void DeleteFormattingProfile()
    {
        var name = (FormattingProfileName ?? string.Empty).Trim();
        var removed = (FormattingProfiles ?? new List<ScriptFormattingProfile>())
            .RemoveAll(item => item != null && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            EditorStatus = $"No custom formatting profile named '{name}' was found. Built-in profiles cannot be deleted.";
            return;
        }

        OnPropertyChanged(nameof(FormattingProfiles));
        if (string.Equals(SelectedFormattingProfile, name, StringComparison.OrdinalIgnoreCase))
            SelectedFormattingProfile = "Spaces (4)";
        PersistEditorPreferences();
        EditorStatus = $"Deleted custom formatting profile '{name}'.";
    }

    private static IEnumerable<ScriptFormattingProfile> GetBuiltInFormattingProfiles()
    {
        yield return new ScriptFormattingProfile { Name = "Spaces (4)", IndentationStyle = ScriptIndentationStyle.Spaces, IndentationSize = ScriptIndentationSize.Four };
        yield return new ScriptFormattingProfile { Name = "Spaces (2)", IndentationStyle = ScriptIndentationStyle.Spaces, IndentationSize = ScriptIndentationSize.Two };
        yield return new ScriptFormattingProfile { Name = "Tabs", IndentationStyle = ScriptIndentationStyle.Tabs, IndentationSize = ScriptIndentationSize.Four };
    }

    [Dropdown(label: "Indentation style", description: "Choose spaces or tab characters for each indentation level.")]
    public ScriptIndentationStyle ScriptIndentationStyle
    {
        get => editorIndentationStyle;
        set
        {
            if (!SetField(ref editorIndentationStyle, value)) return;
            ClearFormattingPreview();
            PersistEditorPreferences();
        }
    }

    [Dropdown(label: "Indentation width", description: "Choose the number of spaces per indentation level. This also controls the visual width of tabs in the formatter output.")]
    public ScriptIndentationSize ScriptIndentationSize
    {
        get => editorIndentationSize;
        set
        {
            if (!SetField(ref editorIndentationSize, value)) return;
            ClearFormattingPreview();
            PersistEditorPreferences();
        }
    }

    [Checkbox(label: "Format on save", description: "Automatically formats the entire editor before Save Script and Save and Run Script. Disabled by default.")]
    public bool FormatOnSave
    {
        get => formatOnSave;
        set
        {
            if (!SetField(ref formatOnSave, value)) return;
            PersistEditorPreferences();
        }
    }

    [XmlIgnore]
    [CodePreview(label: "Formatting preview", description: "Read-only preview. Generating a preview does not modify the editor source or save the script.")]
    public string EditorFormattingPreview
    {
        get => editorFormattingPreview;
        private set => SetField(ref editorFormattingPreview, value ?? string.Empty);
    }

    [Button(label: "Preview Formatting", description: "Shows how the current editor source will look with the selected formatting preferences, without changing the source.")]
    public void PreviewEditorFormatting()
    {
        try
        {
            EditorFormattingPreview = FormatSource(EditorSource);
            hasFormattingPreview = true;
            EditorStatus = "Formatting preview generated. The editor source has not been changed. Select Apply Formatting Preview to use it.";
        }
        catch (Exception ex)
        {
            ClearFormattingPreview();
            EditorStatus = $"Formatting preview failed: {ex.Message}";
        }
    }

    [Button(label: "Apply Formatting Preview", description: "Replaces the editor source with the most recently generated preview. Generate a new preview after changing source or formatting preferences.")]
    public void ApplyFormattingPreview()
    {
        if (!hasFormattingPreview)
        {
            EditorStatus = "No current formatting preview. Select Preview Formatting first.";
            return;
        }

        var preview = EditorFormattingPreview;
        hasFormattingPreview = false;
        EditorSource = preview;
        EditorDiagnosticsSummary = "Not checked yet.";
        EditorDiagnostics = "Formatting preview applied. Run Check Script to refresh compiler diagnostics.";
        EditorStatus = "Applied formatting preview to the editor. Review the changes before saving or running.";
        EditorNavigationOffset = 0;
        EditorNavigationVersion++;
    }

    private void ClearFormattingPreview()
    {
        hasFormattingPreview = false;
        if (!string.IsNullOrEmpty(editorFormattingPreview))
            EditorFormattingPreview = string.Empty;
    }

    private void PersistEditorPreferences()
    {
        if (!ReferenceEquals(Config.Current, this) || Plugin.Instance == null)
            return;

        try
        {
            ConfigStorage.Save(this);
            EditorStatus = "Editor formatting preferences saved to Config.xml.";
        }
        catch (Exception ex)
        {
            EditorStatus = $"Could not save editor preferences: {ex.Message}";
        }
    }

    private string FormatSource(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithKind(SourceCodeKind.Script);
        var syntaxTree = CSharpSyntaxTree.ParseText(source ?? string.Empty, parseOptions);
        var root = syntaxTree.GetRoot();
        var indentation = ScriptIndentationStyle == ScriptIndentationStyle.Tabs
            ? "\t"
            : new string(' ', (int)ScriptIndentationSize);
        return root.NormalizeWhitespace(indentation, Environment.NewLine, elasticTrivia: false).ToFullString();
    }

    [Button(label: "Format Editor Source", description: "Formats the entire editor using the selected indentation style and width. Review the result, then run Check Script.")]
    public void FormatEditorSource()
    {
        try
        {
            EditorSource = FormatSource(EditorSource);
            EditorDiagnosticsSummary = "Not checked yet.";
            EditorDiagnostics = "Source formatted. Run Check Script to refresh compiler diagnostics.";
            EditorStatus = $"Formatted the entire editor source using {ScriptIndentationStyle.ToString().ToLowerInvariant()} with width {(int)ScriptIndentationSize}. Review the changes before saving or running.";
            EditorNavigationOffset = 0;
            EditorNavigationVersion++;
        }
        catch (Exception ex)
        {
            EditorStatus = $"Formatting failed: {ex.Message}";
        }
    }

    [Button(label: "Check Script", description: "Compiles the editor contents and displays errors and warnings without running it.")]
    public void CheckEditorScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            var details = Plugin.Instance.Scripts.ValidateDetailed(EditorSource);
            var diagnostics = details.Select(d => d.ToString()).ToArray();
            ApplyEditorDiagnostics(details);
            UpdateDiagnosticSummary(diagnostics);
            EditorDiagnostics = diagnostics.Length == 0 ? $"{name}: no compiler errors or warnings." : string.Join("\n", diagnostics);
            EditorStatus = $"Check complete for {name}. {diagnostics.Length} diagnostic(s).";
        }
        catch (Exception ex) { EditorStatus = $"Check failed: {ex.Message}"; }
    }

    [Button(label: "Save and Run Script", description: "Saves the current editor contents, then starts that script.")]
    public void SaveAndRunEditorScript()
    {
        try
        {
            var name = NormalizeScriptName(SelectedScript);
            if (FormatOnSave)
            {
                EditorSource = FormatSource(EditorSource);
                EditorNavigationOffset = 0;
                EditorNavigationVersion++;
            }

            var details = Plugin.Instance.Scripts.ValidateDetailed(EditorSource);
            var diagnostics = details.Select(d => d.ToString()).ToArray();
            ApplyEditorDiagnostics(details);
            UpdateDiagnosticSummary(diagnostics);
            EditorDiagnostics = diagnostics.Length == 0 ? $"{name}: no compiler errors or warnings." : string.Join("\n", diagnostics);
            var errors = CountDiagnostics(diagnostics, "Error ");
            if (errors > 0)
            {
                EditorStatus = $"Run blocked: {errors} compiler error(s). Fix the errors and try again.";
                return;
            }

            Plugin.Instance.Scripts.SaveScript(name, EditorSource);
            var started = Plugin.Instance.Scripts.Start(name);
            var warnings = CountDiagnostics(diagnostics, "Warning ");
            EditorStatus = started
                ? warnings > 0 ? $"Saved and started {name} with {warnings} compiler warning(s)." : $"Saved and started {name}."
                : Config.Current.IsScriptDisabled(name) ? $"Run blocked: {name} is disabled. Enable it in Script controls first." : $"{name} is already running or could not be started.";
            RefreshStatus();
        }
        catch (Exception ex) { EditorStatus = $"Run failed: {ex.Message}"; }
    }


    [Button(label: "Insert Log Snippet", description: "Adds an Api.Log call at the end of the editor contents.")]
    public void InsertLogSnippet() => EditorSource += (string.IsNullOrEmpty(EditorSource) ? string.Empty : Environment.NewLine) + "Api.Log(\"Your message here\");";

    [Button(label: "Insert Cancellation Loop", description: "Adds a cooperative loop template that can be stopped by the manager.")]
    public void InsertCancellationLoop() => EditorSource += (string.IsNullOrEmpty(EditorSource) ? string.Empty : Environment.NewLine) +
        "while (!Api.CancellationToken.IsCancellationRequested)\n{\n    await Api.Delay(TimeSpan.FromSeconds(1));\n    Api.Log(\"Running\");\n}";

    [Separator("Script management")]
    [Button(label: "Open Script Management Window", description: "Opens a searchable, sortable list with multi-select enable, disable, start, stop, and auto-start actions.")]
    public void OpenScriptManagementWindow() => Plugin.Instance.OpenScriptManagementDialog();

    [Button(label: "Refresh Script List", description: "Rescan the Scripts folder and update runtime status.")]
    public void RefreshScriptList() => RefreshStatus();

    [Button(label: "Start Selected Script", description: "Starts the selected .csx script from disk.")]
    public void StartSelectedScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); var started = Plugin.Instance.Scripts.Start(name); ScriptStatus = started ? $"Started: {name}" : Config.Current.IsScriptDisabled(name) ? $"Start blocked: {name} is disabled. Enable it first." : $"Could not start {name}. It may already be running or the file does not exist."; RefreshStatus(); }
        catch (Exception ex) { ScriptStatus = $"Start failed: {ex.Message}"; }
    }

    [Button(label: "Stop Selected Script", description: "Requests cancellation of the selected script.")]
    public void StopSelectedScript()
    {
        try { var name = NormalizeScriptName(SelectedScript); var stopped = Plugin.Instance.Scripts.Stop(name); ScriptStatus = stopped ? $"Stop requested: {name}" : $"Not running: {name}"; RefreshStatus(); }
        catch (Exception ex) { ScriptStatus = $"Stop failed: {ex.Message}"; }
    }

    [Button(label: "Enable All Scripts", description: "Clears the disabled list so every script can be started again.")]
    public void EnableAllScripts()
    {
        var previous = GetDisabledScriptNames();
        DisabledScriptNames = string.Empty;
        ScriptStatus = previous.Length == 0
            ? "All scripts were already enabled."
            : $"Enabled all scripts. Cleared {previous.Length} disabled entry/entries. Close settings to save changes.";
        RefreshStatus();
    }

    [Button(label: "Disable All Scripts", description: "Disables every script currently in the Scripts folder and requests cancellation of running scripts.")]
    public void DisableAllScripts()
    {
        var files = Plugin.Instance.Scripts.ListScripts();
        DisabledScriptNames = string.Join(Environment.NewLine, files);
        var stopped = Plugin.Instance.Scripts.StopAll();
        ScriptStatus = $"Disabled {files.Length} script(s). Cancellation requested for {stopped} running script(s). Close settings to save changes.";
        RefreshStatus();
    }

    [Button(label: "Start All Enabled Scripts", description: "Starts scripts that are not disabled. Compiler errors still block launch.")]
    public void StartAllScripts() { var count = Plugin.Instance.Scripts.StartAll(); ScriptStatus = $"Start requested for {count} enabled script(s). Disabled scripts were skipped."; RefreshStatus(); }

    [Button(label: "Stop All Scripts", description: "Requests cancellation of every running script.")]
    public void StopAllScripts() { var count = Plugin.Instance.Scripts.StopAll(); ScriptStatus = $"Stop requested for {count} script(s)."; RefreshStatus(); }

    private void UpdateDiagnosticSummary(string[] diagnostics)
    {
        var errors = CountDiagnostics(diagnostics, "Error ");
        var warnings = CountDiagnostics(diagnostics, "Warning ");
        EditorDiagnosticsSummary = $"Errors: {errors} | Warnings: {warnings}";
    }

    private static int CountDiagnostics(string[] diagnostics, string prefix)
        => Array.FindAll(diagnostics, item => item.StartsWith(prefix, StringComparison.Ordinal)).Length;

    public void RefreshStatus()
    {
        if (Plugin.Instance?.Scripts == null) return;
        var files = Plugin.Instance.Scripts.ListScripts();
        var running = Plugin.Instance.Scripts.RunningScripts;
        var disabled = GetDisabledScriptNames();
        var statusLines = files.Select(file =>
        {
            var state = disabled.Contains(file, StringComparer.OrdinalIgnoreCase)
                ? "DISABLED"
                : running.Contains(file, StringComparer.OrdinalIgnoreCase) ? "RUNNING" : "ENABLED / STOPPED";
            return $"  [{state}] {file}";
        });
        var disabledFileCount = files.Count(file => disabled.Contains(file, StringComparer.OrdinalIgnoreCase));
        var enabledCount = files.Length - disabledFileCount;
        ScriptStatus = $"Scripts folder: {Plugin.Instance.Scripts.ScriptDirectory}\nSummary: {files.Length} total | {enabledCount} enabled | {disabledFileCount} disabled | {running.Length} running\nScript status:\n{string.Join("\n", statusLines)}\n\nRunning ({running.Length}): {string.Join(", ", running)}\nDisabled ({disabled.Length} configured): {string.Join(", ", disabled)}\n\nRecent output:\n{string.Join("\n", ScriptLogger.Snapshot(30))}";
    }

    private static string NormalizeScriptName(string value)
    {
        var name = System.IO.Path.GetFileName(value?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enter a script filename.");
        if (!name.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)) name += ".csx";
        if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException("The script filename contains invalid characters.");
        return name;
    }

    public static Config Default => new Config();
    public static Config Current { get; set; }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
}