<script setup>
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { isPtzConfigured, sendPtzMove, sendPtzStop } from '../api/onvif'
import { MAX_ZOOM, MIN_ZOOM } from '../composables/useDigitalZoom'
import { usePtzSpeed } from '../composables/usePtzSpeed'
import PtzJoystick from './PtzJoystick.vue'

const props = defineProps({
  zoom: {
    type: Number,
    default: 1,
  },
  ptzEnabled: {
    type: Boolean,
    default: true,
  },
})

const configured = isPtzConfigured()
const open = ref(false)
const moving = ref(false)
const connecting = ref(false)
const error = ref('')
const activeDirection = ref(null)
const popoverRef = ref(null)

const emit = defineEmits([
  'open-change',
  'moving-change',
  'zoom-in',
  'zoom-out',
  'zoom-reset',
])

const { speedLevel, setSpeedLevel, velocity, speedLabels } = usePtzSpeed()

const directions = [
  { command: 'up', label: 'Up', icon: '↑', pan: 0, tilt: 1 },
  { command: 'left', label: 'Left', icon: '←', pan: -1, tilt: 0 },
  { command: 'stop', label: 'Stop', icon: '■', pan: 0, tilt: 0 },
  { command: 'right', label: 'Right', icon: '→', pan: 1, tilt: 0 },
  { command: 'down', label: 'Down', icon: '↓', pan: 0, tilt: -1 },
]

const keyDirections = {
  ArrowUp: { pan: 0, tilt: 1 },
  ArrowDown: { pan: 0, tilt: -1 },
  ArrowLeft: { pan: -1, tilt: 0 },
  ArrowRight: { pan: 1, tilt: 0 },
}

const keysHeld = new Set()
let moveGeneration = 0
let activeMove = { pan: 0, tilt: 0, zoom: 0 }

watch(open, (isOpen) => {
  emit('open-change', isOpen)
  if (!isOpen) {
    error.value = ''
    stopMove()
    keysHeld.clear()
  } else {
    nextTick(() => popoverRef.value?.focus())
  }
})

watch(moving, (isMoving) => {
  emit('moving-change', isMoving)
})

function toggle() {
  open.value = !open.value
}

async function startMove(pan, tilt, zoom = 0) {
  if (!configured) return

  const unchanged =
    moving.value &&
    activeMove.pan === pan &&
    activeMove.tilt === tilt &&
    activeMove.zoom === zoom
  if (unchanged) return

  const wasMoving = moving.value
  activeMove = { pan, tilt, zoom }
  moving.value = true
  error.value = ''
  if (!wasMoving) {
    connecting.value = true
  }

  const generation = ++moveGeneration

  try {
    await sendPtzMove({ pan, tilt, zoom, velocity: velocity.value })
    if (generation === moveGeneration) {
      connecting.value = false
    }
  } catch (err) {
    if (generation === moveGeneration) {
      error.value = err.message ?? 'PTZ command failed'
      moving.value = false
      connecting.value = false
      activeMove = { pan: 0, tilt: 0, zoom: 0 }
    }
  }
}

function stopMove() {
  if (!configured) return

  moveGeneration++
  activeDirection.value = null
  activeMove = { pan: 0, tilt: 0, zoom: 0 }

  if (moving.value) {
    moving.value = false
    connecting.value = false
    sendPtzStop().catch(() => {})
  }
}

function onDirectionDown(btn, event) {
  if (!configured) return
  event.preventDefault()
  event.currentTarget.setPointerCapture(event.pointerId)
  activeDirection.value = btn.command

  if (btn.command === 'stop') {
    stopMove()
    return
  }

  startMove(btn.pan, btn.tilt)
}

function onDirectionUp() {
  stopMove()
}

function onJoystickMove({ pan, tilt }) {
  if (!configured) return
  activeDirection.value = 'joystick'
  startMove(pan, tilt)
}

function onJoystickStop() {
  stopMove()
}

function onKeyDown(event) {
  if (!open.value) return

  if (event.key === ' ') {
    event.preventDefault()
    stopMove()
    return
  }

  if (event.key === '+' || event.key === '=') {
    event.preventDefault()
    emit('zoom-in')
    return
  }

  if (event.key === '-') {
    event.preventDefault()
    emit('zoom-out')
    return
  }

  if (event.key === '0') {
    event.preventDefault()
    emit('zoom-reset')
    return
  }

  const vec = keyDirections[event.key]
  if (vec && props.ptzEnabled && !keysHeld.has(event.key)) {
    event.preventDefault()
    keysHeld.add(event.key)
    activeDirection.value = event.key
    startMove(vec.pan, vec.tilt)
  }
}

function onKeyUp(event) {
  if (!keyDirections[event.key]) return
  keysHeld.delete(event.key)
  if (keysHeld.size === 0) {
    stopMove()
  }
}

function formatZoom(level) {
  return `${level.toFixed(level % 1 === 0 ? 0 : 1)}×`
}

onBeforeUnmount(() => {
  stopMove()
  keysHeld.clear()
})
</script>

<template>
  <div class="ptz">
    <Transition name="ptz-pop">
      <div
        v-if="open"
        id="ptz-controls"
        ref="popoverRef"
        class="ptz-popover"
        tabindex="0"
        @click.stop
        @keydown="onKeyDown"
        @keyup="onKeyUp"
      >
        <div class="ptz-popover-body">
          <div v-if="ptzEnabled" class="ptz-section">
            <span class="ptz-section-label">Camera</span>

            <template v-if="configured">
              <div class="ptz-camera-controls">
                <div
                  class="ptz-pad ptz-pad--desktop"
                  role="group"
                  aria-label="Pan and tilt"
                >
                  <button
                    v-for="btn in directions"
                    :key="btn.command"
                    type="button"
                    class="ptz-btn"
                    :class="[
                      `ptz-btn--${btn.command}`,
                      { 'ptz-btn--active': activeDirection === btn.command },
                    ]"
                    :aria-label="btn.label"
                    @pointerdown="onDirectionDown(btn, $event)"
                    @pointerup="onDirectionUp"
                    @pointercancel="onDirectionUp"
                    @pointerleave="onDirectionUp"
                  >
                    {{ btn.icon }}
                  </button>
                </div>

                <PtzJoystick
                  class="ptz-joystick"
                  @move="onJoystickMove"
                  @stop="onJoystickStop"
                />
              </div>

              <div class="ptz-speed" role="group" aria-label="Movement speed">
                <button
                  v-for="(label, index) in speedLabels"
                  :key="label"
                  type="button"
                  class="ptz-speed-btn"
                  :class="{ 'ptz-speed-btn--active': speedLevel === index }"
                  :aria-label="`${label} speed`"
                  :aria-pressed="speedLevel === index"
                  @click="setSpeedLevel(index)"
                >
                  {{ label }}
                </button>
              </div>

              <p
                class="ptz-status"
                :class="{
                  'ptz-status--pulse': moving && !connecting,
                  'ptz-status--hidden': !(connecting || moving),
                }"
                aria-live="polite"
                :aria-hidden="!(connecting || moving)"
              >
                {{ connecting ? 'Connecting…' : 'Moving…' }}
              </p>
            </template>

            <p v-else class="ptz-hint">Camera pan/tilt not configured</p>
          </div>

          <div class="ptz-section">
            <span class="ptz-section-label">View</span>

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
      aria-label="PTZ controls"
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
  outline: none;
}

.ptz-popover-body {
  display: flex;
  align-items: flex-start;
  gap: 0.35rem;
}

.ptz-section {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.45rem;
  border-radius: 0.5rem;
  background: rgba(0, 0, 0, 0.88);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(255, 255, 255, 0.12);
}

.ptz-section-label {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.58rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(255, 255, 255, 0.55);
}

.ptz-camera-controls {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.ptz-pad {
  display: grid;
  grid-template-columns: repeat(3, 2.25rem);
  grid-template-rows: repeat(3, 2.25rem);
  gap: 0.2rem;
}

.ptz-joystick {
  flex-shrink: 0;
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
  touch-action: none;
  -webkit-tap-highlight-color: transparent;
  transition: background 0.12s ease, transform 0.1s ease, box-shadow 0.12s ease;
}

.ptz-btn--active {
  background: rgba(74, 124, 89, 0.65);
  border-color: rgba(124, 184, 138, 0.7);
  box-shadow: 0 0 8px rgba(74, 124, 89, 0.5);
}

.ptz-btn:active:not(.ptz-btn--stop) {
  transform: scale(0.94);
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

.ptz-speed {
  display: flex;
  gap: 0.2rem;
}

.ptz-speed-btn {
  flex: 1;
  padding: 0.2rem 0.15rem;
  border: 1px solid rgba(255, 255, 255, 0.22);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.06);
  font-size: 0.55rem;
  font-weight: 600;
  letter-spacing: 0.03em;
  color: rgba(255, 255, 255, 0.75);
  transition: background 0.12s ease;
}

.ptz-speed-btn--active {
  background: rgba(74, 124, 89, 0.55);
  border-color: rgba(124, 184, 138, 0.5);
  color: #fff;
}

.ptz-status {
  margin: 0;
  min-height: 0.9rem;
  line-height: 0.9rem;
  font-size: 0.58rem;
  color: rgba(124, 184, 138, 0.9);
  text-align: center;
}

.ptz-status--hidden {
  visibility: hidden;
}

.ptz-status--pulse {
  animation: ptz-pulse 1.2s ease-in-out infinite;
}

@keyframes ptz-pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.45;
  }
}

.ptz-hint {
  margin: 0;
  max-width: 8rem;
  font-size: 0.62rem;
  line-height: 1.35;
  color: rgba(255, 255, 255, 0.55);
  text-align: center;
}

.zoom-controls {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
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
  touch-action: none;
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
  max-width: 14rem;
  padding: 0.35rem 0.5rem;
  text-align: center;
  font-size: 0.65rem;
  line-height: 1.3;
  color: #ffd4d4;
  background: rgba(0, 0, 0, 0.88);
  border-radius: 0.35rem;
  border: 1px solid rgba(255, 100, 100, 0.25);
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

@media (max-width: 480px) {
  .ptz-pad--desktop {
    display: none;
  }

  .ptz-popover-body {
    flex-direction: column;
  }

  .ptz-section {
    width: 100%;
  }

  .zoom-btn {
    width: 3rem;
    height: 3rem;
  }
}

@media (prefers-reduced-motion: reduce) {
  .ptz-status--pulse {
    animation: none;
  }
}
</style>
