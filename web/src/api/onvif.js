const apiBase = (import.meta.env.VITE_ONVIF_API_URL ?? '').replace(/\/$/, '')

export function isPtzConfigured() {
  return Boolean(
    import.meta.env.VITE_ONVIF_URI &&
      import.meta.env.VITE_ONVIF_USER &&
      import.meta.env.VITE_ONVIF_PASSWORD,
  )
}

export async function sendPtz(command) {
  const res = await fetch(`${apiBase}/api/onvif/ptz`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      command,
      onvifUri: import.meta.env.VITE_ONVIF_URI,
      userName: import.meta.env.VITE_ONVIF_USER,
      password: import.meta.env.VITE_ONVIF_PASSWORD,
    }),
  })

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error(body.detail || body.error || `Request failed (${res.status})`)
  }

  return res.json()
}
