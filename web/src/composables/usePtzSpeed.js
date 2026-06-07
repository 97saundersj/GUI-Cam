import { computed, ref } from 'vue'

const STORAGE_KEY = 'ptz-speed'
const SPEED_LEVELS = [0.3, 0.6, 0.9]
export const SPEED_LABELS = ['Slow', 'Med', 'Fast']

function loadLevel() {
  try {
    const stored = Number(localStorage.getItem(STORAGE_KEY))
    if (Number.isInteger(stored) && stored >= 0 && stored < SPEED_LEVELS.length) {
      return stored
    }
  } catch {
    // localStorage may be unavailable
  }
  return 1
}

export function usePtzSpeed() {
  const speedLevel = ref(loadLevel())

  const velocity = computed(() => SPEED_LEVELS[speedLevel.value])

  function setSpeedLevel(level) {
    const clamped = Math.max(0, Math.min(SPEED_LEVELS.length - 1, level))
    speedLevel.value = clamped
    try {
      localStorage.setItem(STORAGE_KEY, String(clamped))
    } catch {
      // ignore
    }
  }

  return {
    speedLevel,
    setSpeedLevel,
    velocity,
    speedLabels: SPEED_LABELS,
    speedLevels: SPEED_LEVELS,
  }
}
