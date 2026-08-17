using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace KroModIx.Plugin.CaptainOfIndustry.Views;

/// <summary>Installiert-Tab: Toolbar + Filter + Mod-Row-Liste. Row-Layout
/// simpler als bei den Nexus-Plugins — kein Cover, kein Enrichment (CoI
/// hat keine sinnvolle Katalog-Quelle fuer manuelle Mods).</summary>
public sealed class InstalledModsView : UserControl
{
    public InstalledModsView()
    {
        var refreshBtn = new Button { Content = Strings.T("btn.refresh") };
        refreshBtn.Bind(Button.CommandProperty,
            new Binding(nameof(InstalledModsViewModel.RefreshCommand)));

        var openBtn = new Button { Content = Strings.T("btn.open_folder") };
        openBtn.Classes.Add("ghost");
        openBtn.Bind(Button.CommandProperty,
            new Binding(nameof(InstalledModsViewModel.OpenModsFolderCommand)));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { refreshBtn, openBtn },
        };

        var status = new TextBlock { Margin = new Thickness(0, 0, 0, 4) };
        status.Classes.Add("muted");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModsViewModel.StatusText)));

        var filter = new TextBox
        {
            PlaceholderText = Strings.T("placeholder.search_mods"),
            Margin = new Thickness(0, 0, 0, 8),
        };
        filter.Bind(TextBox.TextProperty,
            new Binding(nameof(InstalledModsViewModel.FilterText)) { Mode = BindingMode.TwoWay });

        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single,
        };
        list.Bind(ListBox.ItemsSourceProperty,
            new Binding(nameof(InstalledModsViewModel.Rows)));
        list.ItemTemplate = new FuncDataTemplate<InstalledModRow>((row, _) =>
            row is null ? null : BuildRowCard(), true);

        Content = new DockPanel
        {
            Margin = new Thickness(16, 12),
            Children =
            {
                WithDock(toolbar, Dock.Top),
                WithDock(status, Dock.Top),
                WithDock(filter, Dock.Top),
                list,
            },
        };
    }

    private static Control BuildRowCard()
    {
        var name = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = 14 };
        name.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModRow.DisplayName)));

        var subtitle = new TextBlock { FontSize = 11 };
        subtitle.Classes.Add("muted");
        subtitle.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModRow.SubtitleText)));

        var description = new TextBlock
        {
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
        };
        description.Classes.Add("secondary");
        description.Bind(TextBlock.TextProperty, new Binding($"{nameof(InstalledModRow.Mod)}.{nameof(Services.CoiMod.Description)}"));
        description.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(InstalledModRow.HasDescription)));

        var status = new TextBlock { FontSize = 10, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 2, 0, 0) };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModRow.StatusLabel)));

        var titleColumn = new StackPanel
        {
            Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Children = { name, subtitle, description, status },
        };

        var toggleBtn = new Button();
        toggleBtn.Bind(Button.ContentProperty, new Binding(nameof(InstalledModRow.ToggleButtonLabel)));
        BindRowCmd(toggleBtn, nameof(InstalledModsViewModel.ToggleEnabledCommand));

        var uninstallBtn = new Button { Content = Strings.T("btn.uninstall") };
        uninstallBtn.Classes.Add("danger");
        BindRowCmd(uninstallBtn, nameof(InstalledModsViewModel.UninstallCommand));

        var actions = new StackPanel
        {
            Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
            Children = { toggleBtn, uninstallBtn },
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(12, 8),
        };
        actions.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(titleColumn, 0);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(titleColumn);
        grid.Children.Add(actions);

        var card = new Border { Margin = new Thickness(0, 0, 0, 6), Child = grid };
        card.Classes.Add("card");
        return card;
    }

    private static void BindRowCmd(Button btn, string cmd)
    {
        btn.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource
            { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + cmd,
        });
        btn.Bind(Button.CommandParameterProperty, new Binding("."));
    }

    private static Control WithDock(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }
}
