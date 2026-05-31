<script setup>
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import Hls from 'hls.js'

const props = defineProps({
  src: {
    type: String,
    required: true,
  },
})

const videoRef = ref(null)
const status = ref('connecting')
const errorMessage = ref('')
const isAtLiveEdge = ref(true)

const LIVE_EDGE_THRESHOLD_SEC = 8

let hls = null

function destroyPlayer() {
  if (hls) {
    hls.destroy()
    hls = null
  }
}

function setupPlayer() {
  const video = videoRef.value
  if (!video || !props.src) return

  destroyPlayer()
  status.value = 'connecting'
  errorMessage.value = ''

  if (!Hls.isSupported()) {
    status.value = 'error'
    errorMessage.value = 'This browser does not support HLS playback.'
    return
  }

  // Standard (non-low-latency) HLS keeps the playlist sliding window buffered
  // so the native controls show a seek bar for going back in time.
  hls = new Hls({
    enableWorker: true,
    lowLatencyMode: false,
    backBufferLength: Infinity,
    liveSyncMode: 'buffered',
  })

  hls.loadSource(props.src)
  hls.attachMedia(video)

  hls.on(Hls.Events.MANIFEST_PARSED, () => {
    status.value = 'live'
    video.play().catch(() => {
      status.value = 'paused'
    })
  })

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

function updateLiveEdgeState() {
  if (!hls) return
  isAtLiveEdge.value = hls.latency <= LIVE_EDGE_THRESHOLD_SEC
}

function goLive() {
  const video = videoRef.value
  if (!video || !hls) return

  const liveEdge = hls.liveSyncPosition
  if (liveEdge != null && Number.isFinite(liveEdge)) {
    video.currentTime = liveEdge
  } else if (video.seekable.length > 0) {
    video.currentTime = video.seekable.end(video.seekable.length - 1)
  }

  video.play().catch(() => {})
  isAtLiveEdge.value = true
}

function onVideoTimeUpdate() {
  updateLiveEdgeState()
}

function onVideoSeeked() {
  updateLiveEdgeState()
}

function onVideoPlaying() {
  status.value = 'live'
  updateLiveEdgeState()
}

function onVideoWaiting() {
  if (status.value !== 'error') {
    status.value = 'buffering'
  }
}

function onVideoError() {
  status.value = 'error'
  errorMessage.value = 'Stream is offline or unreachable. Check back soon.'
}

function retry() {
  setupPlayer()
}

onMounted(setupPlayer)

watch(
  () => props.src,
  () => setupPlayer(),
)

onBeforeUnmount(destroyPlayer)
</script>

<template>
  <div class="stream">
    <div class="stream-frame">
      <video
        ref="videoRef"
        class="stream-video"
        playsinline
        muted
        autoplay
        controls
        @playing="onVideoPlaying"
        @waiting="onVideoWaiting"
        @timeupdate="onVideoTimeUpdate"
        @seeked="onVideoSeeked"
        @error="onVideoError"
      />

      <div v-if="status !== 'live'" class="stream-overlay">
        <span v-if="status === 'connecting'" class="status">Connecting…</span>
        <span v-else-if="status === 'buffering'" class="status">Buffering…</span>
        <template v-else-if="status === 'error'">
          <span class="status error">{{ errorMessage }}</span>
          <button type="button" class="retry" @click="retry">Try again</button>
        </template>
        <span v-else-if="status === 'paused'" class="status">
          Tap play to start the stream
        </span>
      </div>

      <button
        v-if="status === 'live' && !isAtLiveEdge"
        type="button"
        class="go-live"
        @click="goLive"
      >
        Go live
      </button>

      <div v-else-if="status === 'live'" class="live-badge" aria-label="Live">
        <span class="live-dot" />
        Live
      </div>
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

.stream-video {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: contain;
  background: #0a0d0b;
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

.go-live {
  position: absolute;
  top: 0.85rem;
  right: 0.85rem;
  padding: 0.35rem 0.75rem;
  border: none;
  border-radius: 999px;
  background: #ff4d4d;
  backdrop-filter: blur(6px);
  font-size: 0.8rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: #fff;
  box-shadow: 0 0 12px rgba(255, 77, 77, 0.45);
  transition: background 0.15s ease, transform 0.15s ease;
}

.go-live:hover {
  background: #ff6666;
  transform: scale(1.03);
}

.live-badge {
  position: absolute;
  top: 0.85rem;
  right: 0.85rem;
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.35rem 0.65rem;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.55);
  backdrop-filter: blur(6px);
  font-size: 0.8rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: #fff;
  pointer-events: none;
}

.live-dot {
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  background: #ff4d4d;
  box-shadow: 0 0 8px rgba(255, 77, 77, 0.8);
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
