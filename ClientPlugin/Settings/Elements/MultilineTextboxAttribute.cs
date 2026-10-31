using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace SE2ScriptRuntime.Settings.Elements;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class MultilineTextboxAttribute : Attribute, IElement
{
    public readonly string Label;
    public readonly string Description;

    public MultilineTextboxAttribute(string label = null, string description = null)
    {
        Label = label;
        Description = description;
    }

    public Control BuildRow(string name, Func<object> getter, Action<object> setter)
    {
        var textBox = new TextBox
        {
            Text = (string)getter() ?? string.Empty,
            Width = 420,
            Height = 320,
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        textBox.TextChanged += (_, _) => setter(textBox.Text);

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == name)
                textBox.Text = (string)getter() ?? string.Empty;
        };
        Config.Current.PropertyChanged += handler;
        textBox.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= handler;

        return RowBuilder.NewRow(Tools.Tools.GetLabelOrDefault(name, Label), Description, textBox);
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}
