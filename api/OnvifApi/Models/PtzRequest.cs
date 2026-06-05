namespace OnvifApi.Models;

public sealed class PtzRequest : OnvifConnectionRequest
{
    /// <summary>up | down | left | right | stop</summary>
    public required string Command { get; set; }
}
