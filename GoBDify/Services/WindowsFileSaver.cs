using System.Text;
using CommunityToolkit.Maui.Storage;

namespace GoBDify.Services;

/// <summary>
/// Windows-Implementierung von <see cref="IFileSaver"/> über den
/// CommunityToolkit-<c>FileSaver</c> (nativer "Speichern unter"-Dialog).
/// </summary>
public sealed class WindowsFileSaver : IFileSaver
{
    public async Task<string?> SaveTextAsync(string suggestedFileName, string content, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(suggestedFileName, stream, ct);
        return result.IsSuccessful ? result.FilePath : null;
    }
}
