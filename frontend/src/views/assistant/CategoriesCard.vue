<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../../api'
import Icon from '../../components/Icon.vue'
import PhotoSlot from './PhotoSlot.vue'

// Under «Создано категорий»: the new categories, each with a photo slot — a catalog of tiles looks empty without them.
const props = defineProps<{ title: string; categoryIds: number[]; link: string | null; linkLabel: string | null }>()

const rows = ref<{ id: number; name: string; imageUrl: string | null; products: number }[] | null>(null)
const error = ref('')
onMounted(async () => {
  try {
    rows.value = await api.assistantCategories(props.categoryIds)
  } catch (e) {
    error.value = (e as Error).message
  }
})
const missing = computed(() => rows.value?.filter(r => !r.imageUrl).length ?? 0)
</script>

<template>
  <div class="cc">
    <div class="cc-title"><span class="ok"><Icon name="check" /></span>{{ title }}</div>
    <p v-if="error" class="err">{{ error }}</p>
    <ul v-else-if="rows" class="tiles">
      <li v-for="r in rows" :key="r.id">
        <PhotoSlot kind="category" :id="r.id" :url="r.imageUrl" :label="r.name" :size="52" @changed="u => (r.imageUrl = u)" />
        <span class="name">{{ r.name }}</span>
      </li>
    </ul>
    <p v-if="rows && missing" class="hint">Нажмите на квадрат, чтобы добавить фото категории — так каталог выглядит живым. Необязательно.</p>
    <RouterLink v-if="link" :to="link" class="link">{{ linkLabel ?? 'Открыть' }} →</RouterLink>
  </div>
</template>

<style scoped>
.cc { display: flex; flex-direction: column; gap: 10px; }
.cc-title { display: flex; align-items: center; gap: 8px; font-weight: 650; }
.ok { width: 20px; height: 20px; flex: none; border-radius: 50%; display: grid; place-items: center; background: #e3f8ec; color: #139a55; }
.ok svg { width: 13px; height: 13px; }
.tiles { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(92px, 1fr)); gap: 10px; }
.tiles li { display: flex; flex-direction: column; align-items: center; gap: 5px; text-align: center; }
.name { font-size: 12.5px; color: var(--ink-2); line-height: 1.25; }
.hint { margin: 0; font-size: 12.5px; color: var(--ink-3); }
.err { margin: 0; color: #b42318; font-size: 13px; }
.link { font-size: 13.5px; font-weight: 600; color: var(--blue); align-self: flex-start; }
.link:hover { text-decoration: underline; }
</style>
