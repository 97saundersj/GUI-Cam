using SharpOnvifClient;

namespace OnvifApi.Services;

/// <summary>
/// Rewrites ONVIF service addresses (XAddr) to match the connection URI.
/// Cameras behind NAT return private LAN IPs; remote callers must use the public host/port instead.
/// </summary>
public sealed class NatAwareOnvifClient : SimpleOnvifClient
{
    public NatAwareOnvifClient(string onvifUri, string userName, string password)
        : base(onvifUri, userName, password)
    {
    }

    protected override async Task<string> GetServiceUriAsync(string ns)
    {
        if (_supportedServices is null)
        {
            var services = await GetServicesAsync().ConfigureAwait(false);
            var connectionUri = new Uri(OnvifUri);
            var supportedServices = new Dictionary<string, string>();
            foreach (var service in services.Service)
            {
                supportedServices.Add(
                    service.Namespace.ToLowerInvariant(),
                    RewriteXAddr(service.XAddr, connectionUri));
            }

            _supportedServices = supportedServices;
        }

        if (_supportedServices.TryGetValue(ns, out var uri))
        {
            return uri;
        }

        throw new NotSupportedException($"The device does not support {ns} service!");
    }

    private static string RewriteXAddr(string xaddr, Uri connectionUri)
    {
        if (!Uri.TryCreate(xaddr, UriKind.Absolute, out var serviceUri))
        {
            return xaddr;
        }

        var port = connectionUri.Port;
        if (port < 0)
        {
            port = connectionUri.Scheme == "https" ? 443 : 80;
        }

        var builder = new UriBuilder(serviceUri)
        {
            Host = connectionUri.Host,
            Port = port,
        };
        return builder.Uri.ToString();
    }
}
