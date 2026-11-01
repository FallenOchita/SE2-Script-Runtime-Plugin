using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SE2ScriptRuntime.Settings.Elements;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class DiagnosticListAttribute : Attribute, IElement
{
    public readonly string Label;
    public readonly string Description;

    public DiagnosticListAttribute(string label = null, string description = null)
    {
        Label = label;
        Description = description;
    }

    public Control BuildRow(string name, Func<object> getter, Action<object> setter)
    {
        var list = new StackPanel { Spacing = 4 };

        void Rebuild()
        {
            list.Children.Clear();
            var diagnostics = getter() as ScriptDiagnosticInfo[] ?? Array.Empty<ScriptDiagnosticInfo>();
            if (diagnostics.Length == 0)
            {
                list.Children.Add(new TextBlock
                {
                    Text = "No compiler errors or warnings. Select Check Script to refresh.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.LightGray,
                });
                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                var isError = string.Equals(diagnostic.Severity, "Error", StringComparison.Ordinal);
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(8, 6),
                    Background = isError ? new SolidColorBrush(Color.FromArgb(45, 220, 65, 65))
                        : new SolidColorBrush(Color.FromArgb(40, 220, 165, 45)),
                    Content = new TextBlock
                    {
                        Text = $"{diagnostic.Severity} {diagnostic.Id}  •  Line {diagnostic.Line}, Col {diagnostic.Column}\n{diagnostic.Message}",
                        TextWrapping = TextWrapping.Wrap,
                    },
                };
                button.Click += (_, _) => Config.Current?.NavigateToDiagnostic(diagnostic);

                var row = new StackPanel { Spacing = 4 };
                row.Children.Add(button);
                if (ScriptDiagnosticQuickFix.TryGetInsertion(diagnostic, out _, out var fixTitle))
                {
                    var fixButton = new Button
                    {
                        Content = fixTitle,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Padding = new Thickness(8, 4),
                    };
                    fixButton.Click += (_, _) => Config.Current?.ApplyDiagnosticQuickFix(diagnostic);
                    row.Children.Add(fixButton);
                }

                list.Children.Add(row);
            }
        }

        Rebuild();
        var scroll = new ScrollViewer
        {
            Content = list,
            Width = 420,
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == nameof(Config.EditorDiagnosticMarkers))
                Rebuild();
        };
        Config.Current.PropertyChanged += handler;
        scroll.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= handler;

        return RowBuilder.NewRow(
            Tools.Tools.GetLabelOrDefault(name, Label),
            Description,
            scroll);
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(ScriptDiagnosticInfo[]) };
}
