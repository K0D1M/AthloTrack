using AthloTrack.Core.Auth;
using AthloTrack.WinUI.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace AthloTrack.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "AthloTrack";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;

        if (AppWindow is { } aw)
        {
            aw.Resize(new SizeInt32(1180, 780));
            aw.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }

        ShowLogin();
    }

    public void ShowLogin()
    {
        var login = new LoginPage();
        login.LoginSucceeded += OnLoginSucceeded;
        RootFrame.Content = login;
    }

    private void OnLoginSucceeded(UserRole role)
    {
        var shell = new ShellPage();
        shell.LogoutRequested += ShowLogin;
        RootFrame.Content = shell;
    }
}
