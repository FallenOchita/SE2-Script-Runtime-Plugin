using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;

namespace SE2ScriptRuntime.Settings.Elements;

/// <summary>
/// Offers completion candidates based on the expression at the end of the editor buffer.
/// This is intentionally limited to the verified ScriptApi surface and common templates.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class CompletionPickerAttribute : Attribute, IElement
{
    private static readonly string[] Templates =
    {
        "Api.Log(\"Your message here\");",
        "await Api.Delay(TimeSpan.FromSeconds(1));",
        "while (!Api.CancellationToken.IsCancellationRequested)\\n{\\n    await Api.Delay(TimeSpan.FromSeconds(1));\\n    Api.Log(\"Running\");\\n}",
        "if (Api.CancellationToken.IsCancellationRequested) return;",
        "try\\n{\\n    Api.Log(\"Starting\");\\n}\\ncatch (Exception ex)\\n{\\n    Api.Log(ex.ToString());\\n}",
    };

    private static readonly string[] ApiMembers =
    {
        "Log",
        "Delay",
        "CancellationToken",
        "ThrowIfCancellationRequested",
    };

    private static readonly string[] TimeSpanMembers =
    {
        "FromSeconds",
        "FromMilliseconds",
        "FromMinutes",
    };

    private static readonly string[] CancellationMembers =
    {
        "IsCancellationRequested",
        "CanBeCanceled",
        "ThrowIfCancellationRequested",
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
            ItemsSource = Templates,
            SelectedItem = getter()?.ToString() ?? Templates[0],
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
            else if (args.PropertyName == nameof(Config.EditorSource))
            {
                var candidates = GetCandidates(Config.Current.EditorSource);
                comboBox.ItemsSource = candidates;
                comboBox.SelectedItem = candidates.FirstOrDefault();
            }
        };
        Config.Current.PropertyChanged += handler;
        comboBox.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= handler;

        comboBox.ItemsSource = GetCandidates(Config.Current.EditorSource);
        comboBox.SelectedItem = comboBox.ItemsSource is IEnumerable<string> items
            ? items.FirstOrDefault()
            : null;

        return RowBuilder.NewRow(
            Tools.Tools.GetLabelOrDefault(name, Label),
            Description,
            comboBox);
    }

    private static string[] GetCandidates(string source)
    {
        source ??= string.Empty;
        var api = Regex.Match(source, @"Api\\.(?<prefix>[A-Za-z_]\\w*)?$");
        if (api.Success)
            return Filter(ApiMembers, api.Groups["prefix"].Value);

        var timeSpan = Regex.Match(source, @"TimeSpan\\.(?<prefix>[A-Za-z_]\\w*)?$");
        if (timeSpan.Success)
            return Filter(TimeSpanMembers, timeSpan.Groups["prefix"].Value);

        var cancellation = Regex.Match(source, @"Api\\.CancellationToken\\.(?<prefix>[A-Za-z_]\\w*)?$");
        if (cancellation.Success)
            return Filter(CancellationMembers, cancellation.Groups["prefix"].Value);

        return Templates;
    }

    private static string[] Filter(string[] values, string prefix)
    {
        var matches = values
            .Where(value => value.StartsWith(prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length == 0 ? values : matches;
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}
