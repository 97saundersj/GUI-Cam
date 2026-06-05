namespace OnvifApi.Models;

public sealed class OnvifDetailsResponse
{
    public required DeviceInformationDto Device { get; init; }
    public IReadOnlyList<ServiceDto> Services { get; init; } = [];
    public IReadOnlyList<MediaProfileDto> Profiles { get; init; } = [];
}

public sealed class DeviceInformationDto
{
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? FirmwareVersion { get; init; }
    public string? SerialNumber { get; init; }
    public string? HardwareId { get; init; }
}

public sealed class ServiceDto
{
    public string? Namespace { get; init; }
    public string? XAddr { get; init; }
}

public sealed class MediaProfileDto
{
    public string? Token { get; init; }
    public string? Name { get; init; }
    public string? StreamUri { get; init; }
}
