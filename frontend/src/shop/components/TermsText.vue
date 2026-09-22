<script setup lang="ts">
import { computed } from 'vue'

// Renders the merchant's terms text: blocks separated by blank lines, "## " starts a heading.
const props = defineProps<{ text: string | null }>()
const blocks = computed(() => (props.text ?? '').split(/\n\s*\n/).map(b => b.trim()).filter(Boolean).map(b => {
  const [first, ...rest] = b.split('\n')
  return first!.startsWith('## ') ? { title: first!.slice(3), body: rest.join('\n') } : { title: '', body: b }
}))
</script>

<template>
  <div class="terms">
    <section v-for="(b, i) in blocks" :key="i">
      <h2 v-if="b.title">{{ b.title }}</h2>
      <p v-if="b.body">{{ b.body }}</p>
    </section>
  </div>
</template>

<style scoped>
.terms { display: flex; flex-direction: column; gap: 18px; }
h2 { font-size: 18px; margin: 0 0 6px; }
p { margin: 0; line-height: 1.65; color: var(--ink-2); white-space: pre-line; }
</style>
