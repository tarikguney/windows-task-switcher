using System.Windows;
using System.Windows.Input;
using WindowTaskSwitcher.ViewModels;

namespace WindowTaskSwitcher.Views;

public partial class SwitcherWindow : Window
{
    private SwitcherViewModel ViewModel => (SwitcherViewModel)DataContext;

    public SwitcherWindow()
    {
        InitializeComponent();
    }

    public void ShowSwitcher()
    {
        ViewModel.Show();
        Show();
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void HideSwitcher()
    {
        ViewModel.Hide();
        Hide();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideSwitcher();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideSwitcher();
                e.Handled = true;
                break;

            case Key.Enter:
                ViewModel.SwitchToSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                ViewModel.MoveSelectionUpCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                ViewModel.MoveSelectionDownCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Tab:
                // Tab cycles down, Shift+Tab cycles up
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    ViewModel.MoveSelectionUpCommand.Execute(null);
                else
                    ViewModel.MoveSelectionDownCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.W when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ViewModel.CloseSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
