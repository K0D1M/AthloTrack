using Avalonia.Controls;

namespace AthloTrack.Views;

public partial class AdminPushView : UserControl, IHandlesBack
{
    public AdminPushView()
    {
        InitializeComponent();
    }

    /// <summary>Back closes an open confirmation first.</summary>
    public bool TryHandleBack()
    {
        if (DataContext is not ViewModels.AdminPushViewModel { Confirm.IsOpen: true } vm) return false;
        vm.Confirm.CancelCommand.Execute(null);
        return true;
    }
}
