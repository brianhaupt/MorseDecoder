namespace MorseDecoder.App.Models;

public sealed class AudioInputDevice
{
    public int DeviceNumber { get; init; }
    public string ProductName { get; init; } = string.Empty;

    public override string ToString() => $"{DeviceNumber}: {ProductName}";
}
