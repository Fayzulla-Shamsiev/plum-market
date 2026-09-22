<script setup lang="ts">
import { ref } from 'vue'
import { shopApi, type ShopOrder } from '../api'
import { t } from '../i18n'
import { showToast } from '../state/toast'
import SModal from './SModal.vue'

// Confirms "Отменить заказ", with an optional reason for the merchant.
const props = defineProps<{ order: ShopOrder }>()
const emit = defineEmits<{ close: []; done: [order: ShopOrder] }>()
const reason = ref('')
const busy = ref(false)
const error = ref('')
async function confirm() {
  busy.value = true
  error.value = ''
  try {
    const o = await shopApi.cancelOrder(props.order.id, reason.value.trim() || undefined)
    showToast(t('orderCancelled'))
    emit('done', o)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <SModal :title="`${t('cancelAsk')} №${order.id}`" @close="emit('close')">
    <label class="lbl">
      <span>{{ t('cancelReason') }}</span>
      <textarea v-model="reason" class="s-input" rows="2" maxlength="300" />
    </label>
    <p v-if="error" class="err">{{ error }}</p>
    <template #footer>
      <button type="button" class="s-btn ghost" @click="emit('close')">{{ t('keepOrder') }}</button>
      <button type="button" class="s-btn danger" :disabled="busy" @click="confirm">{{ t('cancelOrder') }}</button>
    </template>
  </SModal>
</template>

<style scoped>
.lbl { display: flex; flex-direction: column; gap: 6px; font-size: 14px; color: var(--ink-2); }
.err { color: var(--red); font-size: 13px; }
.danger { background: var(--red); }
.danger:hover { background: #c93b40; }
</style>
