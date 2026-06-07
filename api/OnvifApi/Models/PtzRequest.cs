namespace OnvifApi.Models;

public sealed class PtzRequest : OnvifConnectionRequest
{
    /// <summary>up | down | left | right | stop (required for nudge action)</summary>
    public string? Command { get; set; }

    /// <summary>nudge | move | stop (default: nudge)</summary>
    public string? Action { get; set; }

    /// <summary>Pan velocity direction -1..1 (for move action)</summary>
    public float? Pan { get; set; }

    /// <summary>Tilt velocity direction -1..1 (for move action)</summary>
    public float? Tilt { get; set; }

    /// <summary>Zoom velocity direction -1..1 (for move action)</summary>
    public float? Zoom { get; set; }

    /// <summary>Speed multiplier 0.1..1.0 (default 0.8)</summary>
    public float? Velocity { get; set; }
}
