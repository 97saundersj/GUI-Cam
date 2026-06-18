<script setup>
import { computed, ref } from 'vue'
import {
  formatDuration,
  formatTimelineTimeRange,
  recordingTypeLabel,
  toTapoDate,
} from '../api/tapo.js'
import RecordingTimeline from './RecordingTimeline.vue'

const props = defineProps({
  label: {
    type: String,
    default: '',
  },
  date: {
    type: String,
    required: true,
  },
  selectedRecording: {
    type: Object,
    default: null,
  },
  recordings: {
    type: Array,
    default: () => [],
  },
  responseDate: {
    type: String,
    default: '',
  },
  total: {
    type: Number,
    default: 0,
  },
  loading: {
    type: Boolean,
    default: false,
  },
  error: {
    type: String,
    default: '',
  },
  summary: {
    type: String,
    default: '',
  },
  refresh: {
    type: Function,
    default: null,
  },
})

const emit = defineEmits(['select'])

const expanded = ref(false)

const title = computed(() =>
  props.label ? `${props.label} recordings` : 'SD card recordings',
)

const selectedSummary = computed(() => {
  const recording = props.selectedRecording
  if (!recording) {
    return ''
  }

  const range = formatTimelineTimeRange(recording.startTime, recording.endTime)
  const type = recordingTypeLabel(recording.vedioType)
  const duration = formatDuration(recording.durationSeconds)
  return `Playing ${range} · ${type} · ${duration}`
})

function toggle() {
  expanded.value = !expanded.value
}

function selectRecording(recording) {
  emit('select', recording)
}

function onRefresh() {
  props.refresh?.()
}
</script>

<template>
  <section class="recordings">
    <button
      type="button"
      class="recordings-toggle"
      :aria-expanded="expanded"
      @click="toggle"
    >
      <span
        class="toggle-chevron"
        :class="{ 'toggle-chevron--open': expanded }"
        aria-hidden="true"
      >
        ▶
      </span>

      <span class="toggle-text">
        <span class="recordings-title">{{ title }}</span>
        <span class="recordings-subtitle">
          SD card timeline for the selected day
        </span>
      </span>

      <span class="recordings-summary">{{ summary }}</span>
    </button>

    <div v-show="expanded" class="recordings-body">
      <div class="recordings-toolbar">
        <button
          type="button"
          class="refresh-btn"
          :disabled="loading"
          @click="onRefresh"
        >
          {{ loading ? 'Loading…' : 'Refresh' }}
        </button>
      </div>

      <div class="recordings-legend" aria-hidden="true">
        <span class="legend-item legend-item--continuous">Continuous</span>
        <span class="legend-item legend-item--detection">Detection</span>
        <span class="legend-item legend-item--gap">No recording</span>
      </div>

      <p v-if="error" class="recordings-error" role="alert">
        {{ error }}
      </p>

      <p v-else-if="loading && recordings.length === 0" class="recordings-status">
        Loading recordings…
      </p>

      <p v-else-if="!loading && recordings.length === 0" class="recordings-status">
        No recordings found for {{ responseDate || toTapoDate(date) }}.
      </p>

      <RecordingTimeline
        v-else
        variant="expanded"
        :recordings="recordings"
        :date="date"
        :selected-recording="selectedRecording"
        :loading="loading"
        @select="selectRecording"
      />

      <p v-if="selectedSummary" class="recordings-selected">
        {{ selectedSummary }}
      </p>
    </div>
  </section>
</template>

<style scoped>
.recordings {
  margin-top: 1rem;
  border: 1px solid rgba(124, 184, 138, 0.2);
  border-radius: 0.75rem;
  background: rgba(15, 20, 16, 0.65);
  overflow: hidden;
}

.recordings-toggle {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  width: 100%;
  padding: 0.85rem 1rem;
  border: none;
  background: transparent;
  color: inherit;
  text-align: left;
  transition: background 0.15s ease;
}

.recordings-toggle:hover {
  background: rgba(124, 184, 138, 0.08);
}

.toggle-chevron {
  flex-shrink: 0;
  font-size: 0.65rem;
  color: #7cb88a;
  transition: transform 0.2s ease;
}

.toggle-chevron--open {
  transform: rotate(90deg);
}

.toggle-text {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  min-width: 0;
  flex: 1;
}

.recordings-title {
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #7cb88a;
}

.recordings-subtitle {
  font-size: 0.82rem;
  color: #8a9d86;
}

.recordings-summary {
  flex-shrink: 0;
  font-size: 0.78rem;
  color: #a8b8a4;
}

.recordings-body {
  padding: 0 1rem 1rem;
  border-top: 1px solid rgba(124, 184, 138, 0.12);
}

.recordings-toolbar {
  display: flex;
  justify-content: flex-end;
  padding: 0.65rem 0 0.75rem;
}

.recordings-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin-bottom: 0.75rem;
}

.legend-item {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.72rem;
  color: #a8b8a4;
}

.legend-item::before {
  content: '';
  width: 0.85rem;
  height: 0.55rem;
  border-radius: 0.15rem;
}

.legend-item--continuous::before {
  background: rgba(74, 124, 89, 0.75);
}

.legend-item--detection::before {
  background: rgba(255, 180, 77, 0.65);
}

.legend-item--gap::before {
  background: rgba(0, 0, 0, 0.35);
  border: 1px solid rgba(124, 184, 138, 0.15);
}

.refresh-btn {
  padding: 0.4rem 0.75rem;
  border: 1px solid rgba(124, 184, 138, 0.45);
  border-radius: 0.4rem;
  background: rgba(74, 124, 89, 0.25);
  color: #c8e6c0;
  transition: background 0.15s ease;
}

.refresh-btn:hover:not(:disabled) {
  background: rgba(74, 124, 89, 0.4);
}

.refresh-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.recordings-error {
  margin: 0;
  padding: 0.75rem 0.9rem;
  border-radius: 0.45rem;
  background: rgba(255, 77, 77, 0.12);
  color: #ffb3b3;
  font-size: 0.9rem;
}

.recordings-status {
  margin: 0;
  font-size: 0.9rem;
  color: #8a9d86;
}

.recordings-selected {
  margin: 0.75rem 0 0;
  font-size: 0.82rem;
  color: #c8e6c0;
}
</style>
