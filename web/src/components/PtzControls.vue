<script setup>
import { ref } from 'vue'
import { isPtzConfigured, sendPtz } from '../api/onvif'
import { MAX_ZOOM, MIN_ZOOM } from '../composables/useDigitalZoom'

defineProps({
  zoom: {
    type: Number,
    default: 1,
  },
})

const configured = isPtzConfigured()
const open = ref(false)
const busy = ref(false)
const error = ref('')

const emit = defineEmits(['open-change', 'zoom-in', 'zoom-out', 'zoom-reset'])

const directions = [
  { command: 'up', label: 'Up', icon: '↑' },
  { command: 'left', label: 'Left', icon: '←' },
  { command: 'stop', label: 'Stop', icon: '■' },
  { command: 'right', label: 'Right', icon: '→' },
  { command: 'down', label: 'Down', icon: '↓' },
]

function toggle() {
  open.value = !open.value
  emit('open-change', open.value)
  if (!open.value) {
    error.value = ''
  }
}

async function move(command) {
  if (busy.value) return

  busy.value = true
  error.value = ''

  try {
    await sendPtz(command)
  } catch (err) {
    error.value = err.message ?? 'PTZ command failed'
  } finally {
    busy.value = false
  }
}
function formatZoom(level) {
  return `${level.toFixed(level % 1 === 0 ? 0 : 1)}×`
}
</script>

<template>
  <div class="ptz">
    <Transition name="ptz-pop">
      <div
        v-if="open"
        id="ptz-controls"
        class="ptz-popover"
        @click.stop
      >
        <div class="ptz-popover-body">
          <div v-if="configured" class="ptz-pad" role="group" aria-label="Pan and tilt">
            <button
              v-for="btn in directions"
              :key="btn.command"
              type="button"
              class="ptz-btn"
              :class="`ptz-btn--${btn.command}`"
              :disabled="busy"
              :aria-label="btn.label"
              @click="move(btn.command)"
            >
              {{ btn.icon }}
            </button>
          </div>

          <div class="zoom-controls" role="group" aria-label="Digital zoom">
            <button
              type="button"
              class="zoom-btn"
              :disabled="zoom >= MAX_ZOOM"
              aria-label="Zoom in"
              @click="emit('zoom-in')"
            >
              +
            </button>
            <span class="zoom-level">{{ formatZoom(zoom) }}</span>
            <button
              type="button"
              class="zoom-btn"
              :disabled="zoom <= MIN_ZOOM"
              aria-label="Zoom out"
              @click="emit('zoom-out')"
            >
              −
            </button>
            <button
              type="button"
              class="zoom-reset"
              :disabled="zoom <= MIN_ZOOM"
              @click="emit('zoom-reset')"
            >
              Reset
            </button>
          </div>
        </div>

        <p v-if="error" class="ptz-error" role="alert">{{ error }}</p>
      </div>
    </Transition>

    <button
      type="button"
      class="ptz-toggle"
      :class="{ 'ptz-toggle--active': open }"
      :aria-expanded="open"
      aria-controls="ptz-controls"
      aria-label="Pan and tilt controls"
      @click.stop="toggle"
    >
      PTZ
    </button>
  </div>
</template>

<style scoped>
.ptz {
  position: relative;
  display: flex;
  align-items: center;
}

.ptz-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 2rem;
  height: 2rem;
  padding: 0 0.45rem;
  border: none;
  border-radius: 0.3rem;
  background: transparent;
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  color: #fff;
  transition: background 0.15s ease;
}

.ptz-toggle:hover,
.ptz-toggle--active {
  background: rgba(74, 124, 89, 0.55);
}

.ptz-popover {
  position: absolute;
  right: 0;
  bottom: calc(100% + 0.55rem);
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.4rem;
  pointer-events: auto;
}

.ptz-popover-body {
  display: flex;
  align-items: stretch;
  gap: 0.35rem;
}

.ptz-pad {
  display: grid;
  grid-template-columns: repeat(3, 2.25rem);
  grid-template-rows: repeat(3, 2.25rem);
  gap: 0.2rem;
  padding: 0.45rem;
  border-radius: 0.5rem;
  background: rgba(0, 0, 0, 0.88);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(255, 255, 255, 0.12);
}

.ptz-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid rgba(255, 255, 255, 0.28);
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.08);
  color: rgba(255, 255, 255, 0.92);
  font-size: 0.85rem;
  line-height: 1;
  -webkit-tap-highlight-color: transparent;
  transition: background 0.12s ease, transform 0.1s ease;
}

.ptz-btn:active:not(:disabled) {
  background: rgba(74, 124, 89, 0.55);
  transform: scale(0.94);
}

.ptz-btn:disabled {
  opacity: 0.45;
}

.ptz-btn--up {
  grid-column: 2;
  grid-row: 1;
}

.ptz-btn--left {
  grid-column: 1;
  grid-row: 2;
}

.ptz-btn--stop {
  grid-column: 2;
  grid-row: 2;
  font-size: 0.65rem;
}

.ptz-btn--right {
  grid-column: 3;
  grid-row: 2;
}

.ptz-btn--down {
  grid-column: 2;
  grid-row: 3;
}

.zoom-controls {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
  padding: 0.45rem 0.4rem;
  border-radius: 0.5rem;
  background: rgba(0, 0, 0, 0.88);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(255, 255, 255, 0.12);
}

.zoom-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  padding: 0;
  border: 1px solid rgba(255, 255, 255, 0.28);
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.08);
  color: #fff;
  font-size: 1rem;
  line-height: 1;
  transition: background 0.12s ease;
}

.zoom-btn:hover:not(:disabled) {
  background: rgba(74, 124, 89, 0.55);
}

.zoom-btn:disabled {
  opacity: 0.35;
}

.zoom-level {
  min-width: 2.25rem;
  padding: 0.15rem 0;
  text-align: center;
  font-size: 0.7rem;
  font-weight: 600;
  color: #fff;
}

.zoom-reset {
  margin-top: 0.1rem;
  padding: 0.2rem 0.35rem;
  border: 1px solid rgba(255, 255, 255, 0.28);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.08);
  font-size: 0.58rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: #fff;
  transition: background 0.12s ease;
}

.zoom-reset:hover:not(:disabled) {
  background: rgba(74, 124, 89, 0.55);
}

.zoom-reset:disabled {
  opacity: 0.35;
  cursor: default;
}

.ptz-error {
  margin: 0;
  max-width: 10rem;
  padding: 0.35rem 0.5rem;
  text-align: center;
  font-size: 0.65rem;
  line-height: 1.3;
  color: #ffd4d4;
  background: rgba(0, 0, 0, 0.88);
  border-radius: 0.35rem;
}

.ptz-pop-enter-active,
.ptz-pop-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}

.ptz-pop-enter-from,
.ptz-pop-leave-to {
  opacity: 0;
  transform: translateY(0.35rem);
}
</style>
