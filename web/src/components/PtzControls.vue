<script setup>
import { ref } from 'vue'
import { isPtzConfigured, sendPtz } from '../api/onvif'

const configured = isPtzConfigured()
const open = ref(false)
const busy = ref(false)
const error = ref('')

const directions = [
  { command: 'up', label: 'Up', icon: '↑' },
  { command: 'left', label: 'Left', icon: '←' },
  { command: 'stop', label: 'Stop', icon: '■' },
  { command: 'right', label: 'Right', icon: '→' },
  { command: 'down', label: 'Down', icon: '↓' },
]

function toggle() {
  open.value = !open.value
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
</script>

<template>
  <div v-if="configured" class="ptz-overlay">
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

    <Transition name="ptz-fade">
      <div
        v-if="open"
        id="ptz-controls"
        class="ptz-controls"
        @click.stop
      >
        <div class="ptz-pad" role="group" aria-label="Pan and tilt">
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

        <p v-if="error" class="ptz-error" role="alert">{{ error }}</p>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.ptz-overlay {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 2;
}

.ptz-toggle {
  position: absolute;
  top: 0.85rem;
  left: 0.85rem;
  z-index: 4;
  pointer-events: auto;
  padding: 0.4rem 0.7rem;
  border: 1px solid rgba(255, 255, 255, 0.25);
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(4px);
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  color: #e8f5e4;
  transition: background 0.15s ease;
}

.ptz-toggle:hover,
.ptz-toggle--active {
  background: rgba(74, 124, 89, 0.5);
}

.ptz-controls {
  position: absolute;
  inset: 0;
  z-index: 3;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  pointer-events: none;
}

.ptz-pad {
  display: grid;
  grid-template-columns: repeat(3, clamp(2.25rem, 12vw, 2.75rem));
  grid-template-rows: repeat(3, clamp(2.25rem, 12vw, 2.75rem));
  gap: 0.2rem;
  pointer-events: auto;
}

.ptz-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid rgba(255, 255, 255, 0.28);
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.28);
  color: rgba(255, 255, 255, 0.92);
  font-size: clamp(0.85rem, 4vw, 1rem);
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
  font-size: clamp(0.6rem, 2.8vw, 0.75rem);
  background: rgba(0, 0, 0, 0.38);
}

.ptz-btn--right {
  grid-column: 3;
  grid-row: 2;
}

.ptz-btn--down {
  grid-column: 2;
  grid-row: 3;
}

.ptz-error {
  margin: 0;
  max-width: min(85%, 14rem);
  padding: 0.35rem 0.6rem;
  pointer-events: none;
  text-align: center;
  font-size: 0.7rem;
  line-height: 1.3;
  color: #ffd4d4;
  background: rgba(0, 0, 0, 0.55);
  border-radius: 0.35rem;
}

.ptz-fade-enter-active,
.ptz-fade-leave-active {
  transition: opacity 0.15s ease;
}

.ptz-fade-enter-from,
.ptz-fade-leave-to {
  opacity: 0;
}

.ptz-fade-enter-active .ptz-btn,
.ptz-fade-leave-active .ptz-btn {
  transition: opacity 0.15s ease;
}

.ptz-fade-enter-from .ptz-btn,
.ptz-fade-leave-to .ptz-btn {
  opacity: 0;
}
</style>
