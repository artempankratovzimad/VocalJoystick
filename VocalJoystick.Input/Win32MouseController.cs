using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Input;

public sealed class Win32MouseController : IMouseController
{
    private readonly ILogger _logger;

    public Win32MouseController(ILogger logger)
    {
        _logger = logger;
    }

    public Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (dirX, dirY) = ResolveDirection(direction);
        if (dirX == 0 && dirY == 0)
        {
            return Task.CompletedTask;
        }

        if (intensity <= double.Epsilon)
        {
            return Task.CompletedTask;
        }

        var deltaX = (int)Math.Round(dirX * intensity);
        var deltaY = (int)Math.Round(dirY * intensity);
        if (deltaX == 0 && deltaY == 0)
        {
            return Task.CompletedTask;
        }

        var input = CreateMouseInput(deltaX, deltaY, MouseEventFlags.Move);
        if (SendInputChecked(new[] { input }))
        {
            _logger.LogInfo($"Mouse move {direction}: ({deltaX},{deltaY})");
        }

        return Task.CompletedTask;
    }

    public Task ClickAsync(VocalAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MouseEventFlags downFlag;
        MouseEventFlags upFlag;

        switch (action)
        {
            case VocalAction.LeftClick:
                downFlag = MouseEventFlags.LeftDown;
                upFlag = MouseEventFlags.LeftUp;
                break;
            case VocalAction.RightClick:
                downFlag = MouseEventFlags.RightDown;
                upFlag = MouseEventFlags.RightUp;
                break;
            default:
                return Task.CompletedTask;
        }

        var events = new[]
        {
            CreateMouseInput(0, 0, downFlag),
            CreateMouseInput(0, 0, upFlag)
        };

        if (SendInputChecked(events))
        {
            _logger.LogInfo($"Mouse click {action}");
        }
        return Task.CompletedTask;
    }

    public async Task DoubleClickAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var clickSequence = new[]
        {
            CreateMouseInput(0, 0, MouseEventFlags.LeftDown),
            CreateMouseInput(0, 0, MouseEventFlags.LeftUp)
        };

        SendInputChecked(clickSequence);
        await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        SendInputChecked(clickSequence);
        _logger.LogInfo("Mouse double click");
    }

    private static INPUT CreateMouseInput(int dx, int dy, MouseEventFlags flags) => new()
    {
        Type = InputType.Mouse,
        Data = new MOUSEINPUT
        {
            dx = dx,
            dy = dy,
            mouseData = 0,
            dwFlags = flags,
            time = 0,
            dwExtraInfo = IntPtr.Zero
        }
    };

    private bool SendInputChecked(INPUT[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            _logger.LogWarning("Win32 mouse action did not complete");
            return false;
        }

        return true;
    }

    private static (int x, int y) ResolveDirection(VocalAction direction) => direction switch
    {
        VocalAction.MoveUp => (0, -1),
        VocalAction.MoveDown => (0, 1),
        VocalAction.MoveLeft => (-1, 0),
        VocalAction.MoveRight => (1, 0),
        _ => (0, 0)
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public InputType Type;
        public MOUSEINPUT Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public MouseEventFlags dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private enum InputType : uint
    {
        Mouse = 0
    }

    [Flags]
    private enum MouseEventFlags : uint
    {
        Move = 0x0001,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010,
        Absolute = 0x8000
    }
}
