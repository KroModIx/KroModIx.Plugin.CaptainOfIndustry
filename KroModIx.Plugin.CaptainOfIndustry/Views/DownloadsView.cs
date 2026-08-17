using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace KroModIx.Plugin.CaptainOfIndustry.Views;

/// <summary>Downloads-Tab-View. Simple Liste .zip-Archive + Install/Delete
/// pro Row + „Alle installieren"-Bulk-Button + „Ordner oeffnen" damit
/// der User was reinlegen kann.</summary>
public sealed class DownloadsView : UserControl
{
    public DownloadsView()
    {
        var refreshBtn = new Button { Content = Strings.T("btn.refresh") };
        refreshBtn.Bind(Button.CommandProperty,
            new Binding(nameof(DownloadsViewModel.RefreshCommand)));

        var openBtn = new Button { Content = Strings.T("btn.open_folder") };
        openBtn.Classes.Add("ghost");
        openBtn.Bind(Button.CommandProperty,
            new Binding(nameof(DownloadsViewModel.OpenDownloadsFolderCommand)));

        var bulkBtn = new Button { Content = Strings.T("btn.install_all") };
        bulkBtn.Classes.Add("accent");
        bulkBtn.Bind(Button.CommandProperty,
            new Binding(nameof(DownloadsViewModel.InstallAllCommand)));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { refreshBtn, openBtn, bulkBtn },
        };

        var status = new TextBlock { Margin = new Thickness(0, 0, 0, 8) };
        status.Classes.Add("muted");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadsViewModel.StatusText)));

        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single,
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(DownloadsViewModel.Rows)));
        list.ItemTemplate = new FuncDataTemplate<DownloadRow>((row, _) =>
            row is null ? null : BuildRowCard(), true);

        Content = new DockPanel
        {
            Margin = new Thickness(16, 12),
            Children =
            {
                WithDock(toolbar, Dock.Top),
                WithDock(status, Dock.Top),
                list,
            },
        };
    }

    private static Control BuildRowCard()
    {
        var name = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = 13 };
        name.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.FileName)));

        var meta = new TextBlock { FontSize = 11 };
        meta.Classes.Add("muted");
        meta.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.SizeText)));

        var date = new TextBlock { FontSize = 11 };
        date.Classes.Add("muted");
        date.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.DateText)));

        var metaLine = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12,
            Children = { meta, date },
        };

        var titleColumn = new StackPanel
        {
            Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Children = { name, metaLine },
        };

        var installBtn = new Button { Content = Strings.T("btn.install") };
        installBtn.Classes.Add("accent");
        BindRowCmd(installBtn, nameof(DownloadsViewModel.InstallRowCommand));

        var deleteBtn = new Button { Content = Strings.T("btn.delete_file") };
        deleteBtn.Classes.Add("danger");
        BindRowCmd(deleteBtn, nameof(DownloadsViewModel.DeleteCommand));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { installBtn, deleteBtn },
        };
        actions.Margin = new Thickness(12, 0, 0, 0);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(12, 8),
        };
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
