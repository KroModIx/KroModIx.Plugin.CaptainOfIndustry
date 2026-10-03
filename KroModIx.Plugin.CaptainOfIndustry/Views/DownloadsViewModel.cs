using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.CaptainOfIndustry.Services;

namespace KroModIx.Plugin.CaptainOfIndustry.Views;

/// <summary>Downloads-Tab: listet .zip-Archive im Plugin-Downloads-Ordner
/// (<c>plugin-data/kroste.captainofindustry/downloads/</c>). Extract nach
/// <c>&lt;Docs&gt;/Captain of Industry/Mods/&lt;ModName&gt;/</c>. Kein Nexus-
/// Download (CoI-Community lebt auf Workshop) — reine Local-Import-Ecke
/// fuer .zips die der User von Discord/GitHub-Releases dorthin kopiert.</summary>
public sealed partial class DownloadsViewModel : ObservableObject, IDisposable
{
    private readonly DetectedGame _game;
    private readonly CoiPaths _paths;
    private readonly CoiZipInstaller _installer;
    private readonly DownloadEventBus _bus;
    private readonly IHostServices _host;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<DownloadRow> Rows { get; } = new();

    public DownloadsViewModel(DetectedGame game, CoiPaths paths,
        CoiZipInstaller installer, DownloadEventBus bus, IHostServices host)
    {
        _game = game; _paths = paths; _installer = installer;
        _bus = bus; _host = host;
        Refresh();
    }

    public void Dispose() { }

    /// <summary>Snapshot VOR jedem File-Write (Kernprinzip 6). Fehler
    /// duerfen den Install NIEMALS blockieren — der User will installieren,
    /// nicht den Backup-Service debuggen. Zurueckspielen laeuft ueber das
    /// Backups-Fenster (Sidebar-Kontextmenue), bewusst ohne Auto-Rollback.</summary>
    private async Task TrySnapshotAsync(string label)
    {
        try
        {
            var dirs = new List<string>();
            if (Directory.Exists(_installer.GetModsDir(_game))) dirs.Add(_installer.GetModsDir(_game));
            if (dirs.Count == 0) return;
            var gameKey = _game.Target.SteamAppId is int appId ? $"steam:{appId}" : _game.InstallDir;
            await _host.Backup.CreateSnapshotAsync(
                pluginId: "kroste.captainofindustry", gameKey: gameKey,
                directories: dirs, label: label);
            await _host.Backup.PruneAsync("kroste.captainofindustry", gameKey, keepLast: 10);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Snapshot fehlgeschlagen (Install laeuft trotzdem): {Label}", label);
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        Rows.Clear();
        var dir = _paths.DownloadsDir;
        if (!Directory.Exists(dir))
        {
            StatusText = string.Format(Strings.T("downloads.no_dir"), dir);
            return;
        }
        // v0.6.0: nicht mehr nur *.zip — der Host-Baukasten kann auch RAR
        // und 7z, und welche Endungen das sind, weiss er selbst.
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .Where(_installer.HasSupportedExtension)
            .OrderByDescending(f => new FileInfo(f).CreationTimeUtc)
            .ToList();
        foreach (var f in files) Rows.Add(new DownloadRow(f));
        StatusText = Rows.Count == 0
            ? string.Format(Strings.T("downloads.no_dir"), dir)
            : string.Format(Strings.T("downloads.count"), Rows.Count);
    }

    [RelayCommand]
    private void OpenDownloadsFolder() => _host.Shell.OpenDirectory(_paths.DownloadsDir);

    [RelayCommand]
    private async Task InstallRowAsync(DownloadRow? row)
    {
        if (row is null) return;
        try
        {
            IsBusy = true;
            using var scope = _host.BeginProgress(string.Format(Strings.T("downloads.installing"), row.FileName));
            await TrySnapshotAsync($"Vor Install von {row.FileName}");
            var result = await Task.Run(() => _installer.Install(row.FilePath, _game));
            var msg = result.Success
                ? string.Format(Strings.T("downloads.install_ok"), result.Message)
                : string.Format(Strings.T("downloads.install_fail"), result.Message);
            _host.Notifications.Notify(msg,
                result.Success ? NotificationLevel.Success : NotificationLevel.Error);
            if (result.Success) _bus.RaiseModInstalled();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Install fehlgeschlagen: {File}", row.FileName);
            _host.Notifications.Notify("Fehler: " + ex.Message, NotificationLevel.Error);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task InstallAllAsync()
    {
        if (Rows.Count == 0) return;
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.install_all_title"),
            string.Format(Strings.T("dialog.install_all_msg"), Rows.Count),
            okLabel: Strings.T("dialog.install_all_ok"));
        if (!ok) return;
        int done = 0, failed = 0;
        var snapshot = Rows.ToList();
        // Bulk: EIN Snapshot vor der ganzen Schleife, nicht pro Row — beim
        // Rollback will der User zurueck auf den Stand VOR dem Batch.
        await TrySnapshotAsync($"Vor Bulk-Install ({Rows.Count} Archive)");
        using var scope = _host.BeginProgress("Bulk-Install …");
        foreach (var row in snapshot)
        {
            scope.Report((double)(done + failed) / snapshot.Count,
                $"{done + failed + 1}/{snapshot.Count}: {row.FileName}");
            try
            {
                var r = await Task.Run(() => _installer.Install(row.FilePath, _game));
                if (r.Success) done++; else failed++;
            }
            catch { failed++; }
        }
        _host.Notifications.Notify(
            string.Format(Strings.T("downloads.bulk_result"), done, failed),
            failed == 0 ? NotificationLevel.Success : NotificationLevel.Warning);
        _bus.RaiseModInstalled();
    }

    [RelayCommand]
    private async Task DeleteAsync(DownloadRow? row)
    {
        if (row is null) return;
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.delete_zip_title"),
            string.Format(Strings.T("dialog.delete_zip_msg"), row.FileName),
            okLabel: Strings.T("dialog.delete_zip_ok"));
        if (!ok) return;
        try { File.Delete(row.FilePath); Refresh(); }
        catch (Exception ex)
        {
            _host.Notifications.Notify("Delete-Fehler: " + ex.Message, NotificationLevel.Error);
        }
    }
}

public sealed class DownloadRow
{
    public string FilePath { get; }
    public string FileName { get; }
    public string SizeText { get; }
    public string DateText { get; }
    public DownloadRow(string path)
    {
        FilePath = path;
        FileName = Path.GetFileName(path);
        var info = new FileInfo(path);
        SizeText = info.Length switch
        {
            < 1024 => $"{info.Length} B",
            < 1024 * 1024 => $"{info.Length / 1024.0:F1} KB",
            _ => $"{info.Length / (1024.0 * 1024):F1} MB",
        };
        DateText = info.CreationTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}
