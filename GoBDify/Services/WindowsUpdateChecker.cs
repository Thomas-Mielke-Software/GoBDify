using Windows.ApplicationModel;

namespace GoBDify.Services;

/// <summary>
/// Windows-Implementierung von <see cref="IUpdateChecker"/> über den AppInstaller-Mechanismus
/// (<see cref="Package.CheckUpdateAvailabilityAsync"/>). Nur im net10.0-windows-Head verfügbar,
/// da die WinRT-Typen im gemeinsamen net10.0-UI-Projekt nicht existieren.
/// </summary>
public sealed class WindowsUpdateChecker : IUpdateChecker
{
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await Package.Current.CheckUpdateAvailabilityAsync();
            return result.Availability switch
            {
                PackageUpdateAvailability.NoUpdates => new(UpdateCheckStatus.UpToDate),
                PackageUpdateAvailability.Available => new(UpdateCheckStatus.Available),
                PackageUpdateAvailability.Required  => new(UpdateCheckStatus.Required),
                PackageUpdateAvailability.Error     => new(UpdateCheckStatus.Error),
                _                                   => new(UpdateCheckStatus.Unknown),
            };
        }
        catch (Exception ex)
        {
            return new(UpdateCheckStatus.Error, ex.Message);
        }
    }
}
