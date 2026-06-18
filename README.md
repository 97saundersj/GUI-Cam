# GUI Cam

A live webcam site for **GUI**, a crested gecko. Watch the stream in your browser, scrub back through recent footage, and jump back to live with one click.

**Live site:** https://97saundersj.github.io/GUI-Cam/

## Project structure

| Path                 | Description                                              |
| -------------------- | -------------------------------------------------------- |
| `web/`               | Vue 3 frontend (Vite + hls.js)                           |
| `api/OnvifApi/`      | C# Web API — ONVIF device info and stream URIs           |
| `Dockerfile`         | MediaMTX container — converts an RTSP camera feed to HLS |
| `docker-compose.yml` | Runs the stream converter locally                        |
| `pytapo/`            | Tapo SD-card recording API (FastAPI / PyTapo, port 5246)   |

## Local development

```bash
cd web
npm install
npm run dev
```

Open the URL Vite prints (usually http://localhost:5173).

**Pan/tilt controls** — copy `web/.env.example` to `web/.env`, set `VITE_ONVIF_URI`, `VITE_ONVIF_USER`, and `VITE_ONVIF_PASSWORD`, then restart `npm run dev`. When the stream is live, use the **PTZ** button on the video overlay. Run the [ONVIF API](#onvif-api) locally (`dotnet run` in `api/OnvifApi`) — Vite proxies `/api` to port 5245.

For the live GitHub Pages site, set `VITE_ONVIF_API_URL` (repository variable) and `VITE_ONVIF_*` (repository secrets) in GitHub Actions before deploy. Note: `VITE_ONVIF_USER` / `VITE_ONVIF_PASSWORD` are embedded in the built site — only do this if you accept that risk for a home camera.

### Environment variables

**Stream converter** (repo root `.env`, copy from `.env.example`):

| Variable                 | Description                           |
| ------------------------ | ------------------------------------- |
| `MTX_HLSADDRESS`         | HLS listen address (default `:8888`)  |
| `MTX_PATHS_CAM_SOURCE`   | RTSP URL for the camera               |
| `MTX_HLSVARIANT`         | `lowLatency` for LL-HLS (recommended) |
| `MTX_HLSSEGMENTDURATION` | Segment length, e.g. `1s` or `500ms`  |
| `MTX_HLSPARTDURATION`    | LL-HLS part length, e.g. `200ms`      |
| `MTX_HLSALWAYSREMUX`     | `yes` avoids delay on first viewer    |

**Web player** (`web/.env`):

| Variable               | Description                                                             |
| ---------------------- | ----------------------------------------------------------------------- |
| `VITE_STREAM_URL`      | HLS URL (optional; defaults to Azure converter)                         |
| `VITE_HLS_LOW_LATENCY` | `true` (default) — hls.js LL-HLS mode; `false` for longer rewind buffer |

### Reducing latency

Latency stacks across the pipeline. Tune each layer:

1. **MediaMTX** — use the `MTX_HLS*` vars above in repo root `.env` and on **Azure Container Apps** (same env names), then redeploy the converter container.
2. **Camera** — use substream `stream1` in `MTX_PATHS_CAM_SOURCE` for faster encode; shorten keyframe/GOP interval in the Tapo app if available.
3. **Browser** — `VITE_HLS_LOW_LATENCY=true` in `web/.env` (enabled by default in code). Set `false` only if you prefer more rewind buffer over lower delay.
4. **Expectation** — LL-HLS is typically ~2–6 s behind live, not instant. WebRTC would be lower latency but is not wired up here.

After changing `.env` files, restart Docker (`docker compose up --build`) and Vite (`npm run dev`).

## Stream converter

The converter pulls RTSP from your camera and serves HLS at `/cam/index.m3u8`.

```bash
cp .env.example .env   # add your RTSP URL
docker compose up --build
```

HLS stream: http://localhost:8888/cam/index.m3u8

## Tapo recordings

SD-card clips are listed via a [PyTapo](https://github.com/JurajNyiri/pytapo) HTTP service in `pytapo/`. The C# API proxies requests to it.

**Local stack:**

```bash
docker compose up --build    # starts converter (:8888) and pytapo (:5246)
cd api/OnvifApi && dotnet run
```

PyTapo health: http://localhost:5246/health

**List recordings** — `GET /api/tapo/recordings?host=<camera-ip>&date=YYYYMMDD` on the ONVIF API (port 5245). `host` is required; `date` defaults to today UTC. Tapo cloud password is read from server config (`TapoService:PasswordCloud`), not sent by the browser.

Configure the cloud password when running `dotnet run`:

```bash
# PowerShell
$env:TapoService__PasswordCloud = "your-tapo-cloud-password"
```

Or set `TapoService__PasswordCloud` in Azure App Service (see `terraform.tfvars.example`). Set `TapoService:BaseUrl` in `appsettings.json` (default `http://localhost:5246`). On Azure use `TapoService__BaseUrl`.

## ONVIF API

`api/OnvifApi` — .NET 8 Web API for Tapo / ONVIF cameras.

```bash
cd api/OnvifApi
dotnet run
```

Camera credentials are sent in each request body — nothing ONVIF-related is required in `appsettings`.

Swagger (Development): http://localhost:5245/swagger

Both endpoints use the same JSON body for the camera connection:

| Field                  | Description                                                       |
| ---------------------- | ----------------------------------------------------------------- |
| `onvifUri`             | Device service URL (Tapo: `http://&lt;ip&gt;:2020/onvif/service`) |
| `host`                 | Alternative to `onvifUri`                                         |
| `port`                 | Default `80` when using `host`                                    |
| `userName`, `password` | Camera account (required)                                         |
| `https`                | `true` for HTTPS                                                  |

**Details** — `POST /api/onvif` — returns device info, services, profiles, stream URIs.

**PTZ** — `POST /api/onvif/ptz` — same body plus `"command": "up"` (`down`, `left`, `right`, `stop`).

**Tapo recordings** — `GET /api/tapo/recordings?host=<camera-ip>&date=YYYYMMDD` — `host` required; `date` optional. Cloud password in `TapoService:PasswordCloud` on the server.

## Deployment

The site deploys automatically to **GitHub Pages** on every push to `main` via [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml).

## License

Personal project — watch GUI, don't stress the gecko.
