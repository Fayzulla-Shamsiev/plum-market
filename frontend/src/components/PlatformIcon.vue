<script setup lang="ts">
import type { ChatChannel, Platform } from '../api'
import { channelLabel } from '../format'

// Accepts both storefront platforms and chat channels (which add Wolt).
withDefaults(defineProps<{ platform: Platform | ChatChannel; showLabel?: boolean }>(), { showLabel: false })
</script>

<template>
  <span class="pf" :title="channelLabel[platform]">
    <svg v-if="platform === 'Telegram'" viewBox="0 0 24 24" class="ic tg" aria-hidden="true">
      <circle cx="12" cy="12" r="12" fill="#27A6E5" />
      <path d="M5.4 11.8l11.1-4.3c.5-.2 1 .1.8.9l-1.9 8.9c-.1.6-.5.8-1 .5l-2.8-2.1-1.4 1.3c-.2.2-.3.3-.6.3l.2-2.9 5.3-4.8c.2-.2 0-.3-.3-.1L8.3 13l-2.8-.9c-.6-.2-.6-.6.1-.9z" fill="#fff" />
    </svg>
    <svg v-else-if="platform === 'Instagram'" viewBox="0 0 24 24" class="ic" aria-hidden="true">
      <defs>
        <linearGradient id="ig" x1="0" y1="1" x2="1" y2="0">
          <stop offset="0" stop-color="#f9a13a" /><stop offset=".5" stop-color="#e1306c" /><stop offset="1" stop-color="#833ab4" />
        </linearGradient>
      </defs>
      <rect width="24" height="24" rx="7" fill="url(#ig)" />
      <rect x="6" y="6" width="12" height="12" rx="4" fill="none" stroke="#fff" stroke-width="1.8" />
      <circle cx="12" cy="12" r="2.8" fill="none" stroke="#fff" stroke-width="1.8" />
      <circle cx="16.2" cy="7.8" r=".9" fill="#fff" />
    </svg>
    <svg v-else-if="platform === 'Wolt'" viewBox="0 0 24 24" class="ic" aria-hidden="true">
      <circle cx="12" cy="12" r="12" fill="#00c2e8" />
      <path d="M5.5 8.5l2.2 7 2.1-5.6 2.2 5.6 2.1-5.6 2.2 5.6 2.2-7" fill="none" stroke="#fff" stroke-width="1.7" stroke-linejoin="round" stroke-linecap="round" />
    </svg>
    <svg v-else viewBox="0 0 24 24" class="ic" aria-hidden="true">
      <circle cx="12" cy="12" r="12" fill="#eb6834" />
      <path d="M12 5.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13zM5.5 12h13M12 5.5c1.8 1.8 2.6 4 2.6 6.5s-.8 4.7-2.6 6.5c-1.8-1.8-2.6-4-2.6-6.5s.8-4.7 2.6-6.5z"
            fill="none" stroke="#fff" stroke-width="1.3" />
    </svg>
    <span v-if="showLabel">{{ channelLabel[platform] }}</span>
  </span>
</template>

<style scoped>
.pf { display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; }
.ic { width: 20px; height: 20px; flex: none; }
</style>
