"""In-memory cache for Tapo playback device connections."""

from __future__ import annotations

import time
from dataclasses import dataclass
from threading import Lock

from tapo_recordings import connect_devices

POOL_TTL_SEC = 300

_pool: dict[str, _CachedDevice] = {}
_lock = Lock()


@dataclass
class _CachedDevice:
    device: object
    client_id: str
    expires_at: float


def _pool_key(host: str, device_label: str | None) -> str:
    return f"{host}|{device_label or ''}"


def invalidate_playback_device(host: str, device_label: str | None = None) -> None:
    key = _pool_key(host, device_label)
    with _lock:
        _pool.pop(key, None)


def get_playback_device(
    host: str,
    password_cloud: str,
    device_label: str | None,
) -> tuple[object, str]:
    key = _pool_key(host, device_label)
    now = time.monotonic()

    with _lock:
        cached = _pool.get(key)
        if cached is not None and cached.expires_at > now:
            return cached.device, cached.client_id

    try:
        tapo, children = connect_devices(host, password_cloud)
        device = children[device_label] if device_label and device_label in children else tapo
        client_id = device.getUserID()
    except Exception:
        invalidate_playback_device(host, device_label)
        raise

    with _lock:
        _pool[key] = _CachedDevice(
            device=device,
            client_id=client_id,
            expires_at=now + POOL_TTL_SEC,
        )

    return device, client_id
