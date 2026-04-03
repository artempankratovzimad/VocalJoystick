using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Audio;

public sealed class NAudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly List<AudioDeviceInfo> _availableDevices;
    private WaveInEvent? _waveIn;
    private readonly object _sync = new();
    private double _currentSignalLevel;

    public NAudioCaptureService()
    {
        _availableDevices = Enumerable.Range(0, WaveInEvent.DeviceCount)
            .Select(index =>
            {
                var caps = WaveInEvent.GetCapabilities(index);
                var id = caps.ProductGuid != Guid.Empty ? caps.ProductGuid.ToString() : index.ToString();
                return new AudioDeviceInfo(id, string.IsNullOrWhiteSpace(caps.ProductName) ? $"Device {index}" : caps.ProductName, index);
            })
            .ToList();

        SelectedDevice = _availableDevices.FirstOrDefault();
    }

    public bool IsCapturing { get; private set; }
    public event EventHandler<AudioBufferEventArgs>? BufferCaptured;
    public event EventHandler<double>? SignalLevelUpdated;

    public IReadOnlyList<AudioDeviceInfo> AvailableDevices => _availableDevices;
    public AudioDeviceInfo? SelectedDevice { get; private set; }
    public double CurrentSignalLevel => _currentSignalLevel;

    public Task SelectDeviceAsync(string? deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectedDevice = _availableDevices.FirstOrDefault(device => device.Id == deviceId) ?? _availableDevices.FirstOrDefault();
        if (SelectedDevice is null)
        {
            return Task.CompletedTask;
        }

        _currentSignalLevel = 0;
        SignalLevelUpdated?.Invoke(this, _currentSignalLevel);
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (IsCapturing || SelectedDevice is null)
            {
                return Task.CompletedTask;
            }

            _waveIn = new WaveInEvent
            {
                DeviceNumber = SelectedDevice.Index,
                BufferMilliseconds = 100,
                WaveFormat = new WaveFormat(16000, 16, 1)
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            IsCapturing = true;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!IsCapturing)
            {
                return Task.CompletedTask;
            }

            _waveIn?.StopRecording();
            IsCapturing = false;
        }

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        var bytesRecorded = args.BytesRecorded;
        var sampleCount = bytesRecorded / 2;
        var samples = new float[sampleCount];
        double sumSquares = 0;

        for (var i = 0; i < sampleCount; i++)
        {
            var index = i * 2;
            if (index + 1 >= args.Buffer.Length)
            {
                break;
            }

            var sample = (short)((args.Buffer[index + 1] << 8) | args.Buffer[index]);
            var normalized = sample / 32768f;
            samples[i] = normalized;
            sumSquares += normalized * normalized;
        }

        var rms = sampleCount > 0 ? Math.Sqrt(sumSquares / sampleCount) : 0d;
        _currentSignalLevel = rms;
        SignalLevelUpdated?.Invoke(this, rms);
        BufferCaptured?.Invoke(this, new AudioBufferEventArgs(new AudioBuffer(samples, _waveIn?.WaveFormat.SampleRate ?? 16000)));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _waveIn?.Dispose();
        _waveIn = null;
        IsCapturing = false;
        _currentSignalLevel = 0;
        SignalLevelUpdated?.Invoke(this, 0);
    }

    public void Dispose()
    {
        _waveIn?.Dispose();
        _waveIn = null;
    }
}
