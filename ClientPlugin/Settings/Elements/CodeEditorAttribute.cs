using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace SE2ScriptRuntime.Settings.Elements;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class CodeEditorAttribute : Attribute, IElement
{
    private static bool _stylesRegistered;

    public readonly string Label;
    public readonly string Description;

    public CodeEditorAttribute(string label = null, string description = null)
    {
        Label = label;
        Description = description;
    }

    public Control BuildRow(string name, Func<object> getter, Action<object> setter)
    {
        RegisterEditorStyles();

        var editor = new TextEditor
        {
            Text = getter()?.ToString() ?? string.Empty,
            Width = 420,
            Height = 320,
            ShowLineNumbers = true,
            WordWrap = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
        };

        var diagnosticTransformer = new RoslynDiagnosticTransformer(
            () => Config.Current?.EditorDiagnosticMarkers ?? Array.Empty<ScriptDiagnosticInfo>());
        editor.TextArea.TextView.LineTransformers.Add(diagnosticTransformer);

        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMate = editor.InstallTextMate(registryOptions);
        var csharp = registryOptions.GetLanguageByExtension(".cs");
        if (csharp != null)
            textMate.SetGrammar(registryOptions.GetScopeByLanguageId(csharp.Id));

        editor.TextChanged += (_, _) => setter(editor.Text);

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == name)
            {
                var current = getter()?.ToString() ?? string.Empty;
                if (!string.Equals(editor.Text, current, StringComparison.Ordinal))
                    editor.Text = current;
            }
            else if (args.PropertyName == nameof(Config.EditorDiagnosticMarkers))
            {
                editor.TextArea.TextView.Redraw();
            }
        };
        Config.Current.PropertyChanged += handler;
        editor.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= handler;

        return RowBuilder.NewRow(
            Tools.Tools.GetLabelOrDefault(name, Label),
            Description,
            editor);
    }

    private static void RegisterEditorStyles()
    {
        if (_stylesRegistered || Application.Current == null)
            return;

        var source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml");
        var existing = Application.Current.Styles
            .OfType<StyleInclude>()
            .Any(style => style.Source == source);

        if (!existing)
        {
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://AvaloniaEdit/"))
            {
                Source = source,
            });
        }

        _stylesRegistered = true;
    }

    private sealed class RoslynDiagnosticTransformer : DocumentColorizingTransformer
    {
        private readonly Func<ScriptDiagnosticInfo[]> _getDiagnostics;

        public RoslynDiagnosticTransformer(Func<ScriptDiagnosticInfo[]> getDiagnostics)
            => _getDiagnostics = getDiagnostics;

        protected override void ColorizeLine(DocumentLine line)
        {
            var diagnostics = _getDiagnostics();
            if (diagnostics == null || diagnostics.Length == 0 || line.Length == 0)
                return;

            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;
            foreach (var diagnostic in diagnostics)
            {
                var spanStart = diagnostic.StartOffset;
                var spanEnd = spanStart + Math.Max(1, diagnostic.Length);
                var start = Math.Max(lineStart, spanStart);
                var end = Math.Min(lineEnd, spanEnd);
                if (start >= end)
                    continue;

                var color = string.Equals(diagnostic.Severity, "Error", StringComparison.Ordinal)
                    ? Color.FromArgb(72, 235, 70, 70)
                    : Color.FromArgb(64, 235, 180, 55);
                var brush = new SolidColorBrush(color);
                ChangeLinePart(start, end, element => element.BackgroundBrush = brush);
            }
        }
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}
