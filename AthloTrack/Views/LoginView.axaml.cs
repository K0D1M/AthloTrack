using System;
using System.ComponentModel;
using AthloTrack.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AthloTrack.Views;

public partial class LoginView : UserControl
{
    /// <summary>From this width the brand gets its own half (desktop, a tablet, a wide browser).</summary>
    private const double WideFrom = 900;

    private LoginViewModel? _vm;
    private bool? _wide;

    /// <summary>The logo, where the opening screen's logo lands (SplashView).</summary>
    public Control LogoElement => Logo;

    public LoginView()
    {
        InitializeComponent();
        // The keyboard opens with the form (Email gets the focus); the app ends above it (App),
        // and the motto steps aside for the room.
        _ = new KeyboardInset(this, inset => Motto.IsVisible = inset <= 0);
        FormArea.SizeChanged += (_, _) => Arrange();
        SizeChanged += (_, _) => Arrange();
    }

    /// <summary>Phone: band, card, links, motto at the bottom. Wide: brand half on the left.</summary>
    private void Arrange()
    {
        var wide = Bounds.Width >= WideFrom;
        if (wide != _wide)
        {
            _wide = wide;
            Root.ColumnDefinitions = new ColumnDefinitions(wide ? "*,560" : "*");
            if (wide)
            {
                Move(Brand, Root, 0);
                Grid.SetColumn(Brand, 0);
                Grid.SetColumn(FormArea, 1);
                Move(Motto, BrandContent);
                Motto.Foreground = Brushes.White;
                Motto.Margin = new Thickness(0, 40, 0, 0);
                Card.Margin = new Thickness(40, 0);
                FormGrid.VerticalAlignment = VerticalAlignment.Center;
                AppName.FontSize = 52;
                Tagline.FontSize = 20;
                Logo.Width = Logo.Height = 96;
            }
            else
            {
                Move(Brand, FormGrid, 0);
                Grid.SetColumn(FormArea, 0);
                Move(Motto, FormGrid);
                // Back to the theme's heading colour (follows Light/Dark).
                Motto.Bind(TextBlock.ForegroundProperty, Motto.GetResourceObservable("HeadingBrush"));
                Motto.Margin = new Thickness(16, 24, 16, 22);
                Card.Margin = new Thickness(16, -28, 16, 0);
                FormGrid.VerticalAlignment = VerticalAlignment.Stretch;
                AppName.FontSize = 30;
                Tagline.FontSize = 14;
                Logo.Width = Logo.Height = 88;
            }
        }

        // Phone: at least the screen's height, so the motto sits at the bottom.
        FormGrid.MinHeight = wide ? 0 : FormArea.Bounds.Height;
    }

    /// <summary>Puts <paramref name="child"/> into <paramref name="panel"/> (at <paramref name="index"/>, else last).</summary>
    private static void Move(Control child, Panel panel, int? index = null)
    {
        if (ReferenceEquals(child.Parent, panel)) return;
        (child.Parent as Panel)?.Children.Remove(child);
        if (index is { } i) panel.Children.Insert(i, child);
        else panel.Children.Add(child);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelChanged;
        _vm = DataContext as LoginViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnViewModelChanged;
    }

    /// <summary>
    /// The cursor goes to the card's first box when the card changes, and once at start-up when
    /// the silent sign-in didn't happen. A failed attempt leaves the focus where the user put it.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        var opened = e.PropertyName == nameof(LoginViewModel.Step) || e.PropertyName == nameof(LoginViewModel.IsReady);
        if (opened && _vm.IsReady) Dispatcher.UIThread.Post(FocusFirst, DispatcherPriority.Background);
    }

    private void FocusFirst()
    {
        // The view may already have been replaced (signed in meanwhile).
        if (_vm is null) return;
        TextBox box = _vm.IsCodeStep ? CodeBox : EmailBox;
        if (!box.IsEffectivelyVisible || TopLevel.GetTopLevel(box) is null) return;
        box.Focus(NavigationMethod.Tab);
        box.CaretIndex = box.Text?.Length ?? 0;
    }
}
