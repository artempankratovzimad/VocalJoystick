using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.Services;

public sealed class TestOverlayController : ITestOverlayController, IDisposable
{
    private const double BallDiameter = 36;
    private const double BallRadius = BallDiameter / 2;

    private readonly ILogger _logger;
    private readonly object _sync = new();

    private OverlayWindow? _window;
    private double _x;
    private double _y;
    private CancellationTokenSource? _labelCts;

    public TestOverlayController(ILogger logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        Dispatch(() =>
        {
            if (_window is not null)
            {
                return;
            }

            _window = new OverlayWindow();
            _window.Show();

            _x = (_window.Width - BallDiameter) / 2;
            _y = (_window.Height - BallDiameter) / 2;
            _window.UpdateBall(_x, _y);
            _window.SetLabel(string.Empty);
            _logger.LogInfo("Test overlay started");
        });
    }

    public void Stop()
    {
        Dispatch(() =>
        {
            lock (_sync)
            {
                _labelCts?.Cancel();
                _labelCts?.Dispose();
                _labelCts = null;
            }

            if (_window is null)
            {
                return;
            }

            _window.Close();
            _window = null;
            _logger.LogInfo("Test overlay stopped");
        });
    }

    public Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return DispatchAsync(() =>
        {
            if (_window is null)
            {
                return;
            }

            var (dx, dy) = direction switch
            {
                VocalAction.MoveUp => (0d, -1d),
                VocalAction.MoveDown => (0d, 1d),
                VocalAction.MoveLeft => (-1d, 0d),
                VocalAction.MoveRight => (1d, 0d),
                _ => (0d, 0d)
            };

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                return;
            }

            _x = Math.Clamp(_x + dx * intensity, 0, Math.Max(0, _window.Width - BallDiameter));
            _y = Math.Clamp(_y + dy * intensity, 0, Math.Max(0, _window.Height - BallDiameter));
            _window.UpdateBall(_x, _y);
        });
    }

    public async Task ShowClickLabelAsync(VocalAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = action switch
        {
            VocalAction.LeftClick => "LEFT",
            VocalAction.RightClick => "RIGHT",
            VocalAction.DoubleClick => "DOUBLE",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        CancellationTokenSource localCts;
        lock (_sync)
        {
            _labelCts?.Cancel();
            _labelCts?.Dispose();
            _labelCts = new CancellationTokenSource();
            localCts = _labelCts;
        }

        try
        {
            await DispatchAsync(() =>
            {
                _window?.SetLabel(text);
            }).ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMilliseconds(800), localCts.Token).ConfigureAwait(false);

            await DispatchAsync(() =>
            {
                _window?.SetLabel(string.Empty);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static Task DispatchAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private sealed class OverlayWindow : Window
    {
        private readonly Canvas _canvas;
        private readonly Ellipse _ball;
        private readonly TextBlock _label;

        public OverlayWindow()
        {
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;

            _canvas = new Canvas
            {
                Width = Width,
                Height = Height,
                IsHitTestVisible = false
            };

            _ball = new Ellipse
            {
                Width = BallDiameter,
                Height = BallDiameter,
                Fill = Brushes.Red,
                Opacity = 0.85
            };

            _label = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                Width = BallDiameter,
                Visibility = Visibility.Collapsed
            };

            _canvas.Children.Add(_ball);
            _canvas.Children.Add(_label);
            Content = _canvas;
        }

        public void UpdateBall(double x, double y)
        {
            Canvas.SetLeft(_ball, x);
            Canvas.SetTop(_ball, y);
            Canvas.SetLeft(_label, x);
            Canvas.SetTop(_label, y + BallRadius - 8);
        }

        public void SetLabel(string text)
        {
            _label.Text = text;
            _label.Visibility = string.IsNullOrWhiteSpace(text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}
