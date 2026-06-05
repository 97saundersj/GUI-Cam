using System.Net;
using Microsoft.AspNetCore.Mvc;
using OnvifApi.Models;
using OnvifApi.Services;

namespace OnvifApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class OnvifController(
    OnvifCameraService onvif,
    ILogger<OnvifController> logger) : ControllerBase
{
    /// <summary>Get device info, services, profiles, and RTSP stream URIs.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OnvifDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> GetDetails([FromBody] OnvifConnectionRequest request, CancellationToken cancellationToken) =>
        RunAsync(
            request,
            (connection, ct) => onvif.GetDetailsAsync(connection, ct),
            cancellationToken,
            "Failed to retrieve ONVIF details",
            "Unable to reach the camera or complete ONVIF negotiation.");

    /// <summary>Pan/tilt: command is up, down, left, right, or stop.</summary>
    [HttpPost("ptz")]
    [ProducesResponseType(typeof(PtzResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<IActionResult> SendPtz([FromBody] PtzRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return Task.FromResult<IActionResult>(BadRequest(new { error = "command is required." }));
        }

        return RunAsync(
            request,
            (connection, ct) => onvif.SendPtzAsync(connection, request.Command, ct),
            cancellationToken,
            "PTZ command failed",
            "PTZ command failed.",
            ex => ex is ArgumentException ? BadRequest(new { error = ex.Message }) : null);
    }

    private async Task<IActionResult> RunAsync<T>(
        OnvifConnectionRequest request,
        Func<OnvifCameraRequest, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        string logMessage,
        string errorMessage,
        Func<Exception, IActionResult?>? mapException = null)
    {
        var connection = OnvifConnectionResolver.Resolve(request);
        var validationError = OnvifConnectionResolver.Validate(connection);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        try
        {
            var result = await action(connection!, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex)
        {
            var mapped = mapException?.Invoke(ex);
            if (mapped is not null)
            {
                return mapped;
            }

            logger.LogError(ex, logMessage);
            return StatusCode(
                (int)HttpStatusCode.BadGateway,
                new { error = errorMessage, detail = ex.Message });
        }
    }
}
