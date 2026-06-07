import { computed, ref } from 'vue'

export const MIN_ZOOM = 1
export const MAX_ZOOM = 4
export const ZOOM_STEP = 0.5

function roundZoom(value) {
  return Math.round(value * 100) / 100
}

function maxPanPercent(zoom) {
  if (zoom <= 1) return 0
  return ((zoom - 1) / zoom) * 50
}

export function useDigitalZoom() {
  const zoom = ref(1)
  const panX = ref(0)
  const panY = ref(0)

  const isZoomed = computed(() => zoom.value > 1)

  const videoStyle = computed(() => {
    if (zoom.value === 1 && panX.value === 0 && panY.value === 0) return {}

    const maxPan = maxPanPercent(zoom.value)
    const x = Math.max(-maxPan, Math.min(maxPan, panX.value))
    const y = Math.max(-maxPan, Math.min(maxPan, panY.value))

    return {
      transform: `scale(${zoom.value}) translate(${x}%, ${y}%)`,
      transformOrigin: 'center center',
    }
  })

  function clampPan() {
    const maxPan = maxPanPercent(zoom.value)
    panX.value = Math.max(-maxPan, Math.min(maxPan, panX.value))
    panY.value = Math.max(-maxPan, Math.min(maxPan, panY.value))
  }

  function setPan(x, y) {
    panX.value = x
    panY.value = y
    clampPan()
  }

  function panBy(dx, dy) {
    panX.value += dx
    panY.value += dy
    clampPan()
  }

  function zoomIn() {
    zoom.value = Math.min(MAX_ZOOM, roundZoom(zoom.value + ZOOM_STEP))
    clampPan()
  }

  function zoomOut() {
    zoom.value = Math.max(MIN_ZOOM, roundZoom(zoom.value - ZOOM_STEP))
    clampPan()
  }

  function resetZoom() {
    zoom.value = 1
    panX.value = 0
    panY.value = 0
  }

  function zoomAtWheel(deltaY) {
    if (deltaY < 0) {
      zoomIn()
    } else if (deltaY > 0) {
      zoomOut()
    }
  }

  return {
    zoom,
    panX,
    panY,
    isZoomed,
    videoStyle,
    setPan,
    panBy,
    zoomIn,
    zoomOut,
    resetZoom,
    zoomAtWheel,
  }
}
