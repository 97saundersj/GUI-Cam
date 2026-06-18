<script setup>
import { computed, ref } from 'vue'
import {
  dayBounds,
  findRecordingAtTime,
  formatDuration,
  formatTimelineTime,
  formatTimelineTimeRange,
  isSameRecording,
  recordingSegmentStyle,
  recordingTypeLabel,
  timeFromTimelinePosition,
  timelineHourTicks,
  timelineRangeWindow,
} from '../api/tapo.js'

const ZOOM_WINDOW_SECONDS = 3600

const props = defineProps({
  recordings: {
    type: Array,
    default: () => [],
  },
  date: {
    type: String,
    required: true,
  },
  selectedRecording: {
    type: Object,
    default: null,
  },
  variant: {
    type: String,
    default: 'expanded',
    validator: (value) => value === 'compact' || value === 'expanded',
  },
  loading: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['select'])

const trackRef = ref(null)
const zoomTrackRef = ref(null)
const tooltipRef = ref(null)
const timelineRef = ref(null)
const hoverRecording = ref(null)
const hoverPosition = ref(null)
const scrubUnixSeconds = ref(null)
const zoomCenterUnixSeconds = ref(null)
const zoomAnchorLeftPct = ref(null)
const isZoomLocked = ref(false)
const gapHint = ref(false)
const isScrubbing = ref(false)

let scrubPointerId = null
let suppressNextClick = false

const isCompact = computed(() => props.variant === 'compact')

const dayRange = computed(() => dayBounds(props.date))
const dayStart = computed(() => dayRange.value.dayStart)
const dayEnd = computed(() => dayRange.value.dayEnd)
const dayDuration = computed(() => dayEnd.value - dayStart.value)

const hourTicks = computed(() =>
  timelineHourTicks(dayStart.value, dayDuration.value, isCompact.value ? 5 : 7),
)

const zoomWindow = computed(() => {
  const center = zoomCenterUnixSeconds.value ?? dayStart.value + dayDuration.value / 2
  return timelineRangeWindow(center, ZOOM_WINDOW_SECONDS, dayStart.value, dayEnd.value)
})

const mainZoomRegionStyle = computed(() => {
  if (zoomCenterUnixSeconds.value == null) {
    return null
  }

  const leftPct = ((zoomWindow.value.start - dayStart.value) / dayDuration.value) * 100
  const widthPct = (zoomWindow.value.duration / dayDuration.value) * 100
  return { leftPct, widthPct }
})

const zoomTicks = computed(() => timelineHourTicks(zoomWindow.value.start, zoomWindow.value.duration, 5))

const zoomPlayheadLeftPct = computed(() => {
  if (scrubUnixSeconds.value == null) {
    return 0
  }

  return ((scrubUnixSeconds.value - zoomWindow.value.start) / zoomWindow.value.duration) * 100
})

const segments = computed(() =>
  props.recordings.map((recording, index) => ({
    recording,
    index,
    key: `${recording.startTime}-${recording.endTime}-${index}`,
    style: recordingSegmentStyle(recording, dayStart.value, dayDuration.value),
    selected: isSameRecording(recording, props.selectedRecording),
    hovered: hoverRecording.value != null && isSameRecording(recording, hoverRecording.value),
    typeClass: segmentTypeClass(recording),
  })),
)

const zoomSegments = computed(() =>
  props.recordings
    .filter(
      (recording) =>
        recording.endTime > zoomWindow.value.start && recording.startTime < zoomWindow.value.end,
    )
    .map((recording, index) => ({
      recording,
      index,
      key: `zoom-${recording.startTime}-${recording.endTime}-${index}`,
      style: recordingSegmentStyle(recording, zoomWindow.value.start, zoomWindow.value.duration),
      hovered: hoverRecording.value != null && isSameRecording(recording, hoverRecording.value),
      typeClass: segmentTypeClass(recording),
    })),
)

const tooltipRecording = computed(() => hoverRecording.value)

const zoomTooltipStyle = computed(() => {
  if (zoomAnchorLeftPct.value == null) {
    return {}
  }

  const leftPct = Math.max(10, Math.min(90, zoomAnchorLeftPct.value))
  return {
    left: `${leftPct}%`,
  }
})

function segmentTypeClass(recording) {
  if (recording.vedioType === 1) {
    return 'timeline-segment--continuous'
  }

  if (recording.vedioType === 2) {
    return 'timeline-segment--detection'
  }

  return 'timeline-segment--unknown'
}

function segmentLabel(recording) {
  const range = formatTimelineTimeRange(recording.startTime, recording.endTime)
  const type = recordingTypeLabel(recording.vedioType)
  const duration = formatDuration(recording.durationSeconds)
  return `${range}, ${type}, ${duration}`
}

function selectRecording(recording) {
  emit('select', recording)
}

function positionFromClientX(clientX, trackEl, rangeStart, rangeDuration) {
  if (!trackEl) {
    return null
  }

  const rect = trackEl.getBoundingClientRect()
  if (rect.width <= 0) {
    return null
  }

  const ratio = (clientX - rect.left) / rect.width
  const clampedRatio = Math.max(0, Math.min(1, ratio))
  const leftPct = clampedRatio * 100
  const unixSeconds = timeFromTimelinePosition(clampedRatio, rangeStart, rangeDuration)
  return { ratio: clampedRatio, leftPct, unixSeconds }
}

function updateScrubTimeOnly(unixSeconds) {
  const clamped = Math.max(dayStart.value, Math.min(dayEnd.value - 1, unixSeconds))
  scrubUnixSeconds.value = clamped
  const recording = findRecordingAtTime(props.recordings, clamped)
  hoverRecording.value = recording
  gapHint.value = !recording
  return { unixSeconds: clamped, recording }
}

function updateFromMainTrack(unixSeconds, leftPct) {
  const clamped = Math.max(dayStart.value, Math.min(dayEnd.value - 1, unixSeconds))
  const anchorPct = Math.max(0, Math.min(100, leftPct))

  zoomCenterUnixSeconds.value = clamped
  zoomAnchorLeftPct.value = anchorPct
  scrubUnixSeconds.value = clamped
  hoverPosition.value = { leftPct: anchorPct }
  const recording = findRecordingAtTime(props.recordings, clamped)
  hoverRecording.value = recording
  gapHint.value = !recording
  return { unixSeconds: clamped, leftPct: anchorPct, recording }
}

function lockZoomArea() {
  if (zoomCenterUnixSeconds.value == null && scrubUnixSeconds.value != null) {
    zoomCenterUnixSeconds.value = scrubUnixSeconds.value
  }

  if (zoomAnchorLeftPct.value == null && hoverPosition.value) {
    zoomAnchorLeftPct.value = hoverPosition.value.leftPct
  }

  isZoomLocked.value = true
}

function unlockZoomArea() {
  isZoomLocked.value = false
}

function positionFromMainTrack(event) {
  return positionFromClientX(event.clientX, trackRef.value, dayStart.value, dayDuration.value)
}

function positionFromZoomTrack(event) {
  return positionFromClientX(
    event.clientX,
    zoomTrackRef.value,
    zoomWindow.value.start,
    zoomWindow.value.duration,
  )
}

function getScrubTooltipHitRect() {
  const tooltip = tooltipRef.value
  const track = trackRef.value
  if (!tooltip) {
    return null
  }

  const rect = tooltip.getBoundingClientRect()
  const bridgeBottom = track?.getBoundingClientRect().top ?? rect.bottom

  return {
    left: rect.left,
    right: rect.right,
    top: rect.top,
    bottom: Math.max(rect.bottom, bridgeBottom),
  }
}

function isPointerOverScrubTooltip(event) {
  const rect = getScrubTooltipHitRect()
  if (!rect) {
    return false
  }

  return (
    event.clientX >= rect.left
    && event.clientX <= rect.right
    && event.clientY >= rect.top
    && event.clientY <= rect.bottom
  )
}

function isPointerOverMainTrack(event) {
  const track = trackRef.value
  if (!track) {
    return false
  }

  const rect = track.getBoundingClientRect()
  return (
    event.clientX >= rect.left
    && event.clientX <= rect.right
    && event.clientY >= rect.top
    && event.clientY <= rect.bottom
  )
}

function shouldUseZoomMapping(event) {
  return zoomCenterUnixSeconds.value != null && isPointerOverScrubTooltip(event)
}

function updateScrubFromEvent(event) {
  if (shouldUseZoomMapping(event)) {
    if (!isZoomLocked.value) {
      lockZoomArea()
    }

    const position = positionFromZoomTrack(event)
    if (!position) {
      return null
    }

    return updateScrubTimeOnly(position.unixSeconds)
  }

  if (isPointerOverMainTrack(event)) {
    if (isZoomLocked.value) {
      unlockZoomArea()
    }

    const position = positionFromMainTrack(event)
    if (!position) {
      return null
    }

    return updateFromMainTrack(position.unixSeconds, position.leftPct)
  }

  return null
}

function updateHoverFromPosition(event) {
  const position = positionFromMainTrack(event)
  if (!position) {
    return null
  }

  return updateFromMainTrack(position.unixSeconds, position.leftPct)
}

function clearHover() {
  hoverRecording.value = null
  hoverPosition.value = null
  scrubUnixSeconds.value = null
  zoomCenterUnixSeconds.value = null
  zoomAnchorLeftPct.value = null
  isZoomLocked.value = false
  gapHint.value = false
}

function onTimelinePointerDown(event) {
  if (event.button !== 0) {
    return
  }

  const onMainTrack = isPointerOverMainTrack(event)
  const onZoomTooltip = isPointerOverScrubTooltip(event)
  if (!onMainTrack && !onZoomTooltip) {
    return
  }

  isScrubbing.value = true
  scrubPointerId = event.pointerId
  timelineRef.value?.setPointerCapture(event.pointerId)
  event.preventDefault()

  if (onZoomTooltip) {
    lockZoomArea()
  } else {
    unlockZoomArea()
  }

  updateScrubFromEvent(event)
}

function onTimelinePointerMove(event) {
  if (isScrubbing.value && event.pointerId === scrubPointerId) {
    updateScrubFromEvent(event)
    return
  }

  if (event.pointerType === 'touch') {
    return
  }

  if (isPointerOverScrubTooltip(event)) {
    if (!isZoomLocked.value) {
      lockZoomArea()
    }

    const position = positionFromZoomTrack(event)
    if (position) {
      updateScrubTimeOnly(position.unixSeconds)
    }
    return
  }

  if (isZoomLocked.value) {
    unlockZoomArea()
  }

  if (!isPointerOverMainTrack(event)) {
    return
  }

  if (event.target.closest('.timeline-segment')) {
    return
  }

  updateHoverFromPosition(event)
}

function onSegmentPointerEnter(event, recording) {
  if (isScrubbing.value) {
    return
  }

  if (isZoomLocked.value) {
    unlockZoomArea()
  }

  hoverRecording.value = recording
  gapHint.value = false

  const position = positionFromMainTrack(event)
  if (position) {
    updateFromMainTrack(position.unixSeconds, position.leftPct)
  }
}

function onSegmentPointerLeave(event) {
  if (isScrubbing.value) {
    return
  }

  if (event.relatedTarget?.closest?.('.timeline-segment')) {
    return
  }

  if (
    event.relatedTarget?.closest?.('.timeline-track')
    || event.relatedTarget?.closest?.('.timeline-scrub-tooltip')
  ) {
    return
  }

  clearHover()
}

function onTimelinePointerLeave(event) {
  if (isScrubbing.value) {
    return
  }

  if (event.relatedTarget?.closest?.('.recording-timeline')) {
    return
  }

  if (isPointerOverScrubTooltip(event)) {
    return
  }

  clearHover()
}

function endScrub(event) {
  if (!isScrubbing.value || event.pointerId !== scrubPointerId) {
    return
  }

  timelineRef.value?.releasePointerCapture(event.pointerId)

  const result = updateScrubFromEvent(event)
  if (result?.recording) {
    selectRecording(result.recording)
    suppressNextClick = true
  }

  isScrubbing.value = false
  scrubPointerId = null

  if (event.pointerType === 'touch') {
    clearHover()
  }
}

function onTimelinePointerUp(event) {
  endScrub(event)
}

function onTimelinePointerCancel(event) {
  endScrub(event)
}

function onTrackClick(event) {
  if (suppressNextClick) {
    suppressNextClick = false
    return
  }

  const position = positionFromMainTrack(event)
  if (!position) {
    return
  }

  const recording = findRecordingAtTime(props.recordings, position.unixSeconds)
  if (recording) {
    selectRecording(recording)
  }
}

function onSegmentClick(recording, event) {
  if (suppressNextClick) {
    suppressNextClick = false
    event.stopPropagation()
    return
  }

  selectRecording(recording)
}

function onSegmentKeydown(event, recording) {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    selectRecording(recording)
  }
}
</script>

<template>
  <div
    ref="timelineRef"
    class="recording-timeline"
    :class="{
      'recording-timeline--compact': isCompact,
      'recording-timeline--expanded': !isCompact,
      'recording-timeline--loading': loading,
      'recording-timeline--scrubbing': isScrubbing,
    }"
    @pointerdown="onTimelinePointerDown"
    @pointermove="onTimelinePointerMove"
    @pointerup="onTimelinePointerUp"
    @pointercancel="onTimelinePointerCancel"
    @pointerleave="onTimelinePointerLeave"
  >
    <div
      ref="trackRef"
      class="timeline-track"
      role="presentation"
      @click="onTrackClick"
    >
      <div
        v-for="segment in segments"
        :key="segment.key"
        class="timeline-segment"
        :class="[
          segment.typeClass,
          {
            'timeline-segment--selected': segment.selected,
            'timeline-segment--hovered': segment.hovered,
          },
        ]"
        :style="{
          left: `${segment.style.leftPct}%`,
          width: `${segment.style.widthPct}%`,
        }"
        role="button"
        tabindex="0"
        :aria-label="segmentLabel(segment.recording)"
        :aria-pressed="segment.selected"
        @click.stop="onSegmentClick(segment.recording, $event)"
        @keydown="onSegmentKeydown($event, segment.recording)"
        @pointerenter="onSegmentPointerEnter($event, segment.recording)"
        @pointerleave="onSegmentPointerLeave"
      />

      <div
        v-if="mainZoomRegionStyle"
        class="timeline-zoom-region"
        :style="{
          left: `${mainZoomRegionStyle.leftPct}%`,
          width: `${mainZoomRegionStyle.widthPct}%`,
        }"
        aria-hidden="true"
      />

      <div
        v-if="selectedRecording"
        class="timeline-selection-marker"
        :style="{
          left: `${recordingSegmentStyle(selectedRecording, dayStart, dayDuration).leftPct}%`,
          width: `${recordingSegmentStyle(selectedRecording, dayStart, dayDuration).widthPct}%`,
        }"
        aria-hidden="true"
      />
    </div>

    <div v-if="!isCompact" class="timeline-hours">
      <span
        v-for="tick in hourTicks"
        :key="tick.time"
        class="timeline-hour"
        :style="{ left: `${tick.leftPct}%` }"
      >
        {{ tick.label }}
      </span>
    </div>

    <div
      v-if="zoomCenterUnixSeconds != null"
      ref="tooltipRef"
      class="timeline-scrub-tooltip"
      :class="{ 'timeline-scrub-tooltip--locked': isZoomLocked }"
      :style="zoomTooltipStyle"
      role="tooltip"
      aria-label="Zoomed timeline"
    >
      <p class="timeline-scrub-time">{{ formatTimelineTime(scrubUnixSeconds) }}</p>

      <div ref="zoomTrackRef" class="timeline-zoom-track" aria-hidden="true">
        <div
          v-for="segment in zoomSegments"
          :key="segment.key"
          class="timeline-segment timeline-zoom-segment"
          :class="[segment.typeClass, { 'timeline-segment--hovered': segment.hovered }]"
          :style="{
            left: `${segment.style.leftPct}%`,
            width: `${segment.style.widthPct}%`,
          }"
        />

        <div class="timeline-zoom-playhead" :style="{ left: `${zoomPlayheadLeftPct}%` }" />
      </div>

      <div class="timeline-zoom-hours" aria-hidden="true">
        <span
          v-for="tick in zoomTicks"
          :key="tick.time"
          class="timeline-zoom-hour"
          :style="{ left: `${tick.leftPct}%` }"
        >
          {{ tick.label }}
        </span>
      </div>

      <div v-if="tooltipRecording" class="timeline-scrub-recording">
        <span class="timeline-tooltip-range">
          {{ formatTimelineTimeRange(tooltipRecording.startTime, tooltipRecording.endTime) }}
        </span>
        <span class="timeline-tooltip-meta">
          {{ recordingTypeLabel(tooltipRecording.vedioType) }}
          ·
          {{ formatDuration(tooltipRecording.durationSeconds) }}
        </span>
      </div>

      <p v-else class="timeline-scrub-empty">No recording</p>
    </div>
  </div>
</template>

<style scoped>
.recording-timeline {
  position: relative;
  width: 100%;
}

.recording-timeline--compact {
  margin-top: 0.45rem;
}

.recording-timeline--expanded {
  margin-top: 0.25rem;
}

.recording-timeline--loading {
  opacity: 0.65;
}

.timeline-track {
  position: relative;
  width: 100%;
  border-radius: 0.35rem;
  background: rgba(0, 0, 0, 0.35);
  border: 1px solid rgba(124, 184, 138, 0.15);
  cursor: pointer;
  overflow: hidden;
  touch-action: none;
}

.recording-timeline--scrubbing .timeline-track,
.recording-timeline--scrubbing .timeline-scrub-tooltip {
  cursor: grabbing;
}

.recording-timeline--compact .timeline-track {
  height: 2rem;
}

.recording-timeline--expanded .timeline-track {
  height: 2.75rem;
}

.timeline-segment {
  position: absolute;
  top: 0;
  bottom: 0;
  min-width: 2px;
  border: none;
  padding: 0;
  cursor: pointer;
  transition:
    filter 0.15s ease,
    box-shadow 0.15s ease;
}

.timeline-segment:focus-visible {
  outline: 2px solid #c8e6c0;
  outline-offset: -2px;
  z-index: 2;
}

.timeline-segment--continuous {
  background: rgba(74, 124, 89, 0.75);
  z-index: 0;
}

.timeline-segment--detection {
  background: rgba(255, 180, 77, 0.65);
  z-index: 1;
}

.timeline-segment--unknown {
  background: rgba(255, 255, 255, 0.25);
}

.timeline-segment:hover,
.timeline-segment--hovered {
  filter: brightness(1.15);
}

.timeline-segment--selected {
  box-shadow: inset 0 0 0 2px rgba(200, 230, 192, 0.95);
  z-index: 2;
}

.timeline-selection-marker {
  position: absolute;
  top: 0;
  bottom: 0;
  pointer-events: none;
  border: 2px solid rgba(200, 230, 192, 0.85);
  border-radius: 0.2rem;
  box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.35);
}

.timeline-zoom-region {
  position: absolute;
  top: 0;
  bottom: 0;
  pointer-events: none;
  background: rgba(200, 230, 192, 0.12);
  border-left: 1px solid rgba(200, 230, 192, 0.35);
  border-right: 1px solid rgba(200, 230, 192, 0.35);
  z-index: 1;
}

.timeline-hours {
  position: relative;
  height: 1.1rem;
  margin-top: 0.35rem;
}

.timeline-hour {
  position: absolute;
  transform: translateX(-50%);
  font-size: 0.68rem;
  color: #6d7d69;
  white-space: nowrap;
}

.timeline-scrub-tooltip {
  position: absolute;
  bottom: calc(100% + 0.45rem);
  transform: translateX(-50%);
  pointer-events: auto;
  z-index: 3;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  width: min(92vw, 22rem);
  padding: 0.55rem 0.6rem 0.6rem;
  border-radius: 0.55rem;
  background: rgba(15, 20, 16, 0.98);
  border: 1px solid rgba(124, 184, 138, 0.4);
  box-shadow: 0 14px 36px rgba(0, 0, 0, 0.55);
  touch-action: none;
}

.timeline-scrub-tooltip--locked {
  border-color: rgba(200, 230, 192, 0.55);
}

.timeline-scrub-tooltip::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  top: 100%;
  height: 0.55rem;
}

.timeline-scrub-time {
  margin: 0;
  font-size: 0.82rem;
  font-weight: 600;
  color: #e8f0e6;
  text-align: center;
}

.timeline-zoom-track {
  position: relative;
  height: 2.5rem;
  border-radius: 0.35rem;
  background: rgba(0, 0, 0, 0.45);
  border: 1px solid rgba(124, 184, 138, 0.2);
  overflow: hidden;
  cursor: grab;
}

.recording-timeline--scrubbing .timeline-zoom-track {
  cursor: grabbing;
}

.timeline-zoom-segment {
  pointer-events: none;
}

.timeline-zoom-playhead {
  position: absolute;
  top: 0;
  bottom: 0;
  width: 2px;
  margin-left: -1px;
  background: #e8f0e6;
  box-shadow: 0 0 6px rgba(232, 240, 230, 0.85);
  pointer-events: none;
  z-index: 3;
}

.timeline-zoom-hours {
  position: relative;
  height: 0.95rem;
}

.timeline-zoom-hour {
  position: absolute;
  transform: translateX(-50%);
  font-size: 0.62rem;
  color: #8a9d86;
  white-space: nowrap;
}

.timeline-scrub-recording {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding-top: 0.1rem;
  border-top: 1px solid rgba(124, 184, 138, 0.15);
}

.timeline-scrub-empty {
  margin: 0;
  padding-top: 0.15rem;
  border-top: 1px solid rgba(124, 184, 138, 0.15);
  font-size: 0.68rem;
  color: #8a9d86;
  text-align: center;
}

.timeline-tooltip-range {
  font-size: 0.72rem;
  color: #e8f0e6;
}

.timeline-tooltip-meta {
  font-size: 0.65rem;
  color: #a8b8a4;
}
</style>
