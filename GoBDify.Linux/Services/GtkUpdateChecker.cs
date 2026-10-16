namespace GoBDify.Services;

/// <summary>
/// Linux/GTK-Implementierung von <see cref="IUpdateChecker"/>. Unter Linux gibt es keinen
/// AppInstaller-Mechanismus; Updates laufen über die jeweilige Paketverwaltung. Daher immer
/// <see cref="UpdateCheckStatus.NotSupported"/>.
/// </summary>
public sealed class GtkUpdateChecker : IUpdateChecker
{
    public Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
        => Task.FromResult(new UpdateCheckResult(UpdateCheckStatus.NotSupported));
}
