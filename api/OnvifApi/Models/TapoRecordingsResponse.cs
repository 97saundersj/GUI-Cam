namespace OnvifApi.Models;

public sealed class TapoRecordingsResponse
{
    public string Date { get; set; } = "";
    public IReadOnlyList<TapoRecordingItem> Recordings { get; set; } = [];
    public int Total { get; set; }
}
