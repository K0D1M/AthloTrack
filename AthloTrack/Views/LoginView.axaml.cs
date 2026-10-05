using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AthloTrack.Views;

public partial class LoginView : UserControl
{
    private LoginViewModel? _vm;

    public LoginView()
    {
        InitializeComponent();
        // The keyboard opens with the form (Email gets the focus): keep the form above it. The
        // motto steps aside for the room.
        _ = new KeyboardInset(this, inset =>
        {
            Motto.IsVisible = inset <= 0;
            Content.Margin = new Avalonia.Thickness(16, 24, 16, 24 + inset);
        });
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelChanged;
        _vm = DataContext as LoginViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnViewModelChanged;
    }

    /// <summary>
    /// The cursor goes to Email when a form opens: after choosing a role, and once at start-up
    /// when the remembered role's form is shown and the silent sign-in didn't happen. A failed
    /// login leaves the focus where the user put it.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        var opened = e.PropertyName == nameof(LoginViewModel.Step) || e.PropertyName == nameof(LoginViewModel.IsReady);
        if (opened && _vm.IsForm && _vm.IsReady) Dispatcher.UIThread.Post(FocusEmail, DispatcherPriority.Background);
    }

    private void FocusEmail()
    {
        // The view may already have been replaced (signed in meanwhile).
        if (_vm is null || !_vm.IsForm || TopLevel.GetTopLevel(EmailBox) is null) return;
        EmailBox.Focus(NavigationMethod.Tab);
        EmailBox.CaretIndex = EmailBox.Text?.Length ?? 0;
    }
}
