using System.IO;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Plugin-lokale Pfade (Downloads-Ordner unter PluginDataDir).
/// PluginDataDir wird vom Host garantiert (siehe IHostServices).</summary>
public sealed class CoiPaths
{
    public string DownloadsDir { get; }

    public CoiPaths(IHostServices host)
    {
        DownloadsDir = Path.Combine(host.PluginDataDir, "downloads");
        Directory.CreateDirectory(DownloadsDir);
    }
}

/// <summary>Simple Event-Bus fuer Cross-Tab-Kommunikation: Downloads-Tab
/// signalisiert dem Installiert-Tab dass sich der Mods-Ordner geaendert
/// hat (nach Install), damit der Installiert-Tab neu scannt.</summary>
public sealed class DownloadEventBus
{
    public event System.EventHandler? ModInstalled;
    public event System.EventHandler? DownloadsChanged;
    internal void RaiseModInstalled() => ModInstalled?.Invoke(this, System.EventArgs.Empty);
    internal void RaiseDownloadsChanged() => DownloadsChanged?.Invoke(this, System.EventArgs.Empty);
}
