using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Keen.VRage.UI.Screens;
using SE2ScriptRuntime.Tools;

namespace SE2ScriptRuntime.Settings;

internal sealed class ScriptManagementViewModel : ScreenViewModel
{
    public Action OnClosed { get; }

    public ScriptManagementViewModel(Action onClosed)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;
        OnClosed = onClosed;
        InitializeInputContext();
    }

    public override void OnClose()
    {
        base.OnClose();
        OnClosed?.Invoke();
    }
}

internal sealed class ScriptManagementScreen : ScreenView
{
    private readonly HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly StackPanel rows = new();
    private readonly TextBlock summary = new();
    private readonly TextBlock message = new();
    private TextBox search;
    private ComboBox sort;

    public ScriptManagementScreen() => Build();

    private void Build()
    {
        var close = new Button { Content = "Close", Width = 100, Height = 42 };
        close.Click += (_, _) => Dispose();

        var title = new TextBlock
        {
            Text = "Script Management",
            FontSize = 25,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(title);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        search = new TextBox
        {
            Watermark = "Search scripts by filename...",
            Height = 44,
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        search.TextChanged += (_, _) => RebuildRows();

        sort = new ComboBox { Width = 220, Height = 44, FontSize = 17 };
        sort.Items.Add("Name (A-Z)");
        sort.Items.Add("Name (Z-A)");
        sort.Items.Add("Status");
        sort.SelectedIndex = 0;
        sort.SelectionChanged += (_, _) => RebuildRows();

        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        filters.Children.Add(search);
        filters.Children.Add(sort);

        summary.Foreground = Brushes.White;
        summary.FontSize = 17;
        message.Foreground = Brushes.LightGreen;
        message.FontSize = 15;
        message.TextWrapping = TextWrapping.Wrap;

        var actionRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        AddButton(actionRow1, "Select Visible", () =>
        {
            foreach (var name in GetVisibleScripts()) selected.Add(name);
            RebuildRows();
        });
        AddButton(actionRow1, "Clear Selection", () => { selected.Clear(); RebuildRows(); });
        AddButton(actionRow1, "Refresh", () => RebuildRows());

        var actionRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        AddButton(actionRow2, "Enable Selected", () => SetSelectedEnabled(true));
        AddButton(actionRow2, "Disable Selected", () => SetSelectedEnabled(false));
        AddButton(actionRow2, "Start Selected", StartSelected);
        AddButton(actionRow2, "Stop Selected", StopSelected);

        var actionRow3 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        AddButton(actionRow3, "Add to Auto-start", () => UpdateAutoStart(true));
        AddButton(actionRow3, "Remove from Auto-start", () => UpdateAutoStart(false));

        var listBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(77, 99, 96)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                Content = rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        content.Children.Add(header);
        content.Children.Add(summary);
        content.Children.Add(filters);
        content.Children.Add(actionRow1);
        content.Children.Add(actionRow2);
        content.Children.Add(actionRow3);
        content.Children.Add(message);
        content.Children.Add(listBorder);

        var root = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x80, 37, 47, 53)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(96, 66, 77, 99)),
            BorderThickness = new Thickness(1.5),
            Width = 1040,
            Height = 820,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = content
        };
        var overlay = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        overlay.Children.Add(root);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = Brushes.Transparent;
        Content = overlay;
    }

    private static void AddButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Height = 42, MinWidth = 120, FontSize = 16 };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RebuildRows();
    }

    private string[] GetVisibleScripts()
    {
        var files = Plugin.Instance.Scripts.ListScripts().AsEnumerable();
        var filter = search?.Text?.Trim();
        if (!string.IsNullOrEmpty(filter))
            files = files.Where(name => name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

        var disabled = Config.Current.GetDisabledScriptNames();
        var running = Plugin.Instance.Scripts.RunningScripts;
        files = (sort?.SelectedIndex ?? 0) switch
        {
            1 => files.OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase),
            2 => files.OrderBy(name => GetState(name, disabled, running), StringComparer.Ordinal).ThenBy(name => name, StringComparer.OrdinalIgnoreCase),
            _ => files.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        };
        return files.ToArray();
    }

    private static string GetState(string name, string[] disabled, string[] running)
        => disabled.Contains(name, StringComparer.OrdinalIgnoreCase) ? "DISABLED"
            : running.Contains(name, StringComparer.OrdinalIgnoreCase) ? "RUNNING" : "ENABLED / STOPPED";

    private void RebuildRows()
    {
        if (rows == null || Plugin.Instance?.Scripts == null) return;
        rows.Children.Clear();
        var files = GetVisibleScripts();
        var disabled = Config.Current.GetDisabledScriptNames();
        var running = Plugin.Instance.Scripts.RunningScripts;
        var autoStart = Config.Current.GetAutoStartScriptNames();
        foreach (var name in files)
        {
            var state = GetState(name, disabled, running);
            var auto = autoStart.Contains(name, StringComparer.OrdinalIgnoreCase) ? "AUTO-START" : "";
            var check = new CheckBox { IsChecked = selected.Contains(name), VerticalAlignment = VerticalAlignment.Center };
            check.IsCheckedChanged += (_, _) =>
            {
                if (check.IsChecked == true) selected.Add(name);
                else selected.Remove(name);
                UpdateSummary(files.Length);
            };
            var label = new TextBlock
            {
                Text = $"{name}    [{state}]    {auto}",
                Foreground = Brushes.White,
                FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(4, 6) };
            row.Children.Add(check);
            row.Children.Add(label);
            rows.Children.Add(row);
        }
        UpdateSummary(files.Length);
    }

    private void UpdateSummary(int visibleCount)
    {
        var all = Plugin.Instance.Scripts.ListScripts();
        var disabled = Config.Current.GetDisabledScriptNames();
        var running = Plugin.Instance.Scripts.RunningScripts;
        summary.Text = $"Scripts: {all.Length} total | {all.Count(name => !disabled.Contains(name, StringComparer.OrdinalIgnoreCase))} enabled | {all.Count(name => disabled.Contains(name, StringComparer.OrdinalIgnoreCase))} disabled | {running.Length} running | {selected.Count} selected | {visibleCount} visible";
    }

    private string[] GetSelectedExisting()
    {
        var existing = Plugin.Instance.Scripts.ListScripts();
        return selected.Where(name => existing.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    private void SetSelectedEnabled(bool enabled)
    {
        var names = new HashSet<string>(Config.Current.GetDisabledScriptNames(), StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetSelectedExisting())
        {
            if (enabled) names.Remove(name);
            else
            {
                names.Add(name);
                Plugin.Instance.Scripts.Stop(name);
            }
        }
        Config.Current.DisabledScriptNames = string.Join(Environment.NewLine, names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        SetMessage($"{(enabled ? "Enabled" : "Disabled")} {GetSelectedExisting().Length} selected script(s). Changes save when the window closes.");
        RebuildRows();
    }

    private void StartSelected()
    {
        var started = 0;
        foreach (var name in GetSelectedExisting())
            if (Plugin.Instance.Scripts.Start(name)) started++;
        SetMessage($"Start requested for {started} selected script(s). Disabled scripts and scripts with compiler errors are skipped.");
        RebuildRows();
    }

    private void StopSelected()
    {
        var stopped = 0;
        foreach (var name in GetSelectedExisting())
            if (Plugin.Instance.Scripts.Stop(name)) stopped++;
        SetMessage($"Cancellation requested for {stopped} selected running script(s).");
        RebuildRows();
    }

    private void UpdateAutoStart(bool add)
    {
        var names = new HashSet<string>(Config.Current.GetAutoStartScriptNames(), StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetSelectedExisting())
        {
            if (add) names.Add(name);
            else names.Remove(name);
        }
        Config.Current.AutoStartScriptNames = string.Join(Environment.NewLine, names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        SetMessage($"{(add ? "Added" : "Removed")} selected scripts {(add ? "to" : "from")} auto-start. Auto-start still requires its master setting to be enabled.");
        RebuildRows();
    }

    private void SetMessage(string text) => message.Text = text;
}
