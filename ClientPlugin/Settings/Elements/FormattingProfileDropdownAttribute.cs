using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace SE2ScriptRuntime.Settings.Elements;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class FormattingProfileDropdownAttribute : Attribute, IElement
{
    public readonly string Label;
    public readonly string Description;

    public FormattingProfileDropdownAttribute(string label = null, string description = null)
    {
        Label = label;
        Description = description;
    }

    public Control BuildRow(string name, Func<object> getter, Action<object> setter)
    {
        var comboBox = new ComboBox
        {
            Width = 240,
            Height = SettingsLayout.ControlHeight,
            [TextElement.FontSizeProperty] = 18d,
        };

        void RefreshOptions()
        {
            var selected = getter() as string;
            comboBox.Items.Clear();
            var names = Config.Current?.GetFormattingProfileNames() ?? Array.Empty<string>();
            foreach (var profileName in names)
            {
                comboBox.Items.Add(new ComboBoxItem
                {
                    Content = new TextBlock { Text = profileName, FontSize = 18 },
                    Tag = profileName,
                });
            }

            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item
                    && string.Equals(item.Tag as string, selected, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        RefreshOptions();
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string value)
                setter(value);
        };

        if (Config.Current != null)
        {
            System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
            {
                if (args.PropertyName == nameof(Config.FormattingProfiles)
                    || args.PropertyName == nameof(Config.SelectedFormattingProfile))
                    RefreshOptions();
            };
            Config.Current.PropertyChanged += handler;
            comboBox.DetachedFromVisualTree += (_, _) => Config.Current.PropertyChanged -= handler;
        }

        return RowBuilder.NewRow(Tools.Tools.GetLabelOrDefault(name, Label), Description, comboBox);
    }

    public List<Type> SupportedTypes { get; } = new() { typeof(string) };
}
