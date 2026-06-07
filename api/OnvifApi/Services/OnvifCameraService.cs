using OnvifApi.Models;
using SharpOnvifClient;
using SharpOnvifCommon;

namespace OnvifApi.Services;

public sealed class OnvifCameraService(OnvifClientCache clientCache)
{
    private const string ProfileToken = "profile_1";
    private const float DefaultVelocity = 0.8f;
    private const int NudgeDurationMs = 600;
    private const string ContinuousMoveTimeout = "PT60S";

    public async Task<OnvifDetailsResponse> GetDetailsAsync(OnvifCameraRequest request, CancellationToken cancellationToken = default)
    {
        var client = clientCache.GetClient(request);

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
        PtzRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = clientCache.GetClient(connection);
        var action = (request.Action ?? "nudge").Trim().ToLowerInvariant();
        var velocity = ClampVelocity(request.Velocity ?? DefaultVelocity);

        switch (action)
        {
            case "stop":
                await client.StopAsync(ProfileToken).ConfigureAwait(false);
                return new PtzResponse { Command = "stop", Action = "stop" };

            case "move":
            {
                var pan = ClampAxis(request.Pan ?? 0);
                var tilt = ClampAxis(request.Tilt ?? 0);
                var zoom = ClampAxis(request.Zoom ?? 0);
                if (pan == 0 && tilt == 0 && zoom == 0)
                {
                    throw new ArgumentException("move requires non-zero pan, tilt, or zoom.");
                }

                await client.ContinuousMoveAsync(
                    ProfileToken,
                    pan * velocity,
                    tilt * velocity,
                    zoom * velocity,
                    ContinuousMoveTimeout).ConfigureAwait(false);

                return new PtzResponse { Command = "move", Action = "move" };
            }

            case "nudge":
                return await SendNudgeAsync(client, request, velocity, cancellationToken).ConfigureAwait(false);

            default:
                throw new ArgumentException("Use nudge, move, or stop for action.");
        }
    }

    private static async Task<PtzResponse> SendNudgeAsync(
        SimpleOnvifClient client,
        PtzRequest request,
        float velocity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            throw new ArgumentException("command is required for nudge action.");
        }

        var cmd = request.Command.Trim().ToLowerInvariant();

        switch (cmd)
        {
            case "stop":
                await client.StopAsync(ProfileToken).ConfigureAwait(false);
                break;
            case "up":
                await NudgeAsync(client, pan: 0, tilt: velocity, cancellationToken).ConfigureAwait(false);
                break;
            case "down":
                await NudgeAsync(client, pan: 0, tilt: -velocity, cancellationToken).ConfigureAwait(false);
                break;
            case "left":
                await NudgeAsync(client, pan: -velocity, tilt: 0, cancellationToken).ConfigureAwait(false);
                break;
            case "right":
                await NudgeAsync(client, pan: velocity, tilt: 0, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentException("Use up, down, left, right, or stop.");
        }

        return new PtzResponse { Command = cmd, Action = "nudge" };
    }

    private static async Task NudgeAsync(SimpleOnvifClient client, float pan, float tilt, CancellationToken cancellationToken)
    {
        var timeout = $"PT{NudgeDurationMs / 1000.0:0.###}S";
        await client.ContinuousMoveAsync(ProfileToken, pan, tilt, 0, timeout).ConfigureAwait(false);
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        await client.StopAsync(ProfileToken).ConfigureAwait(false);
    }

    private static float ClampVelocity(float value) => Math.Clamp(value, 0.1f, 1f);

    private static float ClampAxis(float value) => Math.Clamp(value, -1f, 1f);
}
