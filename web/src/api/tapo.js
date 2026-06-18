function resolveApiBase() {
  const configured = (import.meta.env.VITE_ONVIF_API_URL ?? '').replace(/\/$/, '')
  if (configured) {
    return configured
  }

  // mpegts.js fetches inside a Web Worker, which cannot resolve relative URLs.
  if (typeof window !== 'undefined' && window.location?.origin) {
    return window.location.origin
  }

  return ''
}

const apiBase = resolveApiBase()

function apiUrl(path) {
  return `${apiBase}${path}`
}

export function hostFromOnvifUri(uri) {
  if (!uri) {
    return null
  }

  try {
    return new URL(uri).hostname
  } catch {
    return null
  }
}

export function resolveTapoHost(tapoHost, onvifUri) {
  const explicit = tapoHost?.trim()
  if (explicit) {
    return explicit
  }

  return hostFromOnvifUri(onvifUri)
}

export async function fetchRecordings(host, date) {
  const params = new URLSearchParams({ host })
  if (date) {
    params.set('date', date)
  }

  const res = await fetch(apiUrl(`/api/tapo/recordings?${params}`))
  if (!res.ok) {
    const payload = await res.json().catch(() => ({}))
    throw new Error(payload.detail || payload.error || `Request failed (${res.status})`)
  }

  return res.json()
}

const LONG_CLIP_MP4_THRESHOLD_SEC = 300

export function recordingDurationSeconds(recording) {
  if (recording.durationSeconds != null && recording.durationSeconds >= 0) {
    return recording.durationSeconds
  }

  if (recording.startTime != null && recording.endTime != null) {
    return Math.max(0, recording.endTime - recording.startTime)
  }

  return 0
}

export function buildPlaybackUrl(host, recording, format) {
  const duration = recordingDurationSeconds(recording)
  const playbackFormat = format ?? (duration > LONG_CLIP_MP4_THRESHOLD_SEC ? 'mp4' : 'ts')
  const params = new URLSearchParams({
    host,
    startTime: String(recording.startTime),
    endTime: String(recording.endTime),
    format: playbackFormat,
  })

  if (recording.deviceLabel) {
    params.set('deviceLabel', recording.deviceLabel)
  }

  if (recording.vedioType != null) {
    params.set('vedioType', String(recording.vedioType))
  }

  return apiUrl(`/api/tapo/playback?${params}`)
}

export async function warmPlaybackConnection(host) {
  if (!host) {
    return
  }

  const params = new URLSearchParams({ host })
  try {
    await fetch(apiUrl(`/api/tapo/playback/warmup?${params}`))
  } catch {
    // Warmup is best-effort; playback still works without it.
  }
}

export function toTapoDate(isoDate) {
  return isoDate.replace(/-/g, '')
}

export function todayIsoDate() {
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function formatRecordingTime(unixSeconds) {
  if (unixSeconds == null) {
    return '—'
  }

  return new Date(unixSeconds * 1000).toLocaleString()
}

export function formatDuration(seconds) {
  if (seconds == null || seconds < 0) {
    return '—'
  }

  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const secs = seconds % 60

  if (hours > 0) {
    return `${hours}h ${minutes}m`
  }

  if (minutes > 0) {
    return `${minutes}m ${secs}s`
  }

  return `${secs}s`
}

export function recordingTypeLabel(vedioType) {
  if (vedioType === 1) {
    return 'Continuous'
  }

  if (vedioType === 2) {
    return 'Detection'
  }

  return vedioType != null ? `Type ${vedioType}` : 'Unknown'
}

const DAY_SECONDS = 86400

export function dayBounds(isoDate) {
  const [year, month, day] = isoDate.split('-').map(Number)
  const dayStart = Math.floor(new Date(year, month - 1, day).getTime() / 1000)
  return { dayStart, dayEnd: dayStart + DAY_SECONDS }
}

export function recordingSegmentStyle(recording, dayStart, dayDuration = DAY_SECONDS) {
  const start = recording.startTime ?? dayStart
  const end = recording.endTime ?? start
  const clampedStart = Math.max(start, dayStart)
  const clampedEnd = Math.min(end, dayStart + dayDuration)
  const leftPct = ((clampedStart - dayStart) / dayDuration) * 100
  const widthPct = Math.max(((clampedEnd - clampedStart) / dayDuration) * 100, 0.3)
  return { leftPct, widthPct }
}

export function findRecordingAtTime(recordings, unixSeconds) {
  if (unixSeconds == null || !recordings?.length) {
    return null
  }

  const matches = recordings.filter((recording) => {
    const start = recording.startTime
    const end = recording.endTime
    if (start == null || end == null) {
      return false
    }
    return unixSeconds >= start && unixSeconds < end
  })

  if (matches.length === 0) {
    return null
  }

  if (matches.length === 1) {
    return matches[0]
  }

  // Overlapping clips: prefer detection over continuous, then the shortest span.
  return [...matches].sort((a, b) => {
    const typeA = a.vedioType === 2 ? 0 : 1
    const typeB = b.vedioType === 2 ? 0 : 1
    if (typeA !== typeB) {
      return typeA - typeB
    }

    const durationA = (a.endTime ?? 0) - (a.startTime ?? 0)
    const durationB = (b.endTime ?? 0) - (b.startTime ?? 0)
    return durationA - durationB
  })[0]
}

export function timeFromTimelinePosition(ratio, dayStart, dayDuration = DAY_SECONDS) {
  const clamped = Math.max(0, Math.min(1, ratio))
  return dayStart + Math.round(clamped * dayDuration)
}

export function formatTimelineHour(unixSeconds) {
  if (unixSeconds == null) {
    return '—'
  }

  return new Date(unixSeconds * 1000).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function formatTimelineTime(unixSeconds) {
  if (unixSeconds == null) {
    return '—'
  }

  return new Date(unixSeconds * 1000).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}

export function timelineRangeWindow(center, spanSeconds, rangeStart, rangeEnd) {
  const half = spanSeconds / 2
  let start = center - half
  let end = center + half

  if (start < rangeStart) {
    end += rangeStart - start
    start = rangeStart
  }

  if (end > rangeEnd) {
    start -= end - rangeEnd
    end = rangeEnd
  }

  start = Math.max(rangeStart, start)
  end = Math.min(rangeEnd, end)

  return {
    start,
    end,
    duration: Math.max(end - start, 1),
  }
}

export function formatTimelineTimeRange(startTime, endTime) {
  if (startTime == null || endTime == null) {
    return '—'
  }

  const start = formatTimelineHour(startTime)
  const end = formatTimelineHour(endTime)
  return `${start} – ${end}`
}

export function timelineHourTicks(dayStart, dayDuration = DAY_SECONDS, count = 7) {
  const ticks = []
  const step = dayDuration / (count - 1)

  for (let index = 0; index < count; index += 1) {
    const time = dayStart + Math.round(step * index)
    ticks.push({
      time,
      label: formatTimelineHour(time),
      leftPct: (index / (count - 1)) * 100,
    })
  }

  return ticks
}

export function isSameRecording(a, b) {
  if (!a || !b) {
    return false
  }

  return (
    a.startTime === b.startTime
    && a.endTime === b.endTime
    && a.deviceLabel === b.deviceLabel
  )
}
