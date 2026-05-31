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
npm run dev
```

Open the URL Vite prints (usually http://localhost:5173).

### Environment variables

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

## License

Personal project — watch GUI, don't stress the gecko.
