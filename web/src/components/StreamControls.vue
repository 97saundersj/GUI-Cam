<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import PtzControls from './PtzControls.vue'

const props = defineProps({
  videoEl: {
    type: Object,
    default: null,
  },
  frameEl: {
    type: Object,
    default: null,
  },
  isAtLiveEdge: {
    type: Boolean,
    default: true,
  },
  isBuffering: {
    type: Boolean,
    default: false,
  },
  zoom: {
    type: Number,
    default: 1,
  },
  ptzEnabled: {
    type: Boolean,
    default: true,
  },
})

const emit = defineEmits(['go-live', 'zoom-in', 'zoom-out', 'zoom-reset'])

const visible = ref(true)
const isPlaying = ref(false)
const isMuted = ref(true)
const isFullscreen = ref(false)
const seekableStart = ref(0)
const seekableEnd = ref(0)
const currentTime = ref(0)
const scrubbing = ref(false)
const ptzOpen = ref(false)
const ptzMoving = ref(false)

let hideTimer = null

const hasSeekableRange = computed(
  () => seekableEnd.value > seekableStart.value,
)

const timelineProgress = computed(() => {
  if (!hasSeekableRange.value) return 100
  const span = seekableEnd.value - seekableStart.value
  return ((currentTime.value - seekableStart.value) / span) * 100
})

function scheduleHide() {
  clearTimeout(hideTimer)
  if (!isPlaying.value || scrubbing.value || ptzOpen.value || ptzMoving.value || props.isBuffering) {
    visible.value = true
    return
  }
  hideTimer = setTimeout(() => {
    visible.value = false
  }, 2800)
}

function revealControls() {
  visible.value = true
  scheduleHide()
}

function onPlayStateChange() {
  const video = props.videoEl
  if (!video) return
  isPlaying.value = !video.paused && !video.ended
  isMuted.value = video.muted
  scheduleHide()
}

function updateTimeline() {
  const video = props.videoEl
  if (!video || video.seekable.length === 0) return

  seekableStart.value = video.seekable.start(0)
  seekableEnd.value = video.seekable.end(video.seekable.length - 1)
  if (!scrubbing.value) {
    currentTime.value = video.currentTime
  }
}

function togglePlay() {
  const video = props.videoEl
  if (!video) return

  if (video.paused) {
    video.play().catch(() => {})
  } else {
    video.pause()
  }
  revealControls()
}

function toggleMute() {
  const video = props.videoEl
  if (!video) return
  video.muted = !video.muted
  isMuted.value = video.muted
  revealControls()
}

function onSeekInput(event) {
  scrubbing.value = true
  currentTime.value = Number(event.target.value)
}

function onSeekChange(event) {
  const video = props.videoEl
  if (!video) return

  video.currentTime = Number(event.target.value)
  scrubbing.value = false
  revealControls()
}

function onGoLiveClick() {
  emit('go-live')
  revealControls()
}

async function toggleFullscreen() {
  const frame = props.frameEl
  if (!frame) return

  try {
    if (!document.fullscreenElement) {
      await frame.requestFullscreen()
    } else {
      await document.exitFullscreen()
    }
  } catch {
    // Fullscreen may be blocked by the browser.
  }
  revealControls()
}

function onFullscreenChange() {
  isFullscreen.value = Boolean(document.fullscreenElement)
}

function bindVideo(video) {
  if (!video) return

  video.addEventListener('play', onPlayStateChange)
  video.addEventListener('pause', onPlayStateChange)
  video.addEventListener('volumechange', onPlayStateChange)
  video.addEventListener('timeupdate', updateTimeline)
  video.addEventListener('loadedmetadata', updateTimeline)
  video.addEventListener('progress', updateTimeline)
  onPlayStateChange()
  updateTimeline()
}

function unbindVideo(video) {
  if (!video) return

  video.removeEventListener('play', onPlayStateChange)
  video.removeEventListener('pause', onPlayStateChange)
  video.removeEventListener('volumechange', onPlayStateChange)
  video.removeEventListener('timeupdate', updateTimeline)
  video.removeEventListener('loadedmetadata', updateTimeline)
  video.removeEventListener('progress', updateTimeline)
}

watch(
  () => props.videoEl,
  (video, previous) => {
    unbindVideo(previous)
    bindVideo(video)
  },
)

watch(isPlaying, scheduleHide)
watch(ptzOpen, scheduleHide)
watch(ptzMoving, scheduleHide)
watch(() => props.isBuffering, scheduleHide)

onMounted(() => {
  bindVideo(props.videoEl)
  document.addEventListener('fullscreenchange', onFullscreenChange)
  scheduleHide()
})

onBeforeUnmount(() => {
  unbindVideo(props.videoEl)
  document.removeEventListener('fullscreenchange', onFullscreenChange)
  clearTimeout(hideTimer)
})

defineExpose({ revealControls })
</script>

<template>
  <div
    class="controls"
    :class="{ 'controls--hidden': !visible }"
    @click.stop
    @pointerdown.stop
  >
    <input
      v-if="hasSeekableRange"
      class="controls-timeline"
      type="range"
      :min="seekableStart"
      :max="seekableEnd"
      :step="0.1"
      :value="currentTime"
      :style="{ '--progress': `${timelineProgress}%` }"
      aria-label="Stream timeline"
      @input="onSeekInput"
      @change="onSeekChange"
      @pointerdown="scrubbing = true"
      @pointerup="scrubbing = false"
    />

    <div class="controls-row">
      <button
        type="button"
        class="ctrl-btn"
        :aria-label="isPlaying ? 'Pause' : 'Play'"
        @click="togglePlay"
      >
        <span v-if="isPlaying" aria-hidden="true">❚❚</span>
        <span v-else aria-hidden="true">▶</span>
      </button>

      <div class="controls-spacer" />

      <button
        v-if="!isAtLiveEdge"
        type="button"
        class="go-live-btn"
        @click="onGoLiveClick"
      >
        Go live
      </button>

      <div v-else class="live-badge" aria-label="Live">
        <span class="live-dot" />
        Live
      </div>

      <PtzControls
        class="controls-ptz"
        :zoom="zoom"
        :ptz-enabled="ptzEnabled"
        @open-change="ptzOpen = $event"
        @moving-change="ptzMoving = $event"
        @zoom-in="emit('zoom-in')"
        @zoom-out="emit('zoom-out')"
        @zoom-reset="emit('zoom-reset')"
      />

      <button
        type="button"
        class="ctrl-btn"
        :aria-label="isMuted ? 'Unmute' : 'Mute'"
        @click="toggleMute"
      >
        <span aria-hidden="true">{{ isMuted ? '🔇' : '🔊' }}</span>
      </button>

      <button
        type="button"
        class="ctrl-btn"
        :aria-label="isFullscreen ? 'Exit fullscreen' : 'Fullscreen'"
        @click="toggleFullscreen"
      >
        <span aria-hidden="true">{{ isFullscreen ? '⤡' : '⤢' }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.controls {
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  z-index: 5;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 2rem 0.65rem 0.55rem;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.82), transparent);
  transition: opacity 0.22s ease, transform 0.22s ease;
}

.controls--hidden {
  opacity: 0;
  transform: translateY(0.35rem);
  pointer-events: none;
}

.controls-timeline {
  width: 100%;
  height: 0.28rem;
  margin: 0;
  appearance: none;
  border-radius: 999px;
  background: linear-gradient(
    to right,
    #ff4d4d 0%,
    #ff4d4d var(--progress),
    rgba(255, 255, 255, 0.22) var(--progress),
    rgba(255, 255, 255, 0.22) 100%
  );
  cursor: pointer;
}

.controls-timeline::-webkit-slider-thumb {
  appearance: none;
  width: 0.85rem;
  height: 0.85rem;
  border: none;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 0 0 2px rgba(0, 0, 0, 0.35);
}

.controls-timeline::-moz-range-thumb {
  width: 0.85rem;
  height: 0.85rem;
  border: none;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 0 0 2px rgba(0, 0, 0, 0.35);
}

.controls-row {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.controls-spacer {
  flex: 1;
}

.controls-ptz {
  margin-left: 0.15rem;
}

.ctrl-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  padding: 0;
  border: none;
  border-radius: 0.3rem;
  background: transparent;
  color: #fff;
  font-size: 0.85rem;
  line-height: 1;
  transition: background 0.15s ease;
}

.ctrl-btn:hover {
  background: rgba(255, 255, 255, 0.12);
}

.go-live-btn {
  padding: 0.28rem 0.55rem;
  border: none;
  border-radius: 999px;
  background: #ff4d4d;
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: #fff;
  transition: background 0.15s ease;
}

.go-live-btn:hover {
  background: #ff6666;
}

.live-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.28rem 0.5rem;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.1);
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: #fff;
}

.live-dot {
  width: 0.45rem;
  height: 0.45rem;
  border-radius: 50%;
  background: #ff4d4d;
  box-shadow: 0 0 6px rgba(255, 77, 77, 0.8);
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.45;
  }
}
</style>
