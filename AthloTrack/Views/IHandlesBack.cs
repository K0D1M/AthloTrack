namespace AthloTrack.Views;

/// <summary>
/// A page view that uses back itself before the shell navigates (e.g. to close an open menu).
/// MainView asks the view on screen first, for the Up arrow and Android's back button/gesture.
/// </summary>
public interface IHandlesBack
{
    /// <returns>true if the back was used here and the shell shouldn't navigate.</returns>
    bool TryHandleBack();
}
