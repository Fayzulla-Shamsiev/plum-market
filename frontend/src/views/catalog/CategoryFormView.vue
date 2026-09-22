<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { catalogApi, type Category, type CategoryRow } from '../../api'
import Icon from '../../components/Icon.vue'
import LocalizedEditor from '../../components/LocalizedEditor.vue'
import SingleImage from '../../components/SingleImage.vue'
import { loc } from '../../format'

const props = defineProps<{ id?: string }>()
const router = useRouter()
const isEdit = computed(() => !!props.id)

const form = ref<Category>({
  parentId: null, name: {}, description: {}, imageUrl: null, bannerUrl: null,
  layout: 'grid2', productSort: 'manual', isActive: true,
})
// "создание новой категории или привязка к существующей"
const mode = ref<'root' | 'child'>('root')
const all = ref<CategoryRow[]>([])
const hasBanner = ref(false)
const loading = ref(true)
const saving = ref(false)
const error = ref('')

onMounted(async () => {
  all.value = await catalogApi.categories()
  if (props.id) {
    const c = await catalogApi.category(Number(props.id))
    form.value = { ...c, name: c.name ?? {}, description: c.description ?? {} }
    mode.value = c.parentId ? 'child' : 'root'
    hasBanner.value = !!c.bannerUrl
  }
  loading.value = false
})

// A category can't be nested under itself or its own descendants.
const parentOptions = computed(() => {
  const blocked = new Set<number>()
  if (props.id) {
    const walk = (id: number) => { blocked.add(id); all.value.filter(c => c.parentId === id).forEach(c => walk(c.id)) }
    walk(Number(props.id))
  }
  return all.value.filter(c => !blocked.has(c.id))
})

const layouts = [
  { value: 'grid2', label: 'Плитка, 2 в ряд', cols: 2 },
  { value: 'grid3', label: 'Плитка, 3 в ряд', cols: 3 },
  { value: 'list', label: 'Список', cols: 1 },
]
const sorts = [
  { value: 'manual', label: 'Вручную (как в каталоге)' },
  { value: 'popular', label: 'Сначала популярные' },
  { value: 'new', label: 'Сначала новые' },
  { value: 'price_asc', label: 'Сначала дешёвые' },
  { value: 'price_desc', label: 'Сначала дорогие' },
]

async function save() {
  saving.value = true
  error.value = ''
  try {
    await catalogApi.saveCategory({
      ...form.value,
      parentId: mode.value === 'child' ? form.value.parentId : null,
      bannerUrl: hasBanner.value ? form.value.bannerUrl : null,
    })
    router.push('/products/categories')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="page narrow">
    <div class="page-head">
      <RouterLink to="/products/categories" class="btn btn-ghost btn-icon" aria-label="Назад"><Icon name="chevronLeft" /></RouterLink>
      <h1>{{ isEdit ? 'Редактировать категорию' : 'Новая категория' }}</h1>
    </div>

    <div v-if="loading" class="skeleton" style="height: 420px" />
    <form v-else class="stack" @submit.prevent="save">
      <section class="card card-pad">
        <h2>Название и описание</h2>
        <LocalizedEditor v-model:name="form.name" v-model:description="form.description" kind="category" />
      </section>

      <section class="card card-pad">
        <h2>Расположение в каталоге</h2>
        <div class="seg-choice">
          <label :class="{ on: mode === 'root' }"><input v-model="mode" type="radio" value="root" /><b>Новая категория</b><small>Отдельный раздел в меню магазина</small></label>
          <label :class="{ on: mode === 'child' }"><input v-model="mode" type="radio" value="child" /><b>Подкатегория</b><small>Привязать к существующей категории</small></label>
        </div>
        <label v-if="mode === 'child'" class="field">
          <span>Родительская категория</span>
          <select v-model="form.parentId" class="select" required>
            <option :value="null" disabled>Выберите категорию</option>
            <option v-for="c in parentOptions" :key="c.id" :value="c.id">{{ c.parentId ? '— ' : '' }}{{ loc(c.name) }}</option>
          </select>
        </label>
        <label class="switch">
          <input v-model="form.isActive" type="checkbox" /><span class="track" />Показывать в магазине
        </label>
      </section>

      <section class="card card-pad">
        <h2>Изображение</h2>
        <SingleImage v-model="form.imageUrl" label="Изображение категории" />
      </section>

      <section class="card card-pad">
        <h2>Расположение товаров</h2>
        <div class="layouts">
          <label v-for="l in layouts" :key="l.value" class="layout" :class="{ on: form.layout === l.value }">
            <input v-model="form.layout" type="radio" :value="l.value" />
            <span class="preview" :style="{ gridTemplateColumns: `repeat(${l.cols}, 1fr)` }">
              <i v-for="n in l.cols * 2" :key="n" :class="{ line: l.cols === 1 }" />
            </span>
            {{ l.label }}
          </label>
        </div>
        <label class="field">
          <span>Порядок товаров</span>
          <select v-model="form.productSort" class="select">
            <option v-for="s in sorts" :key="s.value" :value="s.value">{{ s.label }}</option>
          </select>
        </label>
      </section>

      <section class="card card-pad">
        <label class="switch"><input v-model="hasBanner" type="checkbox" /><span class="track" /><b>Добавить баннер</b></label>
        <p class="muted small">Широкая картинка над товарами категории (рекомендуется 1200×400).</p>
        <SingleImage v-if="hasBanner" v-model="form.bannerUrl" label="Загрузить баннер" wide />
      </section>

      <div v-if="error" class="error-banner">{{ error }}</div>
      <div class="actions">
        <RouterLink to="/products/categories" class="btn">Отмена</RouterLink>
        <button type="submit" class="btn btn-primary" :disabled="saving">{{ saving ? 'Сохранение…' : 'Сохранить' }}</button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.narrow { max-width: 820px; }
.stack { display: flex; flex-direction: column; gap: 16px; }
.card h2 { margin-bottom: 12px; }
.card > * + * { margin-top: 12px; }
.seg-choice { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.seg-choice label, .layout { border: 1px solid var(--border-strong); border-radius: 10px; padding: 10px 12px; cursor: pointer; display: flex; flex-direction: column; gap: 2px; }
.seg-choice input, .layout input { position: absolute; opacity: 0; }
.seg-choice small { color: var(--text-2); font-size: 12px; }
.seg-choice .on, .layout.on { border-color: var(--plum-500); background: var(--plum-50); box-shadow: 0 0 0 1px var(--plum-500); }
.layouts { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
.layout { align-items: center; font-size: 13px; font-weight: 550; gap: 8px; }
.preview { display: grid; gap: 4px; width: 72px; }
.preview i { height: 22px; border-radius: 4px; background: var(--plum-100); }
.preview i.line { height: 9px; }
.actions { display: flex; justify-content: flex-end; gap: 8px; }
.small { font-size: 12px; margin: 4px 0 0; }
@media (max-width: 600px) { .seg-choice, .layouts { grid-template-columns: 1fr; } }
</style>
