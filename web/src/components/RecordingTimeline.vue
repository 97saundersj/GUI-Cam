<script setup>
import { computed, ref } from 'vue'
import {
  dayBounds,
  findRecordingAtTime,
  formatDuration,
  formatTimelineTimeRange,
  isSameRecording,
  recordingSegmentStyle,
  recordingTypeLabel,
  timeFromTimelinePosition,
  timelineHourTicks,
} from '../api/tapo.js'

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
const hoverRecording = ref(null)
const hoverPosition = ref(null)
const gapHint = ref(false)

const isCompact = computed(() => props.variant === 'compact')

const dayRange = computed(() => dayBounds(props.date))
const dayStart = computed(() => dayRange.value.dayStart)
const dayDuration = computed(() => dayRange.value.dayEnd - dayRange.value.dayStart)

const hourTicks = computed(() =>
  timelineHourTicks(dayStart.value, dayDuration.value, isCompact.value ? 5 : 7),
)

const segments = computed(() =>
  props.recordings.map((recording, index) => ({
    recording,
    index,
    key: `${recording.startTime}-${recording.endTime}-${index}`,
    style: recordingSegmentStyle(recording, dayStart.value, dayDuration.value),
    selected: isSameRecording(recording, props.selectedRecording),
    typeClass:
      recording.vedioType === 1
        ? 'timeline-segment--continuous'
        : recording.vedioType === 2
          ? 'timeline-segment--detection'
          : 'timeline-segment--unknown',
  })),
)

const tooltipRecording = computed(() => hoverRecording.value)
const tooltipStyle = computed(() => {
  if (hoverPosition.value == null) {
    return {}
  }

  return {
    left: `${hoverPosition.value.leftPct}%`,
  }
})

function segmentLabel(recording) {
  const range = formatTimelineTimeRange(recording.startTime, recording.endTime)
  const type = recordingTypeLabel(recording.vedioType)
  const duration = formatDuration(recording.durationSeconds)
  return `${range}, ${type}, ${duration}`
}

function selectRecording(recording) {
  emit('select', recording)
}

function positionFromEvent(event) {
  const track = trackRef.value
  if (!track) {
    return null
  }

  const rect = track.getBoundingClientRect()
  if (rect.width <= 0) {
    return null
  }

  const ratio = (event.clientX - rect.left) / rect.width
  const leftPct = Math.max(0, Math.min(100, ratio * 100))
  const unixSeconds = timeFromTimelinePosition(ratio, dayStart.value, dayDuration.value)
  return { ratio, leftPct, unixSeconds }
}

function onTrackPointerMove(event) {
  if (event.target.closest('.timeline-segment')) {
    return
  }

  const position = positionFromEvent(event)
  if (!position) {
    return
  }

  hoverPosition.value = { leftPct: position.leftPct }
  const recording = findRecordingAtTime(props.recordings, position.unixSeconds)
  hoverRecording.value = recording
  gapHint.value = !recording
}

function onSegmentPointerEnter(event, recording) {
  hoverRecording.value = recording
  gapHint.value = false

  const position = positionFromEvent(event)
  if (position) {
    hoverPosition.value = { leftPct: position.leftPct }
  }
}

function onSegmentPointerLeave(event) {
  if (event.relatedTarget?.closest?.('.timeline-segment')) {
    return
  }

  if (!event.relatedTarget?.closest?.('.timeline-track')) {
    hoverRecording.value = null
    hoverPosition.value = null
    gapHint.value = false
  }
}

function onTrackPointerLeave() {
  hoverRecording.value = null
  hoverPosition.value = null
  gapHint.value = false
}

function onTrackClick(event) {
  const position = positionFromEvent(event)
  if (!position) {
    return
  }

  const recording = findRecordingAtTime(props.recordings, position.unixSeconds)
  if (recording) {
    selectRecording(recording)
  }
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
    class="recording-timeline"
    :class="{
      'recording-timeline--compact': isCompact,
      'recording-timeline--expanded': !isCompact,
      'recording-timeline--loading': loading,
    }"
  >
    <div
      ref="trackRef"
      class="timeline-track"
      role="presentation"
      @click="onTrackClick"
      @pointermove="onTrackPointerMove"
      @pointerleave="onTrackPointerLeave"
    >
      <div
        v-for="segment in segments"
        :key="segment.key"
        class="timeline-segment"
        :class="[segment.typeClass, { 'timeline-segment--selected': segment.selected }]"
        :style="{
          left: `${segment.style.leftPct}%`,
          width: `${segment.style.widthPct}%`,
        }"
        role="button"
        tabindex="0"
        :aria-label="segmentLabel(segment.recording)"
        :aria-pressed="segment.selected"
        @click.stop="selectRecording(segment.recording)"
        @keydown="onSegmentKeydown($event, segment.recording)"
        @pointerenter="onSegmentPointerEnter($event, segment.recording)"
        @pointerleave="onSegmentPointerLeave"
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
      v-if="tooltipRecording && hoverPosition"
      class="timeline-tooltip"
      :style="tooltipStyle"
      role="tooltip"
    >
      <span class="timeline-tooltip-range">
        {{ formatTimelineTimeRange(tooltipRecording.startTime, tooltipRecording.endTime) }}
      </span>
      <span class="timeline-tooltip-meta">
        {{ recordingTypeLabel(tooltipRecording.vedioType) }}
        ·
        {{ formatDuration(tooltipRecording.durationSeconds) }}
      </span>
    </div>

    <p v-else-if="gapHint && hoverPosition && !isCompact" class="timeline-gap-hint" :style="tooltipStyle">
      No recording
    </p>
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

.timeline-segment:hover {
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

.timeline-tooltip,
.timeline-gap-hint {
  position: absolute;
  bottom: calc(100% + 0.45rem);
  transform: translateX(-50%);
  pointer-events: none;
  z-index: 3;
}

.timeline-tooltip {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.35rem 0.45rem;
  border-radius: 0.45rem;
  background: rgba(15, 20, 16, 0.96);
  border: 1px solid rgba(124, 184, 138, 0.35);
  box-shadow: 0 10px 28px rgba(0, 0, 0, 0.45);
  white-space: nowrap;
}

.timeline-tooltip-range {
  font-size: 0.72rem;
  color: #e8f0e6;
}

.timeline-tooltip-meta {
  font-size: 0.65rem;
  color: #a8b8a4;
}

.timeline-gap-hint {
  margin: 0;
  padding: 0.2rem 0.4rem;
  border-radius: 0.3rem;
  background: rgba(15, 20, 16, 0.9);
  font-size: 0.65rem;
  color: #8a9d86;
}
</style>
