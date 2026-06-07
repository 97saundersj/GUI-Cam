import { computed, ref } from 'vue'

export const MIN_ZOOM = 1
export const MAX_ZOOM = 4
export const ZOOM_STEP = 0.5

function roundZoom(value) {
  return Math.round(value * 100) / 100
}

export function useDigitalZoom() {
  const zoom = ref(1)

  const isZoomed = computed(() => zoom.value > 1)

  const videoStyle = computed(() => {
    if (zoom.value === 1) return {}
    return {
      transform: `scale(${zoom.value})`,
      transformOrigin: 'center center',
    }
  })

  function zoomIn() {
    zoom.value = Math.min(MAX_ZOOM, roundZoom(zoom.value + ZOOM_STEP))
  }

  function zoomOut() {
    zoom.value = Math.max(MIN_ZOOM, roundZoom(zoom.value - ZOOM_STEP))
  }

  function resetZoom() {
    zoom.value = 1
  }

  return {
    zoom,
    isZoomed,
    videoStyle,
    zoomIn,
    zoomOut,
    resetZoom,
  }
}
