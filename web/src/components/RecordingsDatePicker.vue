<script setup>
import { computed } from 'vue'
import { VueDatePicker } from '@vuepic/vue-datepicker'
import '@vuepic/vue-datepicker/dist/main.css'
import { todayIsoDate } from '../api/tapo.js'

const props = defineProps({
  modelValue: {
    type: String,
    required: true,
  },
})

const emit = defineEmits(['update:modelValue'])

const maxDate = todayIsoDate()

const displayLabel = computed(() => {
  const [year, month, day] = props.modelValue.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  return date.toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
})

const isToday = computed(() => props.modelValue === maxDate)

function shiftDays(delta) {
  const [year, month, day] = props.modelValue.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  date.setDate(date.getDate() + delta)

  const nextYear = date.getFullYear()
  const nextMonth = String(date.getMonth() + 1).padStart(2, '0')
  const nextDay = String(date.getDate()).padStart(2, '0')
  const iso = `${nextYear}-${nextMonth}-${nextDay}`

  if (iso > maxDate) {
    return
  }

  emit('update:modelValue', iso)
}

function prevDay() {
  shiftDays(-1)
}

function nextDay() {
  if (!isToday.value) {
    shiftDays(1)
  }
}

function onDateUpdate(value) {
  if (value && value <= maxDate) {
    emit('update:modelValue', value)
  }
}
</script>

<template>
  <div class="date-picker" @click.stop>
    <button
      type="button"
      class="date-nav-btn"
      aria-label="Previous day"
      @click="prevDay"
    >
      <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
        <path fill="currentColor" d="M15.41 7.41 14 6l-6 6 6 6 1.41-1.41L10.83 12z" />
      </svg>
    </button>

    <VueDatePicker
      :model-value="modelValue"
      model-type="format"
      format="yyyy-MM-dd"
      :enable-time-picker="false"
      :max-date="maxDate"
      auto-apply
      :clearable="false"
      week-start="1"
      class="date-picker-input"
      @update:model-value="onDateUpdate"
    >
      <template #trigger>
        <button type="button" class="date-trigger" aria-label="Choose recording date">
          <svg class="date-trigger-icon" viewBox="0 0 24 24" width="15" height="15" aria-hidden="true">
            <path
              fill="currentColor"
              d="M19 4h-1V2h-2v2H8V2H6v2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2m0 16H5V10h14zm0-12H5V6h14z"
            />
          </svg>
          <span class="date-trigger-label">{{ displayLabel }}</span>
        </button>
      </template>
    </VueDatePicker>

    <button
      type="button"
      class="date-nav-btn"
      :disabled="isToday"
      aria-label="Next day"
      @click="nextDay"
    >
      <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
        <path fill="currentColor" d="M8.59 16.59 13.17 12 8.59 7.41 10 6l6 6-6 6z" />
      </svg>
    </button>
  </div>
</template>

<style scoped>
.date-picker {
  display: inline-flex;
  align-items: center;
  gap: 0.15rem;
  padding: 0.2rem;
  border-radius: 0.55rem;
  border: 1px solid rgba(124, 184, 138, 0.28);
  background: rgba(10, 13, 11, 0.75);
}

.date-nav-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.75rem;
  height: 1.75rem;
  padding: 0;
  border: none;
  border-radius: 0.4rem;
  background: transparent;
  color: #a8b8a4;
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.date-nav-btn:hover:not(:disabled) {
  background: rgba(124, 184, 138, 0.15);
  color: #d8f0d4;
}

.date-nav-btn:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.date-picker-input {
  --dp-font-family: inherit;
  --dp-border-radius: 0.45rem;
  --dp-cell-border-radius: 0.35rem;
  --dp-common-transition: 0.15s ease;
  --dp-button-height: 2rem;
  --dp-month-year-row-height: 2rem;
  --dp-month-year-row-button-size: 2rem;
  --dp-button-icon-height: 1.1rem;
  --dp-cell-size: 2rem;
  --dp-action-row-height: 2.5rem;
  --dp-font-size: 0.85rem;
  --dp-preview-font-size: 0.8rem;
  --dp-background-color: #141a15;
  --dp-text-color: #e8f0e6;
  --dp-hover-color: rgba(124, 184, 138, 0.18);
  --dp-hover-text-color: #f2f7f0;
  --dp-hover-icon-color: #c8e6c0;
  --dp-primary-color: #4a7c59;
  --dp-primary-disabled-color: rgba(74, 124, 89, 0.45);
  --dp-primary-text-color: #f2f7f0;
  --dp-secondary-color: rgba(124, 184, 138, 0.2);
  --dp-border-color: rgba(124, 184, 138, 0.25);
  --dp-menu-border-color: rgba(124, 184, 138, 0.3);
  --dp-disabled-color: rgba(168, 184, 164, 0.35);
  --dp-scroll-bar-background: #1a221c;
  --dp-scroll-bar-color: #4a7c59;
  --dp-icon-color: #7cb88a;
  --dp-danger-color: #e88;
  --dp-marker-color: #ffb44d;
  --dp-highlight-color: rgba(74, 124, 89, 0.35);
}

.date-trigger {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 0;
  padding: 0.35rem 0.55rem;
  border: none;
  border-radius: 0.4rem;
  background: transparent;
  color: #e8f0e6;
  font-size: 0.82rem;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition: background 0.15s ease;
}

.date-trigger:hover {
  background: rgba(124, 184, 138, 0.12);
}

.date-trigger-icon {
  flex-shrink: 0;
  color: #7cb88a;
}

.date-trigger-label {
  overflow: hidden;
  text-overflow: ellipsis;
}

@media (max-width: 520px) {
  .date-trigger-label {
    max-width: 7.5rem;
  }
}
</style>
