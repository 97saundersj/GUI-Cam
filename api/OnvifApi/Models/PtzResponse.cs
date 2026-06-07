namespace OnvifApi.Models;

public sealed class PtzResponse
{
    public required string Command { get; init; }

    public string? Action { get; init; }
}
