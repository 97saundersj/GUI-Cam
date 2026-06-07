const apiBase = (import.meta.env.VITE_ONVIF_API_URL ?? '').replace(/\/$/, '')

export function isPtzConfigured() {
  return Boolean(
    import.meta.env.VITE_ONVIF_URI &&
      import.meta.env.VITE_ONVIF_USER &&
      import.meta.env.VITE_ONVIF_PASSWORD,
  )
}

function connectionPayload() {
  return {
    onvifUri: import.meta.env.VITE_ONVIF_URI,
    userName: import.meta.env.VITE_ONVIF_USER,
    password: import.meta.env.VITE_ONVIF_PASSWORD,
  }
}

async function postPtz(body) {
  const res = await fetch(`${apiBase}/api/onvif/ptz`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...connectionPayload(), ...body }),
  })

  if (!res.ok) {
    const payload = await res.json().catch(() => ({}))
    throw new Error(payload.detail || payload.error || `Request failed (${res.status})`)
  }

  return res.json()
}

/** Discrete nudge (backward compatible). */
export function sendPtz(command, velocity) {
  return postPtz({
    action: 'nudge',
    command,
    ...(velocity != null ? { velocity } : {}),
  })
}

/** Start continuous move; call sendPtzStop on release. */
export function sendPtzMove({ pan, tilt, zoom = 0, velocity }) {
  return postPtz({
    action: 'move',
    pan,
    tilt,
    zoom,
    ...(velocity != null ? { velocity } : {}),
  })
}

/** Stop continuous move (fire-and-forget safe). */
export function sendPtzStop() {
  return postPtz({ action: 'stop' })
}
