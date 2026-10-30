"""Parse camera API and playback endpoints (possibly on different hosts)."""

from __future__ import annotations

from dataclasses import dataclass

DEFAULT_API_PORT = 443
DEFAULT_PLAYBACK_PORT = 8800


class InvalidCameraTarget(ValueError):
    """Raised when a camera host or port string is invalid."""


@dataclass(frozen=True)
class CameraEndpoint:
    host: str
    port: int


@dataclass(frozen=True)
class CameraTarget:
    host: str
    api_port: int = DEFAULT_API_PORT
    playback_port: int = DEFAULT_PLAYBACK_PORT
    playback_host: str | None = None

    @property
    def media_host(self) -> str:
        return self.playback_host or self.host


def _parse_port(raw: str, label: str) -> int:
    try:
        port = int(raw)
    except ValueError as exc:
        raise InvalidCameraTarget(f"Invalid {label} port: {raw}") from exc
    if port < 1 or port > 65535:
        raise InvalidCameraTarget(f"Invalid {label} port: {raw}")
    return port


def parse_endpoint(raw: str, default_port: int, label: str) -> CameraEndpoint:
    """Parse ``host`` or ``host:port``. IPv6 as ``[2001:db8::1]:port``."""
    value = (raw or "").strip()
    if not value:
        raise InvalidCameraTarget(f"{label} host is required.")

    if value.startswith("["):
        end = value.find("]")
        if end == -1:
            raise InvalidCameraTarget(f"Invalid {label} host: {raw}")
        host = value[1:end]
        rest = value[end + 1 :]
        if not rest:
            return CameraEndpoint(host=host, port=default_port)
        if not rest.startswith(":"):
            raise InvalidCameraTarget(f"Invalid {label} host: {raw}")
        return CameraEndpoint(host=host, port=_parse_port(rest[1:], label))

    parts = value.split(":")
    if len(parts) == 1:
        return CameraEndpoint(host=parts[0], port=default_port)
    if len(parts) == 2 and parts[1].isdigit():
        return CameraEndpoint(host=parts[0], port=_parse_port(parts[1], label))
    if parts[-1].isdigit():
        return CameraEndpoint(host=":".join(parts[:-1]), port=_parse_port(parts[-1], label))
    return CameraEndpoint(host=value, port=default_port)


def parse_camera_target(raw: str) -> CameraTarget:
    """Parse a camera target.

    Formats:
    - ``host``
    - ``host:apiPort``
    - ``host:apiPort:playbackPort`` (same host)
    - ``apiHost:apiPort|playbackHost:playbackPort`` (playit-style split tunnels)
    """
    value = (raw or "").strip()
    if not value:
        raise InvalidCameraTarget("host is required.")

    if "|" in value:
        api_raw, playback_raw = value.split("|", 1)
        api = parse_endpoint(api_raw, DEFAULT_API_PORT, "API")
        playback = parse_endpoint(playback_raw, DEFAULT_PLAYBACK_PORT, "playback")
        return CameraTarget(
            host=api.host,
            api_port=api.port,
            playback_host=playback.host,
            playback_port=playback.port,
        )

    extra: list[str] = []

    if value.startswith("["):
        end = value.find("]")
        if end == -1:
            raise InvalidCameraTarget(f"Invalid camera host: {raw}")
        host = value[1:end]
        rest = value[end + 1 :]
        if rest.startswith(":"):
            extra = rest[1:].split(":")
        elif rest:
            raise InvalidCameraTarget(f"Invalid camera host: {raw}")
    else:
        parts = value.split(":")
        if len(parts) <= 3:
            host = parts[0]
            extra = parts[1:]
        else:
            extra = []
            core = list(parts)
            while core and len(extra) < 2 and core[-1].isdigit():
                extra.insert(0, core.pop())
            host = ":".join(core)

    if not host:
        raise InvalidCameraTarget(f"Invalid camera host: {raw}")
    if len(extra) > 2 or any(part == "" for part in extra):
        raise InvalidCameraTarget(
            "Camera host must be host, host:apiPort, host:apiPort:playbackPort, "
            "or apiHost:apiPort|playbackHost:playbackPort."
        )

    api_port = DEFAULT_API_PORT
    playback_port = DEFAULT_PLAYBACK_PORT
    if extra:
        api_port = _parse_port(extra[0], "API")
    if len(extra) == 2:
        playback_port = _parse_port(extra[1], "playback")

    return CameraTarget(host=host, api_port=api_port, playback_port=playback_port)


def bind_playback_target(device: object, target: CameraTarget) -> None:
    """Point PyTapo's media session at the playback host/port."""
    device.streamPort = target.playback_port
    original = device.getMediaSession

    def get_media_session(*args, **kwargs):
        session = original(*args, **kwargs)
        session.ip = target.media_host
        session.port = target.playback_port
        return session

    device.getMediaSession = get_media_session
