namespace OnvifApi.Models;

public class OnvifConnectionRequest
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool? UseHttps { get; set; }
    /// <summary>Tapo: http://192.168.0.79:2020/onvif/service</summary>
    public string? OnvifUri { get; set; }
}
