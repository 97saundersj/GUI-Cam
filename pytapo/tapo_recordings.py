"""Tapo SD-card recording helpers via PyTapo."""

from __future__ import annotations

import os
from datetime import datetime, timezone

from pytapo import Tapo


class TapoError(Exception):
    """Raised when Tapo camera communication fails."""


def resolve_date(date: str | None) -> str:
    if date and date.strip():
        return date.strip()
    return datetime.now(timezone.utc).strftime("%Y%m%d")


def _camera_credentials(password_cloud: str) -> tuple[str, str, str]:
    """Resolve Tapo auth: camera account + cloud password.

  PyTapo needs the camera account from Tapo app (Advanced Settings → Camera account)
  plus your TP-Link cloud password for SD-card access. Set TAPO_USE_CAMERA_ACCOUNT=1
  only when TAPO_CAMERA_USER and TAPO_CAMERA_PASSWORD are your real camera account
  credentials. Otherwise admin + cloud password is used (works on most Tapo firmware).
    """
    use_camera_account = os.environ.get("TAPO_USE_CAMERA_ACCOUNT", "").strip() in {
        "1",
        "true",
        "yes",
    }
    user = os.environ.get("TAPO_CAMERA_USER", "").strip()
    password = os.environ.get("TAPO_CAMERA_PASSWORD", "").strip()

    if use_camera_account and user and password:
        return user, password, password_cloud

    return "admin", password_cloud, password_cloud


def connect_devices(host: str, password_cloud: str) -> tuple[Tapo, dict[str, Tapo]]:
    user, password, cloud_password = _camera_credentials(password_cloud)

    try:
        tapo = Tapo(
            host,
            user,
            password,
            cloud_password,
            printDebugInformation=False,
        )
    except Exception as exc:
        raise TapoError(str(exc)) from exc

    children: dict[str, Tapo] = {}
    try:
        for child in tapo.getChildDevices():
            mac = child["mac"].replace(":", "")
            children[mac] = Tapo(
                host,
                user,
                password,
                cloud_password,
                childID=child["device_id"],
            )
    except Exception:
        pass

    return tapo, children


def _normalize_recording(item: dict, *, device_label: str | None = None) -> dict:
    start = item.get("startTime")
    end = item.get("endTime")
    duration_seconds = None
    if isinstance(start, (int, float)) and isinstance(end, (int, float)):
        duration_seconds = int(end - start)

    recording = {
        "startTime": start,
        "endTime": end,
        "vedioType": item.get("vedio_type"),
        "durationSeconds": duration_seconds,
    }
    if device_label:
        recording["deviceLabel"] = device_label
    return recording


def list_recordings(host: str, password_cloud: str, date: str | None = None) -> dict:
    resolved_date = resolve_date(date)
    tapo, children = connect_devices(host, password_cloud)

    recordings: list[dict] = []
    devices = (
        [(mac, child) for mac, child in children.items()]
        if children
        else [(None, tapo)]
    )

    for device_label, device in devices:
        try:
            results = device.getRecordings(resolved_date)
        except Exception as exc:
            raise TapoError(str(exc)) from exc

        if not results:
            continue

        for entry in results:
            for key in entry:
                recordings.append(
                    _normalize_recording(
                        entry[key],
                        device_label=device_label,
                    )
                )

    recordings.sort(key=lambda item: item.get("startTime") or 0)
    return {
        "date": resolved_date,
        "recordings": recordings,
        "total": len(recordings),
    }
