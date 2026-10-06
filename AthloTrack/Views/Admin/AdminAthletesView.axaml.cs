using Avalonia.Controls;

namespace AthloTrack.Views;

public partial class AdminAthletesView : UserControl, IHandlesBack
{
    public AdminAthletesView()
    {
        InitializeComponent();
    }

    /// <summary>Back closes an open confirmation or the move panel first.</summary>
    public bool TryHandleBack()
    {
        if (DataContext is not ViewModels.AdminAthletesViewModel vm) return false;
        if (vm.Confirm.IsOpen)
        {
            vm.Confirm.CancelCommand.Execute(null);
            return true;
        }
        if (vm.IsMoving)
        {
            vm.CancelMoveCommand.Execute(null);
            return true;
        }
        return false;
    }
}
