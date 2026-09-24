<script setup lang="ts">
// Shown inside a Telegram Mini App when the shop behind the bot can't be loaded. A customer opened a shop, not
// Plum Market, so there is nothing here about accounts, registration or the merchant's panel.
import { t } from '../i18n'
</script>

<template>
  <div class="unavailable">
    <div class="big">🛍️</div>
    <h1>Магазин недоступен</h1>
    <p>Попробуйте открыть его чуть позже или напишите магазину в этом же чате.</p>
    <button class="s-btn" @click="$router.replace('/')">{{ t('retry') }}</button>
  </div>
</template>

<style scoped>
.unavailable {
  min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center;
  gap: 8px; padding: 24px; text-align: center; background: #f4f6fa; color: #152033;
  font-family: Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}
.big { font-size: 44px; }
h1 { font-size: 22px; margin: 0; }
p { margin: 0 0 8px; color: #4b5567; max-width: 320px; line-height: 1.5; }
.s-btn {
  height: 44px; padding: 0 22px; border: 0; border-radius: 12px; background: #1f7aec; color: #fff;
  font: inherit; font-weight: 600; cursor: pointer;
}
</style>
