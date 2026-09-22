<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../../api'
import Modal from '../../components/Modal.vue'
import { count, money } from '../../format'

const emit = defineEmits<{ close: [] }>()
const enabled = ref(false)
const spendPerPoint = ref(100)
const loaded = ref(false)
const saving = ref(false)
const error = ref('')
const saved = ref(false)

onMounted(async () => {
  const b = await api.bonus()
  enabled.value = b.enabled
  spendPerPoint.value = b.spendPerPoint
  loaded.value = true
})

// A worked example makes the ratio tangible: what a typical order earns.
const exampleOrder = 150_000
const examplePoints = computed(() => (spendPerPoint.value > 0 ? Math.floor(exampleOrder / spendPerPoint.value) : 0))
const cashbackPct = computed(() => (spendPerPoint.value > 0 ? (100 / spendPerPoint.value) : 0))

async function save() {
  saving.value = true
  error.value = ''
  try {
    await api.saveBonus({ enabled: enabled.value, spendPerPoint: Number(spendPerPoint.value) })
    saved.value = true
    setTimeout(() => emit('close'), 600)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Modal title="Настройки баллов" width="460px" persistent @close="emit('close')">
    <div v-if="!loaded" class="skeleton" style="height: 180px" />
    <div v-else class="stack">
      <label class="switch">
        <input v-model="enabled" type="checkbox" /><span class="track" />
        <span><b>Бонусная система</b><br /><small class="muted">Клиент получает баллы после выполненного заказа и может оплатить ими следующие покупки</small></span>
      </label>

      <label class="field">
        <span>Сколько нужно потратить для получения 1 балла</span>
        <div class="with-suffix">
          <input v-model.number="spendPerPoint" type="number" min="1" step="1" class="input" :disabled="!enabled" />
          <span>сум</span>
        </div>
      </label>

      <div class="rate">1 балл = 1 сум</div>

      <div class="example" :class="{ off: !enabled }">
        <div>Заказ на <b class="num">{{ money(exampleOrder) }}</b> → <b class="num">+{{ count(examplePoints) }} баллов</b></div>
        <div class="faint small">Фактически кешбэк {{ cashbackPct.toLocaleString('ru-RU', { maximumFractionDigits: 2 }) }}%</div>
      </div>

      <div v-if="error" class="error-banner">{{ error }}</div>
    </div>
    <template #footer>
      <button class="btn" @click="emit('close')">Отмена</button>
      <button class="btn btn-primary" :disabled="saving || !loaded || spendPerPoint < 1" @click="save">
        {{ saved ? 'Сохранено ✓' : 'Сохранить' }}
      </button>
    </template>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 18px; }
.switch { align-items: flex-start; }
.switch .track { margin-top: 2px; }
.with-suffix { display: flex; align-items: center; gap: 8px; }
.with-suffix .input { flex: 1; }
.rate { align-self: flex-start; font-size: 12px; font-weight: 600; padding: 4px 10px; border-radius: 12px; background: var(--plum-50); color: var(--plum-700); }
.example { padding: 12px 14px; border-radius: 10px; background: var(--good-bg); }
.example.off { opacity: .5; }
.small { font-size: 12px; }
</style>
