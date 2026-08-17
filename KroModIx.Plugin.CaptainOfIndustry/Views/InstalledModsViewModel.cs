using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.CaptainOfIndustry.Services;

namespace KroModIx.Plugin.CaptainOfIndustry.Views;

/// <summary>Installiert-Tab: listet manuelle CoI-Mods im Docs-Mods-Ordner.
/// Kein Nexus-Enrichment (CoI-Community lebt auf Workshop, Nexus fast leer).
/// Toggle via <see cref="CoiInstallService"/>, Uninstall via Confirm-Dialog,
/// Filter-Textbox, „Ordner oeffnen"-Button.</summary>
public sealed partial class InstalledModsViewModel : ObservableObject, IDisposable
{
    private readonly DetectedGame _game;
    private readonly CoiModScanner _scanner;
    private readonly CoiInstallService _installer;
    private readonly CoiPathResolver _paths;
    private readonly DownloadEventBus _bus;
    private readonly IHostServices _host;
    private readonly EventHandler _installedHandler;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _filterText = "";

    public ObservableCollection<InstalledModRow> Rows { get; } = new();
    private List<InstalledModRow> _allRows = new();

    public InstalledModsViewModel(DetectedGame game, CoiModScanner scanner,
        CoiInstallService installer, CoiPathResolver paths,
        DownloadEventBus bus, IHostServices host)
    {
        _game = game; _scanner = scanner; _installer = installer;
        _paths = paths; _bus = bus; _host = host;
        _installedHandler = (_, _) => Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        _bus.ModInstalled += _installedHandler;
        _ = RefreshAsync();
    }

    public void Dispose() => _bus.ModInstalled -= _installedHandler;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = FilterText?.Trim() ?? "";
        Rows.Clear();
        var matched = string.IsNullOrEmpty(q)
            ? _allRows
            : _allRows.Where(r => r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var r in matched) Rows.Add(r);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            var mods = await Task.Run(() => _scanner.Scan(_game));
            _allRows = mods.Select(m => new InstalledModRow(m)).ToList();
            var enabled = mods.Count(m => m.IsEnabled);
            var disabled = mods.Count - enabled;
            StatusText = mods.Count == 0
                ? Strings.T("installed.no_mods")
                : string.Format(Strings.T("installed.count"), mods.Count, enabled, disabled);
            ApplyFilter();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenModsFolder()
    {
        var dir = _paths.EnsureModsDir(_game);
        _host.Shell.OpenDirectory(dir);
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(InstalledModRow? row)
    {
        if (row is null) return;
        try
        {
            IsBusy = true;
            var newPath = _installer.SetEnabled(row.Mod, !row.Mod.IsEnabled);
            row.Mod = row.Mod with { IsEnabled = !row.Mod.IsEnabled, Path = newPath };
            row.OnModChanged();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Toggle fehlgeschlagen: {Name}", row.Mod.DisplayName);
            await _host.Dialogs.ShowMessageAsync("Fehler", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UninstallAsync(InstalledModRow? row)
    {
        if (row is null) return;
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.uninstall_title"),
            string.Format(Strings.T("dialog.uninstall_msg"), row.Mod.DisplayName, row.Mod.Path),
            okLabel: Strings.T("dialog.uninstall_ok"));
        if (!ok) return;
        try
        {
            IsBusy = true;
            _installer.Uninstall(row.Mod);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Uninstall fehlgeschlagen: {Name}", row.Mod.DisplayName);
            await _host.Dialogs.ShowMessageAsync("Fehler", ex.Message);
        }
    }
}

public sealed partial class InstalledModRow : ObservableObject
{
    public InstalledModRow(CoiMod mod) => Mod = mod;
    [ObservableProperty] private CoiMod _mod;

    public string DisplayName => Mod.DisplayName;
    public string StatusLabel => Mod.IsEnabled ? Strings.T("row.status_active") : Strings.T("row.status_inactive");
    public string ToggleButtonLabel => Mod.IsEnabled ? Strings.T("btn.disable") : Strings.T("btn.enable");

    public string SubtitleText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Mod.Author)) parts.Add(Mod.Author!);
            var v = Mod.Version?.Trim() ?? "";
            if (v.Length > 0) parts.Add(char.IsDigit(v[0]) ? "v" + v : v);
            parts.Add(FormatSize(Mod.SizeBytes));
            parts.Add(Mod.InstalledUtc.ToLocalTime().ToString("yyyy-MM-dd"));
            return string.Join(" · ", parts);
        }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Mod.Description);

    public void OnModChanged()
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ToggleButtonLabel));
        OnPropertyChanged(nameof(SubtitleText));
    }

    private static string FormatSize(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB",
    };
}
