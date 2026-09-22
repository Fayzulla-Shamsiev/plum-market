<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { catalogApi, type CategoryRow, type Product } from '../../api'
import Icon from '../../components/Icon.vue'
import LocalizedEditor from '../../components/LocalizedEditor.vue'
import MediaUploader from '../../components/MediaUploader.vue'
import { loc, money, units } from '../../format'
import { useLookups } from '../../store'

const props = defineProps<{ id?: string }>()
const router = useRouter()
const { lookups } = useLookups()
const isEdit = computed(() => !!props.id)

const form = ref<Product>({
  categoryId: null, name: {}, description: {}, price: 0, oldPrice: null, costPrice: 0, unit: 'шт',
  weightGrams: null, lengthCm: null, widthCm: null, heightCm: null,
  tags: [], attributes: [], variants: [], media: [], isActive: true, branchIds: [],
})
const categories = ref<CategoryRow[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const tagInput = ref('')

onMounted(async () => {
  categories.value = await catalogApi.categories()
  if (props.id) {
    const p = await catalogApi.product(Number(props.id))
    form.value = { ...p, name: p.name ?? {}, description: p.description ?? {} }
  }
  loading.value = false
})

// New products are available in every branch by default.
if (!props.id)
  watch(lookups, l => { if (l && !form.value.branchIds.length) form.value.branchIds = l.branches.map(b => b.id) }, { immediate: true })

const categoryOptions = computed(() => {
  const out: { id: number; label: string }[] = []
  const walk = (parent: number | null, depth: number) => {
    for (const c of categories.value.filter(x => x.parentId === parent)) {
      out.push({ id: c.id, label: `${'— '.repeat(depth)}${loc(c.name)}` })
      walk(c.id, depth + 1)
    }
  }
  walk(null, 0)
  return out
})
const categoryName = computed(() => loc(categories.value.find(c => c.id === form.value.categoryId)?.name))

const profit = computed(() => (form.value.price || 0) - (form.value.costPrice || 0))
const margin = computed(() => (form.value.price > 0 ? (profit.value / form.value.price) * 100 : 0))
const discountPct = computed(() =>
  form.value.oldPrice && form.value.oldPrice > form.value.price ? Math.round((1 - form.value.price / form.value.oldPrice) * 100) : 0)

function addTag() {
  const parts = tagInput.value.split(',').map(t => t.trim()).filter(Boolean)
  form.value.tags = [...new Set([...form.value.tags, ...parts])]
  tagInput.value = ''
}
function onTagKey(e: KeyboardEvent) {
  if (e.key === 'Enter' || e.key === ',') { e.preventDefault(); addTag() }
  if (e.key === 'Backspace' && !tagInput.value) form.value.tags = form.value.tags.slice(0, -1)
}

function toggleBranch(id: number) {
  const s = new Set(form.value.branchIds)
  s.has(id) ? s.delete(id) : s.add(id)
  form.value.branchIds = [...s]
}

// Empty number inputs come back as '' from v-model.number; normalise to null.
const n = (v: unknown) => (v === '' || v === undefined || v === null ? null : Number(v))

async function save() {
  saving.value = true
  error.value = ''
  try {
    const f = form.value
    await catalogApi.saveProduct({
      ...f,
      oldPrice: n(f.oldPrice), weightGrams: n(f.weightGrams), lengthCm: n(f.lengthCm), widthCm: n(f.widthCm), heightCm: n(f.heightCm),
      costPrice: n(f.costPrice) ?? 0,
      variants: f.variants.map(v => ({ ...v, price: n(v.price) })),
    })
    router.push('/products/items')
  } catch (e) {
    error.value = (e as Error).message
    window.scrollTo({ top: 0, behavior: 'smooth' })
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <RouterLink to="/products/items" class="btn btn-ghost btn-icon" aria-label="Назад"><Icon name="chevronLeft" /></RouterLink>
      <h1>{{ isEdit ? loc(form.name) || 'Редактировать продукт' : 'Новый продукт' }}</h1>
    </div>
    <div v-if="error" class="error-banner">{{ error }}</div>
    <div v-if="loading" class="skeleton" style="height: 520px" />

    <form v-else class="layout" @submit.prevent="save">
      <div class="main">
        <section class="card card-pad">
          <h2>Название и описание</h2>
          <LocalizedEditor v-model:name="form.name" v-model:description="form.description" kind="product"
                           :context="{ category: categoryName, attributes: form.attributes, unit: form.unit, weightGrams: form.weightGrams }" />
        </section>

        <section class="card card-pad">
          <h2>Фото и видео</h2>
          <MediaUploader v-model="form.media" />
        </section>

        <section class="card card-pad">
          <h2>Цена</h2>
          <div class="grid3">
            <label class="field"><span>Цена, сум *</span><input v-model.number="form.price" type="number" min="1" class="input" required /></label>
            <label class="field"><span>Единица измерения</span>
              <select v-model="form.unit" class="select"><option v-for="u in units" :key="u" :value="u">{{ u }}</option></select>
            </label>
            <label class="field"><span>Старая цена, сум</span><input v-model.number="form.oldPrice" type="number" min="0" class="input" placeholder="Для зачёркнутой цены" /></label>
            <label class="field"><span>Себестоимость, сум</span><input v-model.number="form.costPrice" type="number" min="0" class="input" /></label>
            <div class="field"><span>Прибыль</span><div class="calc num" :class="{ neg: profit < 0 }">{{ money(profit) }}</div></div>
            <div class="field"><span>Маржа</span><div class="calc num" :class="{ neg: margin < 0 }">{{ margin.toFixed(1) }}%</div></div>
          </div>
          <p v-if="discountPct" class="hint">Покупатель увидит скидку −{{ discountPct }}% (зачёркнутая цена {{ money(form.oldPrice!) }})</p>
        </section>

        <section class="card card-pad">
          <div class="sec-head">
            <h2>Варианты товара</h2>
            <button type="button" class="btn btn-sm" @click="form.variants.push({ name: '', price: null, sku: null })"><Icon name="plus" />Добавить</button>
          </div>
          <p class="muted small">Размер, объём, начинка… Цена варианта заменяет основную; пусто — как у товара.</p>
          <div v-for="(v, i) in form.variants" :key="i" class="line">
            <input v-model="v.name" class="input grow" placeholder="Например: 30 см" :aria-label="`Вариант ${i + 1}`" />
            <input v-model.number="v.price" type="number" min="0" class="input w140" placeholder="Цена" aria-label="Цена варианта" />
            <input v-model="v.sku" class="input w140" placeholder="Артикул" aria-label="Артикул" />
            <button type="button" class="btn btn-ghost btn-icon btn-danger" aria-label="Удалить вариант" @click="form.variants.splice(i, 1)"><Icon name="trash" /></button>
          </div>
        </section>

        <section class="card card-pad">
          <div class="sec-head">
            <h2>Дополнительные характеристики</h2>
            <button type="button" class="btn btn-sm" @click="form.attributes.push({ name: '', value: '' })"><Icon name="plus" />Добавить</button>
          </div>
          <div v-for="(a, i) in form.attributes" :key="i" class="line">
            <input v-model="a.name" class="input grow" placeholder="Характеристика (например, Тесто)" aria-label="Название характеристики" />
            <input v-model="a.value" class="input grow" placeholder="Значение (например, тонкое)" aria-label="Значение" />
            <button type="button" class="btn btn-ghost btn-icon btn-danger" aria-label="Удалить характеристику" @click="form.attributes.splice(i, 1)"><Icon name="trash" /></button>
          </div>
          <p v-if="!form.attributes.length" class="faint small">Нет характеристик</p>
        </section>

        <section class="card card-pad">
          <h2>Размеры и вес</h2>
          <div class="grid4">
            <label class="field"><span>Вес, г</span><input v-model.number="form.weightGrams" type="number" min="0" class="input" /></label>
            <label class="field"><span>Длина, см</span><input v-model.number="form.lengthCm" type="number" min="0" class="input" /></label>
            <label class="field"><span>Ширина, см</span><input v-model.number="form.widthCm" type="number" min="0" class="input" /></label>
            <label class="field"><span>Высота, см</span><input v-model.number="form.heightCm" type="number" min="0" class="input" /></label>
          </div>
        </section>
      </div>

      <aside class="side">
        <section class="card card-pad">
          <label class="switch"><input v-model="form.isActive" type="checkbox" /><span class="track" /><b>{{ form.isActive ? 'Активен' : 'Неактивен' }}</b></label>
          <p class="muted small">Неактивный товар скрыт в магазине.</p>
        </section>
        <section class="card card-pad">
          <label class="field"><span>Категория</span>
            <select v-model="form.categoryId" class="select">
              <option :value="null">Без категории</option>
              <option v-for="c in categoryOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
            </select>
          </label>
        </section>
        <section class="card card-pad">
          <h3>Доступен в филиалах</h3>
          <label v-for="b in lookups?.branches" :key="b.id" class="check">
            <input type="checkbox" :checked="form.branchIds.includes(b.id)" @change="toggleBranch(b.id)" />{{ b.name }}
          </label>
          <p v-if="!form.branchIds.length" class="warn small">Товар не будет виден ни в одном филиале</p>
        </section>
        <section class="card card-pad">
          <h3>Теги</h3>
          <div class="tags">
            <span v-for="t in form.tags" :key="t" class="tag">{{ t }}<button type="button" :aria-label="`Удалить тег ${t}`" @click="form.tags = form.tags.filter(x => x !== t)">×</button></span>
            <input v-model="tagInput" class="tag-input" placeholder="новинка, хит…" aria-label="Добавить тег" @keydown="onTagKey" @blur="addTag" />
          </div>
        </section>
        <section v-if="isEdit && form.ikpu" class="card card-pad">
          <h3>ИКПУ</h3>
          <div class="num">{{ form.ikpu }}</div>
          <RouterLink to="/products/ikpu" class="small">Изменить в разделе ИКПУ</RouterLink>
        </section>
      </aside>

      <div class="savebar">
        <RouterLink to="/products/items" class="btn">Отмена</RouterLink>
        <button type="submit" class="btn btn-primary" :disabled="saving">{{ saving ? 'Сохранение…' : isEdit ? 'Сохранить изменения' : 'Создать продукт' }}</button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.layout { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 16px; align-items: start; }
.main, .side { display: flex; flex-direction: column; gap: 16px; }
.side { position: sticky; top: 16px; }
.card h2 { margin-bottom: 12px; }
.card h3 { margin-bottom: 8px; }
.sec-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
.sec-head h2 { margin: 0; }
.grid3 { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
.grid4 { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
.calc { height: 34px; display: flex; align-items: center; padding: 0 10px; border-radius: 8px; background: var(--good-bg); color: var(--good); font-weight: 650; }
.calc.neg { background: var(--bad-bg); color: var(--bad); }
.hint { margin: 10px 0 0; font-size: 12px; color: var(--plum-600); }
.line { display: flex; gap: 8px; margin-top: 8px; }
.w140 { width: 140px; }
.check { display: flex; align-items: center; gap: 8px; padding: 4px 0; cursor: pointer; }
.check input { accent-color: var(--plum-600); width: 16px; height: 16px; }
.tags { display: flex; flex-wrap: wrap; gap: 6px; padding: 6px; border: 1px solid var(--border-strong); border-radius: 8px; }
.tag { display: inline-flex; align-items: center; gap: 4px; background: var(--plum-100); color: var(--plum-700); border-radius: 12px; padding: 2px 4px 2px 10px; font-size: 12px; font-weight: 600; }
.tag button { border: 0; background: none; cursor: pointer; color: inherit; font-size: 15px; line-height: 1; padding: 0 4px; }
.tag-input { border: 0; outline: 0; font: inherit; flex: 1; min-width: 90px; padding: 4px; }
.savebar { grid-column: 1 / -1; position: sticky; bottom: 0; display: flex; justify-content: flex-end; gap: 8px; padding: 12px 0; background: linear-gradient(transparent, var(--bg) 30%); }
.small { font-size: 12px; margin: 6px 0 0; }
.warn { color: var(--warn); }
@media (max-width: 1100px) {
  .layout { grid-template-columns: minmax(0, 1fr); }
  .side { position: static; }
}
@media (max-width: 640px) {
  .grid3, .grid4 { grid-template-columns: 1fr 1fr; }
  .line { flex-wrap: wrap; }
}
</style>
