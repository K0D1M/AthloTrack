using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace AthloTrack.Browser.Services;

/// <summary>wwwroot/download.js, imported as module "download": a file as a plain download.</summary>
[SupportedOSPlatform("browser")]
internal static partial class DownloadInterop
{
    public static async Task LoadAsync()
    {
        try
        {
            await JSHost.ImportAsync("download", "/download.js");
            AthloTrack.Services.FileSaver.BrowserDownload = (name, mediaType, bytes) =>
            {
                Download(name, mediaType, Convert.ToBase64String(bytes));
                return Task.CompletedTask;
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Downloads unavailable: {ex.Message}");
        }
    }

    [JSImport("download", "download")]
    private static partial void Download(string name, string mediaType, string base64);
}
