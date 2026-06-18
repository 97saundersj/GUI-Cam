using System.Net;
using Microsoft.AspNetCore.Mvc;
using OnvifApi.Models;
using OnvifApi.Services;

namespace OnvifApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TapoController(
    TapoRecordingsService tapo,
    ILogger<TapoController> logger) : ControllerBase
{
    /// <summary>List SD-card recordings for a camera and date. Cloud password comes from server config.</summary>
    [HttpGet("recordings")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TapoRecordingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListRecordings(
        [FromQuery] string host,
        [FromQuery] string? date,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return BadRequest(new { error = "host is required." });
        }

        try
        {
            var result = await tapo
                .ListRecordingsAsync(host, date, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Tapo service is not configured");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list Tapo recordings");
            return StatusCode(
                (int)HttpStatusCode.BadGateway,
                new { error = "Unable to reach the Tapo service or camera.", detail = ex.Message });
        }
    }

    /// <summary>Stream an SD-card recording clip as MPEG-TS or MP4.</summary>
    [HttpGet("playback")]
    public async Task Playback(
        [FromQuery] string host,
        [FromQuery] long startTime,
        [FromQuery] long endTime,
        [FromQuery] string? deviceLabel,
        [FromQuery] string format = "ts",
        [FromQuery] int? vedioType = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            await WritePlaybackErrorAsync(
                StatusCodes.Status400BadRequest,
                new { error = "host is required." },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (startTime <= 0 || endTime <= startTime)
        {
            await WritePlaybackErrorAsync(
                StatusCodes.Status400BadRequest,
                new { error = "startTime and endTime are required." },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var playbackFormat = string.Equals(format, "mp4", StringComparison.OrdinalIgnoreCase)
            ? "mp4"
            : "ts";

        try
        {
            using var response = await tapo
                .StreamPlaybackAsync(host, startTime, endTime, deviceLabel, playbackFormat, vedioType, cancellationToken)
                .ConfigureAwait(false);

            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = response.Content.Headers.ContentType?.MediaType
                ?? (playbackFormat == "mp4" ? "video/mp4" : "video/mp2t");
            Response.Headers.CacheControl = "no-store";
            await response.Content
                .CopyToAsync(Response.Body, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Tapo service is not configured");
            await WritePlaybackErrorAsync(
                StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            await WritePlaybackErrorAsync(
                StatusCodes.Status400BadRequest,
                new { error = ex.Message },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stream Tapo recording playback");
            await WritePlaybackErrorAsync(
                StatusCodes.Status502BadGateway,
                new { error = "Unable to play the recording.", detail = ex.Message },
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Pre-authenticate with the camera to speed up the next playback request.</summary>
    [HttpGet("playback/warmup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PlaybackWarmup(
        [FromQuery] string host,
        [FromQuery] string? deviceLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return BadRequest(new { error = "host is required." });
        }

        try
        {
            await tapo.WarmPlaybackAsync(host, deviceLabel, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Tapo service is not configured");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tapo playback warmup failed for {Host}", host);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    private async Task WritePlaybackErrorAsync(
        int statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(payload, cancellationToken).ConfigureAwait(false);
    }
}
