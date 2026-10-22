using System.Windows;
using System.Windows.Input;
using SecondSonLauncher.ViewModels;

namespace SecondSonLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "Launcher error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        var vm = new MainViewModel();

        // "Start the game directly": Shift or Ctrl held while opening shows the launcher instead.
        var held = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ||
                   Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        if (vm.ShouldStartDirectly(held) && vm.Play())
        {
            Shutdown();
            return;
        }

        MainWindow = new MainWindow { DataContext = vm };
        MainWindow.Show();
    }
}
