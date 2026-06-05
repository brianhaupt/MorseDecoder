using System.Diagnostics;
using System.Windows;
using MorseDecoder.App.Dsp;
using MorseDecoder.App.Models;
using MorseDecoder.App.Services;

namespace MorseDecoder.App;

public partial class MainWindow : Window
{
    private const int SampleRate = 8000;
    private const int DetectorSampleCount = 320;

    private readonly AudioCaptureService _audioCapture = new();
    private readonly Stopwatch _runClock = new();

    private GoertzelDetector? _detector;
    private bool _toneActive;
    private long _lastTransitionMs;
    private double _smoothedLevel;
    private double _threshold = 0.015;

    public MainWindow()
    {
        InitializeComponent();

        _audioCapture.SamplesAvailable += AudioCapture_SamplesAvailable;
        _audioCapture.Error += AudioCapture_Error;

        LoadDevices();
    }

    private void LoadDevices()
    {
        var devices = AudioCaptureService.GetInputDevices();
        DeviceCombo.ItemsSource = devices;

        if (devices.Count > 0)
            DeviceCombo.SelectedIndex = 0;

        if (devices.Count == 0)
            AppendLog("No audio input devices were found.");
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceCombo.SelectedItem is not AudioInputDevice selectedDevice)
        {
            MessageBox.Show("Please select an audio input device.", "No Audio Device", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(FrequencyText.Text, out var frequency) || frequency <= 0 || frequency >= SampleRate / 2)
        {
            MessageBox.Show($"Enter a frequency between 1 Hz and {SampleRate / 2 - 1} Hz.", "Invalid Frequency", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(ThresholdText.Text, out _threshold) || _threshold <= 0)
        {
            MessageBox.Show("Enter a positive detection threshold.", "Invalid Threshold", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _detector = new GoertzelDetector(SampleRate, frequency, DetectorSampleCount);
        _toneActive = false;
        _lastTransitionMs = 0;
        _smoothedLevel = 0;

        _runClock.Restart();
        _audioCapture.Start(selectedDevice.DeviceNumber, SampleRate);

        StatusText.Text = $"Running - {frequency:0.0} Hz";
        ToneText.Text = "Tone: OFF";
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;

        AppendLog($"Started. Device={selectedDevice.ProductName}, Target={frequency:0.0} Hz, Threshold={_threshold:0.0000}");
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopCapture();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadDevices();
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        LogText.Clear();
    }

    private void AudioCapture_SamplesAvailable(object? sender, float[] samples)
    {
        var detector = _detector;
        if (detector is null)
            return;

        var magnitude = detector.GetMagnitude(samples);

        // Smooth the signal level so threshold crossings are less jittery.
        _smoothedLevel = (_smoothedLevel * 0.75) + (magnitude * 0.25);

        var isToneNow = _smoothedLevel >= _threshold;
        var nowMs = _runClock.ElapsedMilliseconds;

        Dispatcher.Invoke(() =>
        {
            LevelBar.Value = Math.Min(LevelBar.Maximum, _smoothedLevel);
            LevelText.Text = $"Level: {_smoothedLevel:0.0000}";

            if (isToneNow != _toneActive)
            {
                var previousState = _toneActive ? "ON " : "OFF";
                var newState = isToneNow ? "ON " : "OFF";
                var duration = nowMs - _lastTransitionMs;

                _toneActive = isToneNow;
                _lastTransitionMs = nowMs;

                ToneText.Text = $"Tone: {(isToneNow ? "ON" : "OFF")}";
                AppendLog($"{nowMs,8} ms | {previousState} lasted {duration,5} ms | Tone {newState}");
            }
        });
    }

    private void AudioCapture_Error(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            AppendLog($"ERROR: {message}");
            StopCapture();
        });
    }

    private void StopCapture()
    {
        _audioCapture.Stop();
        _runClock.Stop();

        StatusText.Text = "Stopped";
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;

        AppendLog("Stopped.");
    }

    private void AppendLog(string message)
    {
        LogText.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        LogText.ScrollToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        _audioCapture.Dispose();
        base.OnClosed(e);
    }
}
