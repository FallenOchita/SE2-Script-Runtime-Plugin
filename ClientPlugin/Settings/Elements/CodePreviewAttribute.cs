using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace SE2ScriptRuntime.Settings.Elements;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class CodePreviewAttribute : Attribute, IElement
{
    private static readonly Regex Tokens = new(
        @"(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/)|(?<string>@?""(?:\.|[^""\])*""|'(?:\\.|[^'\\])*')|(?<keyword>\b(?:using|namespace|class|public|private|static|void|int|string|bool|var|new|return|if|else|while|for|foreach|await|async|try|catch|throw|true|false|null|this|in|is|as)\b)|(?<api>\b(?:Api|TimeSpan|CancellationToken|Task|Console)\b)|(?<number>\b\d+(?:\.\d+)?\b)",
        RegexOptions.Compiled);

    public readonly string Label;
    public readonly string Description;

    public CodePreviewAttribute(string label = null, string description = null)
    {
        Label = label;
        Description = description;
    }

    public Control BuildRow(string name, Func<object> getter, Action<object> setter)
    {
        var preview = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Avalonia.Thickness(8),
        };
        UpdatePreview(preview, getter()?.ToString() ?? string.Empty);

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == name)
                UpdatePreview(preview, getter()?.ToString() ?? string.Empty);
        };
        Config.Current.PropertyChanged += handler;
        preview.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= handler;

        var scroll = new ScrollViewer
        {
            Content = preview,
            Width = 420,
            Height = 190,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(24, 29, 36)),
        };
        return RowBuilder.NewRow(Tools.Tools.GetLabelOrDefault(name, Label), Description, scroll);
    }

    private static void UpdatePreview(TextBlock preview, string source)
    {
        preview.Inlines.Clear();
        var position = 0;
        foreach (Match match in Tokens.Matches(source))
        {
            if (match.Index > position)
                preview.Inlines.Add(new Run(source.Substring(position, match.Index - position)) { Foreground = Brushes.Gainsboro });

            var color = match.Groups["comment"].Success ? Brushes.ForestGreen
                : match.Groups["string"].Success ? Brushes.LightSalmon
                : match.Groups["keyword"].Success ? Brushes.CornflowerBlue
                : match.Groups["api"].Success ? Brushes.MediumTurquoise
                : Brushes.Goldenrod;
            preview.Inlines.Add(new Run(match.Value) { Foreground = color });
            position = match.Index + match.Length;
        }
        if (position < source.Length)
            preview.Inlines.Add(new Run(source.Substring(position)) { Foreground = Brushes.Gainsboro });
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}