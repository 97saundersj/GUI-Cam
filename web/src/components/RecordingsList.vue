<script setup>
import { computed, ref } from 'vue'
import {
  formatDuration,
  formatTimelineTimeRange,
  recordingTypeLabel,
  toTapoDate,
} from '../api/tapo.js'
import RecordingTimeline from './RecordingTimeline.vue'
import RecordingsDatePicker from './RecordingsDatePicker.vue'

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

const emit = defineEmits(['select', 'update:date'])

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
    <div class="recordings-header">
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
      </button>

      <div class="recordings-meta">
        <span class="recordings-summary">{{ summary }}</span>
        <button
          type="button"
          class="refresh-btn"
          :disabled="loading"
          :aria-label="loading ? 'Loading recordings' : 'Refresh recordings'"
          @click="onRefresh"
        >
          <svg
            class="refresh-icon"
            :class="{ 'refresh-icon--spinning': loading }"
            viewBox="0 0 24 24"
            width="16"
            height="16"
            aria-hidden="true"
          >
            <path
              fill="currentColor"
              d="M17.65 6.35A7.958 7.958 0 0 0 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08a5.99 5.99 0 0 1-5.65 4c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4z"
            />
          </svg>
        </button>
      </div>
    </div>

    <div v-show="expanded" class="recordings-body">
      <div class="recordings-legend" aria-hidden="true">
        <span class="legend-item legend-item--continuous">Continuous</span>
        <span class="legend-item legend-item--detection">Detection</span>
        <span class="legend-item legend-item--gap">No recording</span>
      </div>

      <p v-if="error" class="recordings-error" role="alert">
        {{ error }}
      </p>

      <RecordingsDatePicker
        v-if="!error"
        class="recordings-date"
        :model-value="date"
        @update:model-value="emit('update:date', $event)"
      />

      <p v-if="!error && loading && recordings.length === 0" class="recordings-status">
        Loading recordings…
      </p>

      <p v-else-if="!error && !loading && recordings.length === 0" class="recordings-status">
        No recordings found for {{ responseDate || toTapoDate(date) }}.
      </p>

      <RecordingTimeline
        v-if="!error && recordings.length > 0"
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

.recordings-header {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.85rem 1rem;
  flex-wrap: wrap;
}

.recordings-toggle {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  flex: 1;
  min-width: 0;
  padding: 0;
  border: none;
  background: transparent;
  color: inherit;
  text-align: left;
  transition: background 0.15s ease;
  border-radius: 0.35rem;
}

.recordings-header:hover .recordings-toggle {
  background: transparent;
}

.recordings-header:has(.recordings-toggle:hover) {
  background: rgba(124, 184, 138, 0.08);
}

.recordings-meta {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  flex-shrink: 0;
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

.recordings-date {
  margin-bottom: 0.75rem;
}

.recordings-body {
  padding: 0 1rem 1rem;
  border-top: 1px solid rgba(124, 184, 138, 0.12);
}

.recordings-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin-bottom: 0.75rem;
  padding-top: 0.65rem;
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
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.75rem;
  height: 1.75rem;
  padding: 0;
  border: 1px solid rgba(124, 184, 138, 0.35);
  border-radius: 0.4rem;
  background: rgba(74, 124, 89, 0.2);
  color: #a8b8a4;
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.refresh-btn:hover:not(:disabled) {
  background: rgba(74, 124, 89, 0.4);
  color: #c8e6c0;
}

.refresh-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.refresh-icon--spinning {
  animation: refresh-spin 0.8s linear infinite;
}

@keyframes refresh-spin {
  to {
    transform: rotate(360deg);
  }
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
