namespace OnvifApi.Models;

public sealed class TapoRecordingItem
{
    public long? StartTime { get; set; }
    public long? EndTime { get; set; }
    public int? VedioType { get; set; }
    public long? DurationSeconds { get; set; }
    public string? DeviceLabel { get; set; }
}
