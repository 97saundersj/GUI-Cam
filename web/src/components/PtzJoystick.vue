<script setup>
import { onBeforeUnmount, ref } from 'vue'

const THROTTLE_MS = 100
const DEAD_ZONE = 0.12

const emit = defineEmits(['move', 'stop'])

const padRef = ref(null)
const thumbX = ref(0)
const thumbY = ref(0)
const active = ref(false)

let pointerId = null
let throttleTimer = null
let lastEmit = { pan: 0, tilt: 0 }

function padRect() {
  return padRef.value?.getBoundingClientRect()
}

function pointerToOffset(clientX, clientY) {
  const rect = padRect()
  if (!rect) return { x: 0, y: 0 }

  const radius = rect.width / 2
  const cx = rect.left + radius
  const cy = rect.top + radius
  let x = (clientX - cx) / radius
  let y = (clientY - cy) / radius

  const dist = Math.hypot(x, y)
  if (dist > 1) {
    x /= dist
    y /= dist
  }

  return { x, y: -y }
}

function emitMove(x, y) {
  const dist = Math.hypot(x, y)
  if (dist < DEAD_ZONE) {
    if (lastEmit.pan !== 0 || lastEmit.tilt !== 0) {
      lastEmit = { pan: 0, tilt: 0 }
      emit('stop')
    }
    return
  }

  const pan = x
  const tilt = y
  if (pan === lastEmit.pan && tilt === lastEmit.tilt) return

  lastEmit = { pan, tilt }
  emit('move', { pan, tilt })
}

function scheduleEmit(x, y) {
  if (throttleTimer) return
  throttleTimer = setTimeout(() => {
    throttleTimer = null
    emitMove(x, y)
  }, THROTTLE_MS)
  emitMove(x, y)
}

function onPointerDown(event) {
  event.preventDefault()
  active.value = true
  pointerId = event.pointerId
  padRef.value?.setPointerCapture(pointerId)

  const { x, y } = pointerToOffset(event.clientX, event.clientY)
  thumbX.value = x
  thumbY.value = y
  scheduleEmit(x, y)
}

function onPointerMove(event) {
  if (!active.value || event.pointerId !== pointerId) return

  const { x, y } = pointerToOffset(event.clientX, event.clientY)
  thumbX.value = x
  thumbY.value = y
  scheduleEmit(x, y)
}

function endInteraction() {
  if (!active.value) return

  active.value = false
  pointerId = null
  thumbX.value = 0
  thumbY.value = 0
  lastEmit = { pan: 0, tilt: 0 }

  if (throttleTimer) {
    clearTimeout(throttleTimer)
    throttleTimer = null
  }

  emit('stop')
}

function onPointerUp(event) {
  if (event.pointerId !== pointerId) return
  endInteraction()
}

function onPointerCancel(event) {
  if (event.pointerId !== pointerId) return
  endInteraction()
}

onBeforeUnmount(() => {
  if (throttleTimer) clearTimeout(throttleTimer)
})
</script>

<template>
  <div
    ref="padRef"
    class="joystick"
    :class="{ 'joystick--active': active }"
    role="group"
    aria-label="Pan and tilt joystick"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerCancel"
    @pointerleave="onPointerUp"
  >
    <div class="joystick-ring" />
    <div
      class="joystick-thumb"
      :style="{
        transform: `translate(calc(-50% + ${thumbX * 42}%), calc(-50% + ${-thumbY * 42}%))`,
      }"
    />
  </div>
</template>

<style scoped>
.joystick {
  position: relative;
  width: 5rem;
  height: 5rem;
  touch-action: none;
  cursor: pointer;
  -webkit-tap-highlight-color: transparent;
}

.joystick-ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  border: 1px solid rgba(255, 255, 255, 0.28);
  background: rgba(255, 255, 255, 0.06);
}

.joystick--active .joystick-ring {
  border-color: rgba(74, 124, 89, 0.65);
  background: rgba(74, 124, 89, 0.15);
}

.joystick-thumb {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 2rem;
  height: 2rem;
  border-radius: 50%;
  border: 1px solid rgba(255, 255, 255, 0.35);
  background: rgba(255, 255, 255, 0.2);
  transition: background 0.12s ease;
  pointer-events: none;
}

.joystick--active .joystick-thumb {
  background: rgba(74, 124, 89, 0.75);
  border-color: rgba(124, 184, 138, 0.8);
}

@media (max-width: 480px) {
  .joystick {
    width: 5.5rem;
    height: 5.5rem;
  }

  .joystick-thumb {
    width: 2.25rem;
    height: 2.25rem;
  }
}

@media (prefers-reduced-motion: reduce) {
  .joystick-thumb {
    transition: none;
  }
}
</style>
