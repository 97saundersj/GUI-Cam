using OnvifApi.Models;
using SharpOnvifClient;
using SharpOnvifCommon;

namespace OnvifApi.Services;

public sealed class OnvifCameraService
{
    private const string ProfileToken = "profile_1";
    private const float Velocity = 0.8f;
    private const int DurationMs = 600;

    public async Task<OnvifDetailsResponse> GetDetailsAsync(OnvifCameraRequest request, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(request);

        var deviceInfo = await client.GetDeviceInformationAsync().ConfigureAwait(false);
        var services = await client.GetServicesAsync().ConfigureAwait(false);

        var serviceList = services.Service?
            .Select(s => new ServiceDto { Namespace = s.Namespace, XAddr = s.XAddr })
            .ToList() ?? [];

        var profiles = new List<MediaProfileDto>();
        if (services.Service?.Any(s => s.Namespace == OnvifServices.MEDIA) == true)
        {
            var mediaProfiles = await client.GetProfilesAsync().ConfigureAwait(false);
            foreach (var profile in mediaProfiles.Profiles ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? streamUri = null;
                if (!string.IsNullOrEmpty(profile.token))
                {
                    try
                    {
                        streamUri = (await client.GetStreamUriAsync(profile.token).ConfigureAwait(false))?.Uri;
                    }
                    catch
                    {
                    }
                }

                profiles.Add(new MediaProfileDto
                {
                    Token = profile.token,
                    Name = profile.Name,
                    StreamUri = streamUri,
                });
            }
        }

        return new OnvifDetailsResponse
        {
            Device = new DeviceInformationDto
            {
                Manufacturer = deviceInfo.Manufacturer,
                Model = deviceInfo.Model,
                FirmwareVersion = deviceInfo.FirmwareVersion,
                SerialNumber = deviceInfo.SerialNumber,
                HardwareId = deviceInfo.HardwareId,
            },
            Services = serviceList,
            Profiles = profiles,
        };
    }

    public async Task<PtzResponse> SendPtzAsync(
        OnvifCameraRequest connection,
        string command,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(connection);
        var cmd = command.Trim().ToLowerInvariant();

        switch (cmd)
        {
            case "stop":
                await client.StopAsync(ProfileToken).ConfigureAwait(false);
                break;
            case "up":
                await NudgeAsync(client, pan: 0, tilt: Velocity, cancellationToken).ConfigureAwait(false);
                break;
            case "down":
                await NudgeAsync(client, pan: 0, tilt: -Velocity, cancellationToken).ConfigureAwait(false);
                break;
            case "left":
                await NudgeAsync(client, pan: -Velocity, tilt: 0, cancellationToken).ConfigureAwait(false);
                break;
            case "right":
                await NudgeAsync(client, pan: Velocity, tilt: 0, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentException("Use up, down, left, right, or stop.");
        }

        return new PtzResponse { Command = cmd };
    }

    private static async Task NudgeAsync(SimpleOnvifClient client, float pan, float tilt, CancellationToken cancellationToken)
    {
        var timeout = $"PT{DurationMs / 1000.0:0.###}S";
        await client.ContinuousMoveAsync(ProfileToken, pan, tilt, 0, timeout).ConfigureAwait(false);
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        await client.StopAsync(ProfileToken).ConfigureAwait(false);
    }

    private static SimpleOnvifClient CreateClient(OnvifCameraRequest request)
    {
        var uri = !string.IsNullOrWhiteSpace(request.OnvifUri)
            ? request.OnvifUri.Trim()
            : BuildDeviceServiceUri(request);

        return new NatAwareOnvifClient(uri, request.UserName ?? string.Empty, request.Password ?? string.Empty);
    }

    private static string BuildDeviceServiceUri(OnvifCameraRequest request)
    {
        var scheme = request.UseHttps ? "https" : "http";
        var portSuffix = request.Port is 80 or 443 ? string.Empty : $":{request.Port}";
        return $"{scheme}://{request.Host}{portSuffix}/onvif/device_service";
    }
}
