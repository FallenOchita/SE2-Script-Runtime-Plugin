using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

        var textView = editor.TextArea.TextView;
        var diagnosticTransformer = new RoslynDiagnosticTransformer(
            () => Config.Current?.EditorDiagnosticMarkers ?? Array.Empty<ScriptDiagnosticInfo>());
        textView.LineTransformers.Add(diagnosticTransformer);

        void CloseDiagnosticTooltip()
        {
            ToolTip.SetIsOpen(textView, false);
            ToolTip.SetTip(textView, null);
        }

        void OnPointerHover(object sender, PointerEventArgs args)
        {
            var visualPosition = args.GetPosition(textView) + textView.ScrollOffset;
            var textPosition = textView.GetPosition(visualPosition);
            if (!textPosition.HasValue)
            {
                CloseDiagnosticTooltip();
                return;
            }

            var line = editor.Document.GetLineByNumber(textPosition.Value.Line);
            var offset = Math.Clamp(line.Offset + textPosition.Value.Column - 1, line.Offset, line.EndOffset);
            var diagnostic = (Config.Current?.EditorDiagnosticMarkers ?? Array.Empty<ScriptDiagnosticInfo>())
                .FirstOrDefault(item =>
                {
                    var spanLength = Math.Max(1, item.Length);
                    return offset >= item.StartOffset && offset < item.StartOffset + spanLength;
                });

            if (diagnostic == null)
            {
                CloseDiagnosticTooltip();
                return;
            }

            ToolTip.SetTip(textView, $"{diagnostic.Severity} {diagnostic.Id} (line {diagnostic.Line}, column {diagnostic.Column})\n{diagnostic.Message}");
            ToolTip.SetIsOpen(textView, true);
        }

        void OnPointerHoverStopped(object sender, PointerEventArgs args)
            => CloseDiagnosticTooltip();

        textView.PointerHover += OnPointerHover;
        textView.PointerHoverStopped += OnPointerHoverStopped;

        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMate = editor.InstallTextMate(registryOptions);
        var csharp = registryOptions.GetLanguageByExtension(".cs");
        if (csharp != null)
            textMate.SetGrammar(registryOptions.GetScopeByLanguageId(csharp.Id));

        var caretStatus = new TextBlock
        {
            Text = "Line 1, Column 1 • Offset 0",
            Margin = new Thickness(2, 4, 2, 0),
            Foreground = Brushes.LightGray,
            FontSize = 12,
        };

        var searchBox = new TextBox
        {
            Width = 220,
            Watermark = "Find in script…",
        };
        var searchStatus = new TextBlock
        {
            Text = "Enter text to search",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.LightGray,
            FontSize = 12,
        };

        bool FindMatch(bool forward)
        {
            var query = searchBox.Text ?? string.Empty;
            var source = editor.Document.Text;
            if (query.Length == 0)
            {
                searchStatus.Text = "Enter text to search";
                return false;
            }

            var comparison = StringComparison.OrdinalIgnoreCase;
            int match;
            if (forward)
            {
                var start = editor.SelectionLength > 0
                    ? editor.SelectionStart + editor.SelectionLength
                    : editor.TextArea.Caret.Offset;
                match = source.IndexOf(query, Math.Clamp(start, 0, source.Length), comparison);
                if (match < 0 && start > 0)
                    match = source.IndexOf(query, 0, comparison);
            }
            else
            {
                var start = editor.SelectionLength > 0
                    ? editor.SelectionStart - 1
                    : editor.TextArea.Caret.Offset - 1;
                match = start >= 0
                    ? source.LastIndexOf(query, Math.Min(start, source.Length - 1), comparison)
                    : -1;
                if (match < 0 && source.Length > 0)
                    match = source.LastIndexOf(query, source.Length - 1, comparison);
            }

            if (match < 0)
            {
                searchStatus.Text = "No matches found";
                return false;
            }

            editor.Select(match, query.Length);
            var lineNumber = editor.Document.GetLineByOffset(match).LineNumber;
            editor.ScrollToLine(lineNumber);
            editor.TextArea.Focus();
            searchStatus.Text = $"Found at line {lineNumber}, column {editor.Document.GetLocation(match).Column}";
            return true;
        }

        var findPreviousButton = new Button { Content = "Previous", MinWidth = 68 };
        findPreviousButton.Click += (_, _) => FindMatch(false);
        var findNextButton = new Button { Content = "Next", MinWidth = 54 };
        findNextButton.Click += (_, _) => FindMatch(true);
        searchBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                FindMatch(true);
                args.Handled = true;
            }
        };
        var searchPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 4),
        };
        searchPanel.Children.Add(searchBox);
        searchPanel.Children.Add(findPreviousButton);
        searchPanel.Children.Add(findNextButton);
        searchPanel.Children.Add(searchStatus);

        void UpdateCaretStatus()
        {
            var offset = Math.Clamp(editor.TextArea.Caret.Offset, 0, editor.Document.TextLength);
            var location = editor.Document.GetLocation(offset);
            caretStatus.Text = $"Line {location.Line}, Column {location.Column} • Offset {offset}";
        }

        editor.TextChanged += (_, _) =>
        {
            setter(editor.Text);
            UpdateCaretStatus();
        };
        editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretStatus();
        UpdateCaretStatus();

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
                CloseDiagnosticTooltip();
                textView.Redraw();
            }
            else if (args.PropertyName == nameof(Config.EditorNavigationVersion))
            {
                var offset = Math.Clamp(Config.Current.EditorNavigationOffset, 0, editor.Document.TextLength);
                var line = editor.Document.GetLineByOffset(offset).LineNumber;
                editor.ScrollToLine(line);
                editor.TextArea.Caret.Offset = offset;
                editor.TextArea.Focus();
            }
        };
        Config.Current.PropertyChanged += handler;
        editor.DetachedFromVisualTree += (_, _) =>
        {
            Config.Current.PropertyChanged -= handler;
            textView.PointerHover -= OnPointerHover;
            textView.PointerHoverStopped -= OnPointerHoverStopped;
            CloseDiagnosticTooltip();
        };

        var editorContainer = new StackPanel { Spacing = 2 };
        editorContainer.Children.Add(searchPanel);
        editorContainer.Children.Add(editor);
        editorContainer.Children.Add(caretStatus);

        return RowBuilder.NewRow(
            Tools.Tools.GetLabelOrDefault(name, Label),
            Description,
            editorContainer);
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
                var spanEnd = spanStart + diagnostic.Length;
                var start = Math.Max(lineStart, spanStart);
                var end = Math.Min(lineEnd, spanEnd);

                // Roslyn can report a zero-length span for a missing token at the
                // end of a line. Highlight the nearest existing character instead.
                if (diagnostic.Length == 0 && start == end && start == lineEnd && line.Length > 0)
                    start = lineEnd - 1;
                if (diagnostic.Length == 0 && end == start && line.Length > 0)
                    end = start + 1;
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
