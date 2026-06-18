using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OnvifApi.Models;

namespace OnvifApi.Services;

public sealed class TapoServiceOptions
{
    public const string SectionName = "TapoService";

    public string? BaseUrl { get; set; }

    /// <summary>Tapo cloud account password (required for SD-card access).</summary>
    public string? PasswordCloud { get; set; }
}

public sealed class TapoRecordingsService(
    HttpClient http,
    IOptions<TapoServiceOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<TapoRecordingsResponse> ListRecordingsAsync(
        string host,
        string? date,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var baseUrl = settings.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "TapoService:BaseUrl is not configured. Set it in appsettings or TapoService__BaseUrl.");
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("host is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.PasswordCloud))
        {
            throw new InvalidOperationException(
                "TapoService:PasswordCloud is not configured. Set it in appsettings or TapoService__PasswordCloud.");
        }

        using var response = await http.PostAsJsonAsync(
            $"{baseUrl}/recordings",
            new
            {
                host,
                passwordCloud = settings.PasswordCloud,
                date,
            },
            cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content
                .ReadFromJsonAsync<TapoRecordingsResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return payload ?? new TapoRecordingsResponse();
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var detail = TryReadDetail(errorBody) ?? response.ReasonPhrase ?? "Tapo service request failed.";

        throw response.StatusCode switch
        {
            HttpStatusCode.BadGateway => new HttpRequestException(detail),
            HttpStatusCode.BadRequest => new ArgumentException(detail),
            _ => new HttpRequestException($"Tapo service returned {(int)response.StatusCode}: {detail}"),
        };
    }

    public async Task<HttpResponseMessage> StreamPlaybackAsync(
        string host,
        long startTime,
        long endTime,
        string? deviceLabel,
        string format,
        int? vedioType,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var baseUrl = settings.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "TapoService:BaseUrl is not configured. Set it in appsettings or TapoService__BaseUrl.");
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("host is required.");
        }

        if (startTime <= 0 || endTime <= startTime)
        {
            throw new ArgumentException("Invalid startTime or endTime.");
        }

        if (string.IsNullOrWhiteSpace(settings.PasswordCloud))
        {
            throw new InvalidOperationException(
                "TapoService:PasswordCloud is not configured. Set it in appsettings or TapoService__PasswordCloud.");
        }

        var query = new List<string>
        {
            $"host={Uri.EscapeDataString(host)}",
            $"startTime={startTime}",
            $"endTime={endTime}",
            $"format={Uri.EscapeDataString(format)}",
            $"passwordCloud={Uri.EscapeDataString(settings.PasswordCloud)}",
        };

        if (!string.IsNullOrWhiteSpace(deviceLabel))
        {
            query.Add($"deviceLabel={Uri.EscapeDataString(deviceLabel)}");
        }

        if (vedioType is > 0)
        {
            query.Add($"vedioType={vedioType.Value}");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/playback?{string.Join('&', query)}");

        var response = await http
            .SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var detail = TryReadDetail(errorBody) ?? response.ReasonPhrase ?? "Tapo playback request failed.";

        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new HttpRequestException(
                "Tapo playback endpoint was not found. Rebuild the pytapo container: docker compose up --build pytapo"),
            HttpStatusCode.BadGateway => new HttpRequestException(detail),
            HttpStatusCode.BadRequest => new ArgumentException(detail),
            HttpStatusCode.ServiceUnavailable => new InvalidOperationException(detail),
            _ => new HttpRequestException($"Tapo service returned {(int)response.StatusCode}: {detail}"),
        };
    }

    public async Task WarmPlaybackAsync(
        string host,
        string? deviceLabel,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var baseUrl = settings.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "TapoService:BaseUrl is not configured. Set it in appsettings or TapoService__BaseUrl.");
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("host is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.PasswordCloud))
        {
            throw new InvalidOperationException(
                "TapoService:PasswordCloud is not configured. Set it in appsettings or TapoService__PasswordCloud.");
        }

        var query = new List<string>
        {
            $"host={Uri.EscapeDataString(host)}",
            $"passwordCloud={Uri.EscapeDataString(settings.PasswordCloud)}",
        };

        if (!string.IsNullOrWhiteSpace(deviceLabel))
        {
            query.Add($"deviceLabel={Uri.EscapeDataString(deviceLabel)}");
        }

        using var response = await http
            .GetAsync($"{baseUrl}/playback/warmup?{string.Join('&', query)}", cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var detail = TryReadDetail(errorBody) ?? response.ReasonPhrase ?? "Tapo warmup request failed.";

        throw response.StatusCode switch
        {
            HttpStatusCode.BadGateway => new HttpRequestException(detail),
            HttpStatusCode.BadRequest => new ArgumentException(detail),
            HttpStatusCode.ServiceUnavailable => new InvalidOperationException(detail),
            _ => new HttpRequestException($"Tapo service returned {(int)response.StatusCode}: {detail}"),
        };
    }

    private static string? TryReadDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }
}
