using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using OnvifApi.Models;

namespace OnvifApi.Services;

/// <summary>
/// Reuses ONVIF clients across requests so service discovery runs once per session.
/// </summary>
public sealed class OnvifClientCache : IDisposable
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private bool _disposed;

    private sealed class CacheEntry(NatAwareOnvifClient client) : IDisposable
    {
        public NatAwareOnvifClient Client { get; } = client;
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;

        public void Dispose()
        {
            if (Client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public NatAwareOnvifClient GetClient(OnvifCameraRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EvictStaleEntries();

        var key = BuildKey(request);
        var entry = _entries.AddOrUpdate(
            key,
            _ => new CacheEntry(CreateClient(request)),
            (_, existing) =>
            {
                existing.LastAccess = DateTime.UtcNow;
                return existing;
            });

        entry.LastAccess = DateTime.UtcNow;
        return entry.Client;
    }

    private void EvictStaleEntries()
    {
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var pair in _entries)
        {
            if (pair.Value.LastAccess < cutoff && _entries.TryRemove(pair.Key, out var entry))
            {
                entry.Dispose();
            }
        }
    }

    private static string BuildKey(OnvifCameraRequest request)
    {
        var uri = !string.IsNullOrWhiteSpace(request.OnvifUri)
            ? request.OnvifUri.Trim()
            : BuildDeviceServiceUri(request);

        var raw = $"{uri}|{request.UserName}|{request.Password}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static NatAwareOnvifClient CreateClient(OnvifCameraRequest request)
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var pair in _entries)
        {
            if (_entries.TryRemove(pair.Key, out var entry))
            {
                entry.Dispose();
            }
        }
    }
}
