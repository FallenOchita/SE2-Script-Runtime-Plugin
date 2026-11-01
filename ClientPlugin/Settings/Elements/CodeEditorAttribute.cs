using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml.Styling;
using AvaloniaEdit;
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

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}
