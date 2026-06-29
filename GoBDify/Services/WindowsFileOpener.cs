using Microsoft.Maui.Storage;

namespace GoBDify.Services;

/// <summary>
/// Windows-Implementierung von <see cref="IFileOpener"/> über den MAUI-eigenen
/// <see cref="FilePicker"/> (nativer Datei-Öffnen-Dialog).
/// </summary>
public sealed class WindowsFileOpener : IFileOpener
{
    public async Task<string?> OpenTextAsync(CancellationToken ct = default)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Ordnerliste importieren"
        });
        if (result is null) return null;

        using var stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}
