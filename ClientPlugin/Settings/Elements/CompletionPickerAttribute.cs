using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace SE2ScriptRuntime.Settings.Elements;

/// <summary>
/// Provides a small, curated list of host-API snippets for the script editor.
/// This is an insertion helper, not context-aware IntelliSense.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class CompletionPickerAttribute : Attribute, IElement
{
    private static readonly string[] Suggestions =
    {
        "Api.Log(\"Your message here\");",
        "await Api.Delay(TimeSpan.FromSeconds(1));",
        "while (!Api.CancellationToken.IsCancellationRequested)\\n{\\n    await Api.Delay(TimeSpan.FromSeconds(1));\\n    Api.Log(\"Running\");\\n}",
        "if (Api.CancellationToken.IsCancellationRequested) return;",
        "try\\n{\\n    Api.Log(\"Starting\");\\n}\\ncatch (Exception ex)\\n{\\n    Api.Log(ex.ToString());\\n}",
    };

    public readonly string Label;
    public readonly string Description;

    public CompletionPickerAttribute(string label = null, string description = null)
    {
        Label = label;
        Description = description;
    }

    public Control BuildRow(string name, Func<object> getter, Action<object> setter)
    {
        var comboBox = new ComboBox
        {
            Width = 420,
            MinHeight = SettingsLayout.ControlHeight,
            ItemsSource = Suggestions,
            SelectedItem = getter()?.ToString() ?? Suggestions[0],
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is string selected)
                setter(selected);
        };

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == name)
                comboBox.SelectedItem = getter()?.ToString();
        };
        Config.Current.PropertyChanged += handler;
        comboBox.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= handler;

        return RowBuilder.NewRow(
            Tools.Tools.GetLabelOrDefault(name, Label),
            Description,
            comboBox);
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}
