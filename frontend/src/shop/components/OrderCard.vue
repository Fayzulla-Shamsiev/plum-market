<script setup lang="ts">
import type { ShopOrder } from '../api'
import { count, dateTime, price, t } from '../i18n'
import { statusText, tone } from '../orderStatus'
import ProductImage from './ProductImage.vue'
import SIcon from './SIcon.vue'

// One order in "Мои заказы": ID, status, date and time, recipient, number of items, total.
defineProps<{ o: ShopOrder }>()
defineEmits<{ cancel: [] }>()
</script>

<template>
  <article class="oc">
    <RouterLink :to="`/profile/orders/${o.id}`" class="top">
      <div>
        <div class="id">{{ t('order') }} №{{ o.id }}</div>
        <div class="faint small">{{ dateTime(o.createdAt) }}</div>
      </div>
      <span class="st-badge" :class="tone(o.status)">{{ statusText(o.status, o.deliveryType) }}</span>
    </RouterLink>
    <RouterLink :to="`/profile/orders/${o.id}`" class="thumbs">
      <span v-for="i in o.items.slice(0, 5)" :key="i.productId + (i.variant ?? '')" class="th"><ProductImage :id="i.productId" :name="i.name" :src="i.image" /></span>
      <span v-if="o.items.length > 5" class="th more">+{{ o.items.length - 5 }}</span>
    </RouterLink>
    <dl>
      <dt>{{ t('recipient') }}</dt><dd>{{ o.recipientName }}, {{ o.recipientPhone }}</dd>
      <dt>{{ t('receive') }}</dt><dd>{{ o.deliveryType === 'Pickup' ? `${t('pickup')} · ${o.branch.name}` : `${t('delivery')} · ${o.address}` }}</dd>
    </dl>
    <div class="bottom">
      <span class="faint">{{ count(o.itemsCount, 'items') }}</span>
      <strong>{{ price(o.total) }}</strong>
    </div>
    <div class="actions">
      <RouterLink :to="`/profile/orders/${o.id}`" class="s-btn ghost">{{ t('more') }} <SIcon name="chevronRight" :size="16" /></RouterLink>
      <button v-if="o.canCancel" type="button" class="cancel" @click="$emit('cancel')">{{ t('cancelOrder') }}</button>
    </div>
  </article>
</template>

<style scoped>
.oc { background: var(--card); border-radius: 20px; padding: 18px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 12px; }
.top { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
.id { font-weight: 700; font-size: 17px; }
.faint { color: var(--ink-3); }
.small { font-size: 13px; }
.st-badge { font-size: 13px; font-weight: 650; padding: 5px 10px; border-radius: 999px; white-space: nowrap; }
.st-badge.amber { background: #fff4de; color: #b06a00; }
.st-badge.blue { background: var(--blue-50); color: var(--blue-600); }
.st-badge.green { background: var(--green-50); color: #0d7a45; }
.st-badge.grey { background: var(--page); color: var(--ink-3); }
.thumbs { display: flex; gap: 8px; }
.th { width: 54px; height: 54px; border-radius: 12px; overflow: hidden; flex: none; }
.th :deep(.ph) { font-size: 26px; }
.th.more { display: grid; place-items: center; background: var(--page); color: var(--ink-2); font-weight: 650; }
dl { display: grid; grid-template-columns: auto 1fr; gap: 4px 14px; margin: 0; font-size: 14px; }
dt { color: var(--ink-3); }
dd { margin: 0; min-width: 0; overflow-wrap: anywhere; }
.bottom { display: flex; justify-content: space-between; align-items: baseline; border-top: 1px solid var(--line); padding-top: 12px; }
.bottom strong { font-size: 19px; }
.actions { display: flex; gap: 10px; align-items: center; }
.actions .s-btn { height: 40px; padding: 0 14px; }
.cancel { margin-left: auto; border: 0; background: none; color: var(--red); font-weight: 600; cursor: pointer; }
</style>
