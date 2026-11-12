using System;
using AthloTrack.ViewModels;
using Avalonia.Controls;

namespace AthloTrack.Views.Controls;

/// <summary>
/// Pull down to refresh, around a page's <see cref="ScrollViewer"/>: reloads the page's
/// <see cref="IRefreshable"/> view model. ⟳ in the top bar reloads the same view model.
/// </summary>
public class PullRefresh : RefreshContainer
{
    protected override Type StyleKeyOverride => typeof(RefreshContainer);

    public PullRefresh()
    {
        RefreshRequested += OnRefreshRequested;
    }

    private async void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (DataContext is IRefreshable page) await page.RefreshAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Refresh failed: {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }
}
