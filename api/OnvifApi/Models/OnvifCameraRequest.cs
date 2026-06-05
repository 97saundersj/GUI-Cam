namespace OnvifApi.Models;

public sealed class OnvifCameraRequest
{
    public required string Host { get; init; }
    public int Port { get; init; } = 80;
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public bool UseHttps { get; init; }
    public string? OnvifUri { get; init; }
}
