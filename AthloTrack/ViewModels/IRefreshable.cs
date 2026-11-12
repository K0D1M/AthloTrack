using System.Threading.Tasks;

namespace AthloTrack.ViewModels;

/// <summary>
/// A screen that reloads its data in place: pull down on a phone, or ⟳ in the top bar.
/// The page stays as it is (scroll, tab, filters) while it reloads.
/// </summary>
public interface IRefreshable
{
    Task RefreshAsync();
}
