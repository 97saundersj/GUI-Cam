using OnvifApi.Models;

namespace OnvifApi.Services;

public static class OnvifConnectionResolver
{
    public static OnvifCameraRequest? Resolve(OnvifConnectionRequest? request)
    {
        if (request is null ||
            (string.IsNullOrWhiteSpace(request.Host) && string.IsNullOrWhiteSpace(request.OnvifUri)))
        {
            return null;
        }

        return new OnvifCameraRequest
        {
            Host = request.Host ?? "camera",
            Port = request.Port ?? 80,
            UserName = request.UserName,
            Password = request.Password,
            UseHttps = request.UseHttps ?? false,
            OnvifUri = request.OnvifUri,
        };
    }

    public static string? Validate(OnvifCameraRequest? connection)
    {
        if (connection is null)
        {
            return "Missing camera details. Send onvifUri (or host), userName, and password in the JSON body.";
        }

        if (string.IsNullOrWhiteSpace(connection.UserName) || string.IsNullOrWhiteSpace(connection.Password))
        {
            return "userName and password are required.";
        }

        if (string.IsNullOrWhiteSpace(connection.OnvifUri) && string.IsNullOrWhiteSpace(connection.Host))
        {
            return "onvifUri or host is required.";
        }

        return null;
    }
}
