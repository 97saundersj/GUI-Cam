# AGENTS.md

## Cursor Cloud specific instructions

GUI Cam is a single product made of four cooperating components. Standard build/run commands live in `README.md`; this section only captures the non-obvious bits for running them in this cloud VM.

### Components & how to run them (dev mode)

| Component | Path | Dev command | Port |
| --- | --- | --- | --- |
| Web frontend (Vue 3 + Vite) | `web/` | `npm run dev --prefix web` | 5173 |
| ONVIF API (.NET 8) | `api/OnvifApi/` | `dotnet run` (in that dir) | 5245 |
| PyTapo service (FastAPI) | `pytapo/` | `pytapo/.venv/bin/uvicorn app:app --host 0.0.0.0 --port 5246` | 5246 |
| Stream converter (MediaMTX) | `Dockerfile` / `docker-compose.yml` | `docker compose up --build` | 8888 |

There is no automated test suite (no vitest/jest, xUnit, or pytest) and no lint script. "Testing" is building each service and exercising it manually. CI (`.github/workflows/deploy.yml`) only builds and deploys the web frontend.

### Non-obvious caveats

- **.NET SDK location:** the SDK is installed at `~/.dotnet` (not on the default PATH for non-login shells). `~/.bashrc` exports `DOTNET_ROOT` and adds it to PATH for interactive shells. In scripts, call `"$HOME/.dotnet/dotnet"` explicitly if PATH is not set.
- **PyTapo runs in a venv** at `pytapo/.venv` (system Python is PEP 668 externally-managed). Use `pytapo/.venv/bin/...` rather than a bare `uvicorn`/`pip`. ffmpeg is required by the playback path and is already present.
- **PyTapo port:** Docker maps host `5246` → container `8000`. When running uvicorn directly (no Docker), bind it to `5246` so the ONVIF API's default `TapoService:BaseUrl` lines up.
- **ONVIF API → PyTapo wiring:** `TapoService:BaseUrl` has NO built-in default; the recordings endpoint returns HTTP 503 ("TapoService:BaseUrl is not configured") until you set it. For local dev run the API with `TapoService__BaseUrl=http://localhost:5246`. The README's "default http://localhost:5246" describes intended config, not a code default.
- **Recordings & PTZ need real hardware:** SD-card recordings additionally require `TapoService__PasswordCloud` (a TP-Link/Tapo cloud account password) plus a physical Tapo camera with an SD card. PTZ and the local RTSP→HLS converter likewise need a real ONVIF/RTSP camera. These external dependencies cannot be exercised in the cloud VM.
- **Live-stream demo without a camera:** the web app's stream URL defaults to a hosted Azure converter (`converterBase` in `web/src/App.vue`). To demo the HLS player locally without that or a camera, point `VITE_STREAM_URL` (and `VITE_STREAM_URL_2`) in `web/.env` at a local HLS playlist (e.g. one generated with `ffmpeg ... -f hls` served from `web/public/`), then restart Vite. Vite only reads `web/.env` at startup, so restart after changing it. `web/.env` is gitignored.
- **Vite proxy:** the dev server proxies `/api` → `http://localhost:5245`, so the frontend reaches the ONVIF API without CORS config. Run the API if you need PTZ/recordings in the UI.
