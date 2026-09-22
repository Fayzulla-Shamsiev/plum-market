<script setup lang="ts">
/** How a bot message / channel post will look in Telegram: photo, caption and one inline button. */
defineProps<{ title: string; text: string; imageUrl?: string | null; buttonText?: string | null; channel?: boolean }>()
</script>

<template>
  <div class="tg">
    <div class="tg-head">
      <span class="ava">{{ title[0]?.toUpperCase() }}</span>
      <div><b>{{ title }}</b><small>{{ channel ? 'канал' : 'бот' }}</small></div>
    </div>
    <div class="tg-body">
      <div class="msg">
        <img v-if="imageUrl" :src="imageUrl" alt="" />
        <p :class="{ empty: !text.trim() }">{{ text.trim() || 'Текст сообщения…' }}</p>
        <span class="time">{{ new Date().toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' }) }}</span>
      </div>
      <div v-if="buttonText" class="btn-inline">{{ buttonText }}</div>
    </div>
  </div>
</template>

<style scoped>
.tg { width: 320px; max-width: 100%; border-radius: 18px; overflow: hidden; border: 1px solid var(--border); box-shadow: var(--shadow); background: #fff; }
.tg-head { display: flex; align-items: center; gap: 10px; padding: 10px 14px; background: #517da2; color: #fff; }
.tg-head small { display: block; opacity: .8; font-size: 11px; }
.ava { width: 34px; height: 34px; border-radius: 50%; background: #7ea7c7; display: grid; place-items: center; font-weight: 700; }
.tg-body { padding: 14px 12px 16px; background: #c9d9e4 linear-gradient(135deg, #cfdde7, #b9cfdd); min-height: 200px; display: flex; flex-direction: column; justify-content: flex-end; }
.msg { background: #fff; border-radius: 14px 14px 14px 4px; overflow: hidden; box-shadow: 0 1px 1px rgb(0 0 0 / 12%); position: relative; }
.msg img { display: block; width: 100%; max-height: 220px; object-fit: cover; }
.msg p { margin: 0; padding: 8px 12px 18px; white-space: pre-wrap; word-wrap: break-word; font-size: 14px; }
.msg p.empty { color: var(--text-3); }
.time { position: absolute; right: 10px; bottom: 4px; font-size: 11px; color: #8aa0b0; }
.btn-inline { margin-top: 4px; text-align: center; padding: 8px; border-radius: 10px; background: rgb(0 0 0 / 22%); color: #fff; font-weight: 600; font-size: 13px; }
</style>
