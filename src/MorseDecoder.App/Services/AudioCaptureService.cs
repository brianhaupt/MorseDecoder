using MorseDecoder.App.Models;
using NAudio.Wave;

namespace MorseDecoder.App.Services;

public sealed class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;

    public event EventHandler<float[]>? SamplesAvailable;
    public event EventHandler<string>? Error;

    public static IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        var devices = new List<AudioInputDevice>();

        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            devices.Add(new AudioInputDevice
            {
                DeviceNumber = i,
                ProductName = caps.ProductName
            });
        }

        return devices;
    }

    public void Start(int deviceNumber, int sampleRate = 8000, int bufferMilliseconds = 40)
    {
        Stop();

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(sampleRate, 16, 1),
                BufferMilliseconds = bufferMilliseconds
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, $"Unable to start audio capture: {ex.Message}");
            Stop();
        }
    }

    public void Stop()
    {
        if (_waveIn is null)
            return;

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;

        try
        {
            _waveIn.StopRecording();
        }
        catch
        {
            // Ignore stop errors during cleanup.
        }

        _waveIn.Dispose();
        _waveIn = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var sampleCount = e.BytesRecorded / 2;
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i * 2);
            samples[i] = sample / 32768f;
        }

        SamplesAvailable?.Invoke(this, samples);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
            Error?.Invoke(this, $"Audio capture stopped: {e.Exception.Message}");
    }

    public void Dispose()
    {
        Stop();
    }
}
