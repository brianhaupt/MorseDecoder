namespace MorseDecoder.App.Dsp;

/// <summary>
/// Efficient single-frequency energy detector.
/// This is used for v0.1 to determine whether a CW tone is present near a target frequency.
/// </summary>
public sealed class GoertzelDetector
{
    private readonly double _sampleRate;
    private readonly double _targetFrequency;
    private readonly int _sampleCount;
    private readonly double _coefficient;

    public GoertzelDetector(double sampleRate, double targetFrequency, int sampleCount)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (targetFrequency <= 0) throw new ArgumentOutOfRangeException(nameof(targetFrequency));
        if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));

        _sampleRate = sampleRate;
        _targetFrequency = targetFrequency;
        _sampleCount = sampleCount;

        var normalizedFrequency = targetFrequency / sampleRate;
        var omega = 2.0 * Math.PI * normalizedFrequency;
        _coefficient = 2.0 * Math.Cos(omega);
    }

    public double GetMagnitude(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
            return 0;

        double q0 = 0;
        double q1 = 0;
        double q2 = 0;

        var count = Math.Min(samples.Length, _sampleCount);

        for (var i = 0; i < count; i++)
        {
            q0 = _coefficient * q1 - q2 + samples[i];
            q2 = q1;
            q1 = q0;
        }

        var power = q1 * q1 + q2 * q2 - _coefficient * q1 * q2;
        var magnitude = Math.Sqrt(Math.Max(0, power)) / count;

        return magnitude;
    }
}
