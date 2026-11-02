using System;
using System.Collections.Generic;
using System.Linq;
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
        var root = new StackPanel { Spacing = 6 };
        var selectedFilter = "All";

        void Rebuild()
        {
            list.Children.Clear();
            var diagnostics = getter() as ScriptDiagnosticInfo[] ?? Array.Empty<ScriptDiagnosticInfo>();
            var errors = diagnostics.Count(item => string.Equals(item.Severity, "Error", StringComparison.Ordinal));
            var warnings = diagnostics.Count(item => string.Equals(item.Severity, "Warning", StringComparison.Ordinal));
            var visibleDiagnostics = diagnostics.Where(item =>
                selectedFilter == "All"
                || (selectedFilter == "Errors" && string.Equals(item.Severity, "Error", StringComparison.Ordinal))
                || (selectedFilter == "Warnings" && string.Equals(item.Severity, "Warning", StringComparison.Ordinal))).ToArray();

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

            foreach (var diagnostic in visibleDiagnostics)
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

            if (visibleDiagnostics.Length == 0)
            {
                list.Children.Add(new TextBlock
                {
                    Text = $"No {selectedFilter.ToLowerInvariant()} to display. {errors} error(s), {warnings} warning(s) total.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.LightGray,
                });
            }
        }

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var previousButton = new Button
        {
            Content = "Previous",
            Padding = new Thickness(8, 4),
        };
        previousButton.Click += (_, _) => Config.Current?.NavigateToPreviousDiagnostic();
        toolbar.Children.Add(previousButton);

        var nextButton = new Button
        {
            Content = "Next",
            Padding = new Thickness(8, 4),
        };
        nextButton.Click += (_, _) => Config.Current?.NavigateToNextDiagnostic();
        toolbar.Children.Add(nextButton);

        foreach (var filterName in new[] { "All", "Errors", "Warnings" })
        {
            var filterButton = new Button
            {
                Content = filterName,
                Padding = new Thickness(8, 4),
            };
            filterButton.Click += (_, _) =>
            {
                selectedFilter = filterName;
                Rebuild();
            };
            toolbar.Children.Add(filterButton);
        }

        Rebuild();
        root.Children.Add(toolbar);
        root.Children.Add(list);
        var scroll = new ScrollViewer
        {
            Content = root,
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
