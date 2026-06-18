import { computed, ref, toValue, watch } from 'vue'
import { fetchRecordings, toTapoDate, warmPlaybackConnection } from '../api/tapo.js'

function hostEnvHint(label) {
  return label.includes('2')
    ? 'VITE_TAPO_HOST_2 or VITE_ONVIF_URI_2'
    : 'VITE_TAPO_HOST or VITE_ONVIF_URI'
}

export function useTapoRecordings(host, date, label = '') {
  const recordings = ref([])
  const responseDate = ref('')
  const total = ref(0)
  const loading = ref(false)
  const error = ref('')
  const hasFetched = ref(false)

  async function refresh() {
    const hostValue = toValue(host)
    const dateValue = toValue(date)

    if (!hostValue) {
      recordings.value = []
      responseDate.value = ''
      total.value = 0
      error.value = `Camera host is not configured. Set ${hostEnvHint(label)} in web/.env.`
      loading.value = false
      hasFetched.value = true
      return
    }

    loading.value = true
    error.value = ''

    try {
      const data = await fetchRecordings(hostValue, toTapoDate(dateValue))
      recordings.value = data.recordings ?? []
      responseDate.value = data.date ?? ''
      total.value = data.total ?? recordings.value.length
      warmPlaybackConnection(hostValue)
    } catch (err) {
      recordings.value = []
      responseDate.value = ''
      total.value = 0
      error.value = err instanceof Error ? err.message : 'Failed to load recordings.'
    } finally {
      loading.value = false
      hasFetched.value = true
    }
  }

  watch(
    () => [toValue(host), toValue(date)],
    refresh,
    { immediate: true },
  )

  const summary = computed(() => {
    if (loading.value || !hasFetched.value) {
      return 'Loading…'
    }

    if (error.value) {
      return 'Unavailable'
    }

    if (total.value > 0) {
      return `${total.value} clip${total.value === 1 ? '' : 's'}`
    }

    return 'No clips'
  })

  return {
    recordings,
    responseDate,
    total,
    loading,
    error,
    hasFetched,
    summary,
    refresh,
  }
}
