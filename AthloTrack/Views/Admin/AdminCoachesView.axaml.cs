using Avalonia.Controls;

namespace AthloTrack.Views;

public partial class AdminCoachesView : UserControl, IHandlesBack
{
    public AdminCoachesView()
    {
        InitializeComponent();
    }

    /// <summary>Back closes an open confirmation or the new-coach form first.</summary>
    public bool TryHandleBack()
    {
        if (DataContext is not ViewModels.AdminCoachesViewModel vm) return false;
        if (vm.Confirm.IsOpen)
        {
            vm.Confirm.CancelCommand.Execute(null);
            return true;
        }
        if (vm.IsCreating)
        {
            vm.IsCreating = false;
            return true;
        }
        return false;
    }
}
