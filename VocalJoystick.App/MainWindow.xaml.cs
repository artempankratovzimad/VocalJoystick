using System;
using System.Threading;
using System.Windows;
using VocalJoystick.App.ViewModels;
using VocalJoystick.App.Views;

namespace VocalJoystick.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.SampleListRequested += OnSampleListRequested;
        Closed += (_, _) => _viewModel.SampleListRequested -= OnSampleListRequested;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private void OnSampleListRequested(object? sender, SampleListRequestEventArgs args)
    {
        var viewModel = new DirectionalSampleListViewModel(
            args.Action,
            args.Samples,
            args.Template,
            args.AverageMetrics,
            args.DeleteSampleCallback);

        var dialog = new DirectionalSampleListWindow
        {
            Owner = this,
            DataContext = viewModel
        };

        void CloseHandler() => dialog.Close();
        viewModel.RequestClose += CloseHandler;
        dialog.Closed += (_, _) => viewModel.RequestClose -= CloseHandler;
        dialog.ShowDialog();
    }
}
