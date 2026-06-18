"""Stream Tapo SD-card recording playback as MPEG-TS or fragmented MP4."""

from __future__ import annotations

import asyncio
import json
import os
import subprocess
from collections.abc import AsyncIterator
from json import JSONDecodeError

from pytapo.media_stream._utils import StreamType

from tapo_pool import get_playback_device
from tapo_recordings import TapoError

DEFAULT_STREAM_PORT = 8800
DEFAULT_WINDOW_SIZE = 200
DEFAULT_CHUNK_SECONDS = 300
DEFAULT_STALL_TIMEOUT = 120


def playback_window_size() -> int:
    raw = os.environ.get("TAPO_PLAYBACK_WINDOW_SIZE", str(DEFAULT_WINDOW_SIZE))
    try:
        return max(50, int(raw))
    except ValueError:
        return DEFAULT_WINDOW_SIZE


def playback_chunk_seconds() -> int:
    raw = os.environ.get("TAPO_PLAYBACK_CHUNK_SECONDS", str(DEFAULT_CHUNK_SECONDS))
    try:
        return max(60, int(raw))
    except ValueError:
        return DEFAULT_CHUNK_SECONDS


def playback_stall_timeout() -> float:
    raw = os.environ.get("TAPO_PLAYBACK_STALL_TIMEOUT", str(DEFAULT_STALL_TIMEOUT))
    try:
        return max(10.0, float(raw))
    except ValueError:
        return DEFAULT_STALL_TIMEOUT


def _playback_event_types(vedio_type: int | None) -> list[int]:
    if vedio_type == 1:
        return [1]
    if vedio_type == 2:
        return [2]
    return [1, 2]


def _playback_payload(
    client_id: str,
    start_time: int,
    end_time: int,
    vedio_type: int | None = None,
) -> str:
    return json.dumps(
        {
            "type": "request",
            "seq": 1,
            "params": {
                "playback": {
                    "client_id": client_id,
                    "channels": [0, 1],
                    "scale": "1/1",
                    "start_time": str(start_time),
                    "end_time": str(end_time),
                    "event_type": _playback_event_types(vedio_type),
                },
                "method": "get",
            },
        }
    )


def _is_stream_finished(resp: object) -> bool:
    if getattr(resp, "mimetype", None) != "application/json":
        return False

    try:
        payload = json.loads(resp.plaintext.decode())
    except (JSONDecodeError, AttributeError):
        return False

    params = payload.get("params") if isinstance(payload, dict) else None
    if not isinstance(params, dict):
        return False

    return (
        payload.get("type") == "notification"
        and params.get("event_type") == "stream_status"
        and params.get("status") == "finished"
    )


async def ensure_media_port_open(
    host: str,
    port: int = DEFAULT_STREAM_PORT,
    timeout: float = 5,
) -> None:
    try:
        _, writer = await asyncio.wait_for(
            asyncio.open_connection(host, port),
            timeout=timeout,
        )
        writer.close()
        await writer.wait_closed()
    except Exception as exc:
        raise TapoError(
            f"Cannot reach camera media stream at {host}:{port}. "
            "Ensure port 8800 is forwarded and use the camera LAN IP on your home network."
        ) from exc


async def prepare_playback(
    host: str,
    password_cloud: str,
    device_label: str | None,
) -> tuple[object, str]:
    device, client_id = await asyncio.to_thread(
        get_playback_device,
        host,
        password_cloud,
        device_label,
    )
    await ensure_media_port_open(host)
    return device, client_id


async def _stream_segment(
    device: object,
    client_id: str,
    start_time: int,
    end_time: int,
    vedio_type: int | None,
    *,
    retry: bool = False,
) -> AsyncIterator[bytes]:
    """Stream one contiguous time range from the camera."""
    window_size = 50 if retry else playback_window_size()
    stall_timeout = playback_stall_timeout()
    media_session = device.getMediaSession(StreamType.Download)
    media_session.set_window_size(window_size)

    finished = False

    async with media_session:
        payload = _playback_payload(client_id, start_time, end_time, vedio_type)
        stream = media_session.transceive(payload)

        while True:
            try:
                resp = await asyncio.wait_for(stream.__anext__(), timeout=stall_timeout)
            except StopAsyncIteration:
                break
            except asyncio.TimeoutError:
                break

            if getattr(resp, "mimetype", None) == "video/mp2t":
                yield resp.plaintext
                continue

            if _is_stream_finished(resp):
                finished = True
                break

    if not finished and not retry:
        async for chunk in _stream_segment(
            device,
            client_id,
            start_time,
            end_time,
            vedio_type,
            retry=True,
        ):
            yield chunk


async def _stream_camera_ts(
    device: object,
    client_id: str,
    start_time: int,
    end_time: int,
    vedio_type: int | None = None,
) -> AsyncIterator[bytes]:
    chunk_seconds = playback_chunk_seconds()
    cursor = start_time

    while cursor < end_time:
        segment_end = min(cursor + chunk_seconds, end_time)
        async for chunk in _stream_segment(
            device,
            client_id,
            cursor,
            segment_end,
            vedio_type,
        ):
            yield chunk
        cursor = segment_end


async def stream_playback_ts(
    host: str,
    password_cloud: str,
    start_time: int,
    end_time: int,
    device_label: str | None = None,
    *,
    device: object | None = None,
    client_id: str | None = None,
) -> AsyncIterator[bytes]:
    if end_time <= start_time:
        raise TapoError("endTime must be greater than startTime.")

    if device is None or client_id is None:
        device, client_id = await prepare_playback(host, password_cloud, device_label)

    async for chunk in _stream_camera_ts(device, client_id, start_time, end_time):
        yield chunk


async def stream_playback_chunks(
    device: object,
    client_id: str,
    start_time: int,
    end_time: int,
    *,
    output_format: str = "ts",
    vedio_type: int | None = None,
) -> AsyncIterator[bytes]:
    if output_format == "mp4":
        async for chunk in stream_playback_mp4_chunks(
            device,
            client_id,
            start_time,
            end_time,
            vedio_type=vedio_type,
        ):
            yield chunk
        return

    async for chunk in _stream_camera_ts(
        device,
        client_id,
        start_time,
        end_time,
        vedio_type,
    ):
        yield chunk


async def stream_playback_mp4_chunks(
    device: object,
    client_id: str,
    start_time: int,
    end_time: int,
    *,
    vedio_type: int | None = None,
) -> AsyncIterator[bytes]:
    if end_time <= start_time:
        raise TapoError("endTime must be greater than startTime.")

    ffmpeg = await asyncio.create_subprocess_exec(
        "ffmpeg",
        "-loglevel",
        "error",
        "-probesize",
        "32768",
        "-analyzeduration",
        "0",
        "-fflags",
        "nobuffer+genpts",
        "-f",
        "mpegts",
        "-i",
        "pipe:0",
        "-c:v",
        "copy",
        "-an",
        "-movflags",
        "frag_keyframe+empty_moov+default_base_moof",
        "-f",
        "mp4",
        "pipe:1",
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    if ffmpeg.stdin is None or ffmpeg.stdout is None:
        raise TapoError("Failed to start ffmpeg for playback.")

    async def feed_camera() -> None:
        try:
            async for chunk in _stream_camera_ts(
                device,
                client_id,
                start_time,
                end_time,
                vedio_type,
            ):
                ffmpeg.stdin.write(chunk)
                await ffmpeg.stdin.drain()
        except Exception as exc:
            raise TapoError(str(exc)) from exc
        finally:
            if ffmpeg.stdin and not ffmpeg.stdin.is_closing():
                ffmpeg.stdin.close()

    feed_task = asyncio.create_task(feed_camera())

    try:
        while True:
            chunk = await ffmpeg.stdout.read(65536)
            if not chunk:
                break
            yield chunk
    finally:
        if not feed_task.done():
            feed_task.cancel()
            try:
                await feed_task
            except asyncio.CancelledError:
                pass
            except TapoError:
                pass

        if ffmpeg.returncode is None:
            ffmpeg.terminate()
            try:
                await asyncio.wait_for(ffmpeg.wait(), timeout=5)
            except asyncio.TimeoutError:
                ffmpeg.kill()
                await ffmpeg.wait()

        if feed_task.done() and not feed_task.cancelled():
            exc = feed_task.exception()
            if exc is not None:
                raise TapoError(str(exc)) from exc

        if ffmpeg.returncode not in (0, None, -15):
            stderr = b""
            if ffmpeg.stderr is not None:
                stderr = await ffmpeg.stderr.read()
            detail = stderr.decode("utf-8", errors="replace").strip()
            if detail:
                raise TapoError(detail)


async def stream_playback_mp4(
    host: str,
    password_cloud: str,
    start_time: int,
    end_time: int,
    device_label: str | None = None,
    *,
    device: object | None = None,
    client_id: str | None = None,
) -> AsyncIterator[bytes]:
    if device is None or client_id is None:
        device, client_id = await prepare_playback(host, password_cloud, device_label)

    async for chunk in stream_playback_mp4_chunks(device, client_id, start_time, end_time):
        yield chunk


async def stream_playback(
    host: str,
    password_cloud: str,
    start_time: int,
    end_time: int,
    device_label: str | None = None,
    *,
    output_format: str = "ts",
) -> AsyncIterator[bytes]:
    if output_format == "mp4":
        async for chunk in stream_playback_mp4(
            host,
            password_cloud,
            start_time,
            end_time,
            device_label,
        ):
            yield chunk
        return

    async for chunk in stream_playback_ts(
        host,
        password_cloud,
        start_time,
        end_time,
        device_label,
    ):
        yield chunk


async def warm_playback_connection(
    host: str,
    password_cloud: str,
    device_label: str | None = None,
) -> None:
    await prepare_playback(host, password_cloud, device_label)
