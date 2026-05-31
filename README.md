# GUI Cam

A live webcam site for **GUI**, a crested gecko. Watch the stream in your browser, scrub back through recent footage, and jump back to live with one click.

**Live site:** https://97saundersj.github.io/GUI-Cam/

## Project structure

| Path | Description |
| --- | --- |
| `web/` | Vue 3 frontend (Vite + hls.js) |
| `Dockerfile` | MediaMTX container — converts an RTSP camera feed to HLS |
| `docker-compose.yml` | Runs the stream converter locally |

## Local development

```bash
cd web
npm install
cp .env.example .env   # optional — set VITE_STREAM_URL
npm run dev
```

Open the URL Vite prints (usually http://localhost:5173).

### Environment variables

**Website** (`web/.env`):

| Variable | Description |
| --- | --- |
| `VITE_STREAM_URL` | HLS playlist URL (e.g. `https://your-converter/cam/index.m3u8`) |

**Stream converter** (repo root `.env`, copy from `.env.example`):

| Variable | Description |
| --- | --- |
| `MTX_HLSADDRESS` | HLS listen address (default `:8888`) |
| `MTX_PATHS_CAM_SOURCE` | RTSP URL for the camera |

## Stream converter

The converter pulls RTSP from your camera and serves HLS at `/cam/index.m3u8`.

```bash
cp .env.example .env   # add your RTSP URL
docker compose up --build
```

HLS stream: http://localhost:8888/cam/index.m3u8

## Deployment

The site deploys automatically to **GitHub Pages** on every push to `main` via [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml).

To override the stream URL in production, add a repository variable:

1. GitHub → **Settings** → **Secrets and variables** → **Actions** → **Variables**
2. Add `VITE_STREAM_URL` with your HLS playlist URL

If unset, the app uses the default stream URL baked into the build.

## License

Personal project — watch GUI, don't stress the gecko.
