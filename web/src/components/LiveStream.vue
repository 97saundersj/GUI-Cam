<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import Hls from 'hls.js'
import mpegts from 'mpegts.js'
import StreamControls from './StreamControls.vue'
import { useDigitalZoom } from '../composables/useDigitalZoom'
const props = defineProps({
  src: {
    type: String,
    required: true,
  },
  mode: {
    type: String,
    default: 'live',
    validator: (value) => value === 'live' || value === 'recording',
  },
  ptzEnabled: {
    type: Boolean,
    default: true,
  },
})

const emit = defineEmits(['go-live'])

const isLiveMode = computed(() => props.mode === 'live')

const isMp4Recording = computed(
  () => !isLiveMode.value && props.src.includes('format=mp4'),
)

const videoRef = ref(null)
const streamFrameRef = ref(null)
const streamControlsRef = ref(null)
const status = ref('connecting')
const errorMessage = ref('')
const isAtLiveEdge = ref(true)
const isBuffering = ref(false)

const isPlaying = computed(() => status.value === 'live' || status.value === 'playing')

const lowLatency = import.meta.env.VITE_HLS_LOW_LATENCY !== 'false'
const LIVE_EDGE_THRESHOLD_SEC = lowLatency ? 3 : 8
const SEEK_TO_LIVE_EPSILON_SEC = 0.05

let hls = null
let mpegtsPlayer = null
let playerGeneration = 0
let initialLiveSyncDone = false

function destroyMpegtsPlayer() {
  if (!mpegtsPlayer) return

  const player = mpegtsPlayer
  mpegtsPlayer = null

  const video = videoRef.value
  if (video) {
    video.pause()
  }

  try {
    player.pause()
  } catch {
    // Player may already be paused or torn down.
  }

  try {
    player.unload()
  } catch {
    // Ignore unload errors during teardown.
  }

  try {
    player.detachMediaElement()
  } catch {
    // detachMediaElement can reject if play() is still pending.
  }

  try {
    player.destroy()
  } catch {
    // Ignore destroy errors during teardown.
  }
}

function destroyPlayer() {
  if (hls) {
    hls.destroy()
    hls = null
  }

  destroyMpegtsPlayer()

  const video = videoRef.value
  if (video) {
    video.pause()
    video.removeAttribute('src')
    video.load()
  }
}

const {
  zoom,
  isZoomed,
  videoStyle,
  panBy,
  zoomIn,
  zoomOut,
  resetZoom,
  zoomAtWheel,
} = useDigitalZoom()

let panDragActive = false
let panDragStartX = 0
let panDragStartY = 0

function createHlsConfig() {
  if (lowLatency) {
    return {
      enableWorker: true,
      lowLatencyMode: true,
      backBufferLength: 30,
      liveSyncMode: 'live',
      liveSyncDurationCount: 2,
      liveMaxLatencyDurationCount: 6,
      maxLiveSyncPlaybackRate: 1.2,
    }
  }

  return {
    enableWorker: true,
    lowLatencyMode: false,
    backBufferLength: Infinity,
    liveSyncMode: 'buffered',
  }
}

function toggleRecordingPlay() {
  if (isMp4Recording.value) {
    const video = videoRef.value
    if (!video) return

    if (video.paused) {
      video.play().catch(() => {})
    } else {
      video.pause()
    }
    return
  }

  if (!mpegtsPlayer) {
    setupRecordingPlayer()
    return
  }

  const video = videoRef.value
  if (!video) return

  if (video.paused) {
    mpegtsPlayer.play()
    status.value = 'connecting'
  } else {
    mpegtsPlayer.pause()
  }
}

function startRecordingPlayback(generation = playerGeneration) {
  if (!mpegtsPlayer || generation !== playerGeneration) return

  const video = videoRef.value
  if (!video) return

  try {
    mpegtsPlayer.play()
  } catch {
    if (generation === playerGeneration) {
      status.value = 'paused'
    }
    return
  }

  window.setTimeout(() => {
    if (generation !== playerGeneration) return

    if (!isLiveMode.value && video.paused && status.value === 'connecting') {
      status.value = 'paused'
    }
  }, 250)
}

function setupNativeRecordingPlayer() {
  const video = videoRef.value
  if (!video || !props.src) return

  const generation = ++playerGeneration

  destroyPlayer()
  status.value = 'connecting'
  errorMessage.value = ''
  isBuffering.value = false
  isAtLiveEdge.value = true
  resetZoom()

  video.src = props.src
  video.load()

  video.play().catch(() => {
    if (generation === playerGeneration) {
      status.value = 'paused'
    }
  })
}

function setupRecordingPlayer() {
  const video = videoRef.value
  if (!video || !props.src) return

  const generation = ++playerGeneration

  destroyPlayer()
  status.value = 'connecting'
  errorMessage.value = ''
  isBuffering.value = false
  isAtLiveEdge.value = true
  resetZoom()

  if (!mpegts.isSupported()) {
    status.value = 'error'
    errorMessage.value = 'This browser does not support MPEG-TS playback.'
    return
  }

  mpegtsPlayer = mpegts.createPlayer(
    {
      type: 'mpegts',
      isLive: false,
      url: props.src,
    },
    {
      enableWorker: true,
      enableStashBuffer: true,
      stashInitialSize: 512 * 1024,
    },
  )

  mpegtsPlayer.attachMediaElement(video)
  mpegtsPlayer.on(mpegts.Events.ERROR, () => {
    if (generation !== playerGeneration) return

    status.value = 'error'
    errorMessage.value = 'Unable to play this recording right now.'

    try {
      mpegtsPlayer?.pause()
      mpegtsPlayer?.unload()
    } catch {
      // Ignore cleanup errors after a playback failure.
    }
  })
  mpegtsPlayer.on(mpegts.Events.LOADING_COMPLETE, () => {
    if (generation !== playerGeneration) return

    if (!isLiveMode.value && status.value === 'playing') {
      status.value = 'paused'
    }
  })
  mpegtsPlayer.load()
  startRecordingPlayback(generation)
}

function setupHlsPlayer() {
  const video = videoRef.value
  if (!video || !props.src) return

  destroyPlayer()
  status.value = 'connecting'
  errorMessage.value = ''
  isBuffering.value = false
  initialLiveSyncDone = false
  resetZoom()

  if (!Hls.isSupported()) {
    status.value = 'error'
    errorMessage.value = 'This browser does not support HLS playback.'
    return
  }

  hls = new Hls(createHlsConfig())

  hls.loadSource(props.src)
  hls.attachMedia(video)

  hls.on(Hls.Events.MANIFEST_PARSED, () => {
    status.value = 'live'
    tryInitialGoLive()
    video.play().catch(() => {
      status.value = 'paused'
    })
  })

  hls.on(Hls.Events.FRAG_BUFFERED, tryInitialGoLive)

  hls.on(Hls.Events.ERROR, (_event, data) => {
    if (data.fatal) {
      status.value = 'error'
      errorMessage.value =
        data.type === Hls.ErrorTypes.NETWORK_ERROR
          ? 'Stream is offline or unreachable. Check back soon.'
          : 'Unable to play the stream right now.'
      destroyPlayer()
    }
  })
}

function setupPlayer() {
  if (isLiveMode.value) {
    setupHlsPlayer()
  } else if (isMp4Recording.value) {
    setupNativeRecordingPlayer()
  } else {
    setupRecordingPlayer()
  }
}

function getSeekableEnd() {
  const video = videoRef.value
  if (!video || video.seekable.length === 0) return null
  return video.seekable.end(video.seekable.length - 1)
}

function secondsBehindLiveEdge() {
  const video = videoRef.value
  const seekableEnd = getSeekableEnd()
  if (!video || seekableEnd == null) return null
  return seekableEnd - video.currentTime
}

function updateLiveEdgeState() {
  if (!isLiveMode.value) {
    isAtLiveEdge.value = false
    return
  }

  if (!hls) return

  const behindLive = secondsBehindLiveEdge()
  if (behindLive != null) {
    isAtLiveEdge.value = behindLive <= LIVE_EDGE_THRESHOLD_SEC
    return
  }

  const targetLatency = hls.targetLatency
  const threshold =
    targetLatency != null ? targetLatency + 0.5 : LIVE_EDGE_THRESHOLD_SEC
  isAtLiveEdge.value = hls.latency <= threshold
}

function goLive() {
  if (!isLiveMode.value) {
    emit('go-live')
    return
  }

  const video = videoRef.value
  if (!video || !hls) return

  const seekableEnd = getSeekableEnd()
  if (seekableEnd != null) {
    const rangeStart = video.seekable.start(video.seekable.length - 1)
    video.currentTime = Math.max(rangeStart, seekableEnd - SEEK_TO_LIVE_EPSILON_SEC)
  } else if (hls.liveSyncPosition != null && Number.isFinite(hls.liveSyncPosition)) {
    video.currentTime = hls.liveSyncPosition
  }

  video.play().catch(() => {})
  updateLiveEdgeState()
}

function tryInitialGoLive() {
  if (initialLiveSyncDone || !hls) return

  const seekableEnd = getSeekableEnd()
  const liveSyncReady =
    hls.liveSyncPosition != null && Number.isFinite(hls.liveSyncPosition)
  if (seekableEnd == null && !liveSyncReady) return

  initialLiveSyncDone = true
  goLive()
}

function onVideoTimeUpdate() {
  updateLiveEdgeState()
}

function onVideoSeeked() {
  updateLiveEdgeState()
}

function onVideoPlaying() {
  status.value = isLiveMode.value ? 'live' : 'playing'
  isBuffering.value = false
  updateLiveEdgeState()
}

function onVideoPause() {
  if (!isLiveMode.value && status.value === 'playing') {
    status.value = 'paused'
  }
}

function onVideoCanPlay() {
  isBuffering.value = false
}

function onVideoWaiting() {
  if (isPlaying.value) {
    isBuffering.value = true
  }
}

function onVideoError() {
  status.value = 'error'
  errorMessage.value = isLiveMode.value
    ? 'Stream is offline or unreachable. Check back soon.'
    : 'Unable to play this recording right now.'
}

function onRecordingEnded() {
  if (!isLiveMode.value) {
    status.value = 'paused'
  }
}

function retry() {
  setupPlayer()
}

function revealControls() {
  streamControlsRef.value?.revealControls()
}

function onStreamClick() {
  const video = videoRef.value
  if (!video) return

  if (panDragActive) return

  if (status.value === 'paused') {
    if (!isLiveMode.value) {
      toggleRecordingPlay()
    } else {
      video.play().catch(() => {})
    }
    return
  }

  if (isPlaying.value) {
    revealControls()
  }
}

function onStreamPointerDown(event) {
  if (!isPlaying.value || !isZoomed.value) return
  if (event.button !== 0) return

  panDragActive = true
  panDragStartX = event.clientX
  panDragStartY = event.clientY
  streamFrameRef.value?.setPointerCapture(event.pointerId)
  event.preventDefault()
}

function onStreamPointerUp(event) {
  if (!panDragActive) return
  panDragActive = false
  streamFrameRef.value?.releasePointerCapture(event.pointerId)
}

function onStreamWheel(event) {
  if (!isPlaying.value) return
  event.preventDefault()
  zoomAtWheel(event.deltaY)
  revealControls()
}

function onStreamPointerMove(event) {
  onStreamPointerMoveDrag(event)
  revealControls()
}

function onStreamPointerMoveDrag(event) {
  if (!panDragActive || !isZoomed.value) return

  const frame = streamFrameRef.value
  if (!frame) return

  const rect = frame.getBoundingClientRect()
  const dx = ((event.clientX - panDragStartX) / rect.width) * 100
  const dy = ((event.clientY - panDragStartY) / rect.height) * 100

  panBy(dx, dy)
  panDragStartX = event.clientX
  panDragStartY = event.clientY
}

onMounted(setupPlayer)

watch(
  () => [props.src, props.mode],
  (value, previous) => {
    if (previous && value[0] === previous[0] && value[1] === previous[1]) {
      return
    }
    setupPlayer()
  },
)

onBeforeUnmount(destroyPlayer)
</script>

<template>
  <div class="stream">
    <div
      ref="streamFrameRef"
      class="stream-frame"
      :class="{ 'stream-frame--panning': panDragActive, 'stream-frame--zoomed': isZoomed }"
      @pointermove="onStreamPointerMove"
      @pointerdown="onStreamPointerDown"
      @pointerup="onStreamPointerUp"
      @pointercancel="onStreamPointerUp"
      @wheel="onStreamWheel"
      @click="onStreamClick"
    >
      <div class="stream-video-wrap">
        <video
          ref="videoRef"
          class="stream-video"
          :style="videoStyle"
          playsinline
          muted
          autoplay
          @playing="onVideoPlaying"
          @pause="onVideoPause"
          @canplay="onVideoCanPlay"
          @waiting="onVideoWaiting"
          @timeupdate="onVideoTimeUpdate"
          @seeked="onVideoSeeked"
          @error="onVideoError"
          @ended="onRecordingEnded"
        />
      </div>

      <div v-if="!isPlaying && status !== 'paused'" class="stream-overlay">
        <span v-if="status === 'connecting'" class="status">
          {{ isLiveMode ? 'Connecting…' : 'Loading recording…' }}
        </span>
        <template v-else-if="status === 'error'">
          <span class="status error">{{ errorMessage }}</span>
          <button type="button" class="retry" @click="retry">Try again</button>
        </template>
      </div>

      <div
        v-else-if="status === 'paused'"
        class="stream-overlay stream-overlay--interactive"
        @click.stop="onStreamClick"
      >
        <span class="status">
          {{ isLiveMode ? 'Tap play to start the stream' : 'Tap play to watch the recording' }}
        </span>
      </div>

      <StreamControls
        v-if="isPlaying || status === 'paused'"
        ref="streamControlsRef"
        :video-el="videoRef"
        :frame-el="streamFrameRef"
        :is-at-live-edge="isAtLiveEdge"
        :is-buffering="isBuffering"
        :is-live-mode="isLiveMode"
        :zoom="zoom"
        :ptz-enabled="ptzEnabled"
        :toggle-play="isLiveMode ? null : toggleRecordingPlay"
        @go-live="goLive"
        @zoom-in="zoomIn"
        @zoom-out="zoomOut"
        @zoom-reset="resetZoom"
      />
    </div>
  </div>
</template>

<style scoped>
.stream {
  width: 100%;
}

.stream-frame {
  position: relative;
  aspect-ratio: 16 / 9;
  border-radius: 1rem;
  overflow: hidden;
  background: #1a221c;
  border: 1px solid rgba(124, 184, 138, 0.2);
  box-shadow:
    0 24px 48px rgba(0, 0, 0, 0.45),
    0 0 0 1px rgba(255, 255, 255, 0.04) inset;
}

.stream-frame--zoomed {
  cursor: grab;
}

.stream-frame--panning {
  cursor: grabbing;
}

.stream-video-wrap {
  width: 100%;
  height: 100%;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stream-video {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: contain;
  background: #0a0d0b;
  transition: transform 0.15s ease;
}

.stream-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 1.5rem;
  background: rgba(10, 13, 11, 0.82);
  text-align: center;
}

.stream-overlay--interactive {
  cursor: pointer;
  z-index: 4;
}

.status {
  font-size: 1rem;
  color: #c5d4c1;
}

.status.error {
  max-width: 22rem;
  color: #e8c4c4;
}

.retry {
  padding: 0.55rem 1.1rem;
  border: 1px solid rgba(124, 184, 138, 0.45);
  border-radius: 999px;
  background: rgba(74, 124, 89, 0.25);
  color: #d8f0d4;
  transition: background 0.15s ease;
}

.retry:hover {
  background: rgba(74, 124, 89, 0.45);
}
</style>
