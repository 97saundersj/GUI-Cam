"""HTTP API for Tapo SD-card recordings."""

import logging
import os

from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import Response, StreamingResponse
from pydantic import BaseModel, Field

from tapo_playback import prepare_playback, stream_playback_chunks, warm_playback_connection
from tapo_recordings import TapoError, list_recordings

app = FastAPI(title="GUI Cam Tapo API", version="1.0.0")
logger = logging.getLogger(__name__)

PASSWORD_CLOUD = os.environ.get("TAPO_PASSWORD_CLOUD", "")

PLAYBACK_MEDIA_TYPES = {
    "ts": "video/mp2t",
    "mp4": "video/mp4",
}


class RecordingsRequest(BaseModel):
    host: str = Field(min_length=1)
    password_cloud: str = Field(min_length=1, alias="passwordCloud")
    date: str | None = None

    model_config = {"populate_by_name": True}


def _resolve_password(password_cloud: str | None) -> str:
    password = password_cloud or PASSWORD_CLOUD
    if not password:
        raise HTTPException(
            status_code=503,
            detail="Tapo cloud password is not configured.",
        )
    return password


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/recordings")
def get_recordings(request: RecordingsRequest) -> dict:
    try:
        return list_recordings(
            request.host,
            request.password_cloud,
            request.date,
        )
    except TapoError as exc:
        logger.warning("Recordings failed for %s: %s", request.host, exc)
        raise HTTPException(status_code=502, detail=str(exc)) from exc


@app.get("/playback/warmup")
async def playback_warmup(
    host: str = Query(min_length=1),
    device_label: str | None = Query(default=None, alias="deviceLabel"),
    password_cloud: str | None = Query(default=None, alias="passwordCloud"),
) -> Response:
    password = _resolve_password(password_cloud)
    try:
        await warm_playback_connection(host, password, device_label)
    except TapoError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    return Response(status_code=204)


@app.get("/playback")
async def playback(
    host: str = Query(min_length=1),
    start_time: int = Query(alias="startTime"),
    end_time: int = Query(alias="endTime"),
    device_label: str | None = Query(default=None, alias="deviceLabel"),
    password_cloud: str | None = Query(default=None, alias="passwordCloud"),
    vedio_type: int | None = Query(default=None, alias="vedioType"),
    format: str = Query(default="ts", pattern="^(ts|mp4)$"),
) -> StreamingResponse:
    password = _resolve_password(password_cloud)

    try:
        device, client_id = await prepare_playback(host, password, device_label)
    except TapoError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc

    return StreamingResponse(
        stream_playback_chunks(
            device,
            client_id,
            start_time,
            end_time,
            output_format=format,
            vedio_type=vedio_type,
        ),
        media_type=PLAYBACK_MEDIA_TYPES[format],
        headers={"Cache-Control": "no-store"},
    )
