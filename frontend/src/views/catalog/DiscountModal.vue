<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { catalogApi, type DiscountInput, type DiscountRow, type ProductRow } from '../../api'
import Modal from '../../components/Modal.vue'
import ProductThumb from '../../components/ProductThumb.vue'
import { loc, money } from '../../format'
import { useLookups } from '../../store'

const props = defineProps<{ discount: DiscountRow | null }>()
const emit = defineEmits<{ close: []; saved: [] }>()
const { lookups } = useLookups()

/** <input type="datetime-local"> wants local "YYYY-MM-DDTHH:mm". */
const toLocal = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
const weekLater = new Date(Date.now() + 7 * 86400000)
weekLater.setHours(23, 59, 0, 0)

const form = ref<DiscountInput>(props.discount
  ? {
      name: props.discount.name, type: props.discount.type, value: props.discount.value, productIds: [...props.discount.productIds],
      branchIds: [...props.discount.branchIds], startsAt: props.discount.startsAt.slice(0, 16), endsAt: props.discount.endsAt.slice(0, 16),
      minOrderAmount: props.discount.minOrderAmount, isActive: props.discount.isActive,
    }
  : { name: '', type: 'Percent', value: 10, productIds: [], branchIds: [], startsAt: toLocal(new Date()), endsAt: toLocal(weekLater), minOrderAmount: null, isActive: true })

const hasMin = ref(!!form.value.minOrderAmount)
const allBranches = ref(form.value.branchIds.length === 0)
const products = ref<ProductRow[]>([])
const search = ref('')
const saving = ref(false)
const error = ref('')

onMounted(async () => {
  products.value = (await catalogApi.products({ pageSize: 100 })).items
})

const visible = computed(() => {
  const t = search.value.trim().toLowerCase()
  return t ? products.value.filter(p => Object.values(p.name).some(v => v?.toLowerCase().includes(t))) : products.value
})
const selected = computed(() => new Set(form.value.productIds))
function toggle(id: number) {
  form.value.productIds = selected.value.has(id) ? form.value.productIds.filter(x => x !== id) : [...form.value.productIds, id]
}
function toggleBranch(id: number) {
  const s = new Set(form.value.branchIds)
  s.has(id) ? s.delete(id) : s.add(id)
  form.value.branchIds = [...s]
}

/** Preview on the first selected product so the merchant sees the effect. */
const preview = computed(() => {
  const p = products.value.find(x => selected.value.has(x.id))
  if (!p || !form.value.value) return null
  const after = form.value.type === 'Percent' ? Math.round(p.price * (1 - form.value.value / 100)) : p.price - form.value.value
  return { name: loc(p.name), before: p.price, after }
})

async function save() {
  saving.value = true
  error.value = ''
  try {
    await catalogApi.saveDiscount(props.discount?.id ?? null, {
      ...form.value,
      branchIds: allBranches.value ? [] : form.value.branchIds,
      minOrderAmount: hasMin.value ? form.value.minOrderAmount : null,
    })
    emit('saved')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Modal :title="discount ? 'Редактировать скидку' : 'Новая скидка'" width="720px" persistent @close="emit('close')">
    <div class="grid">
      <div class="col">
        <label class="field"><span>Название *</span><input v-model="form.name" class="input" placeholder="Например: Утренний кофе" /></label>
        <div class="field">
          <span>Тип скидки</span>
          <div class="seg">
            <button type="button" :class="{ on: form.type === 'Percent' }" @click="form.type = 'Percent'">В процентах</button>
            <button type="button" :class="{ on: form.type === 'Fixed' }" @click="form.type = 'Fixed'">Фиксированная сумма</button>
          </div>
        </div>
        <label class="field">
          <span>Размер скидки</span>
          <div class="suffix"><input v-model.number="form.value" type="number" min="1" :max="form.type === 'Percent' ? 100 : undefined" class="input" /><span>{{ form.type === 'Percent' ? '%' : 'сум' }}</span></div>
        </label>
        <div class="row2">
          <label class="field"><span>Начало</span><input v-model="form.startsAt" type="datetime-local" class="input" /></label>
          <label class="field"><span>Окончание *</span><input v-model="form.endsAt" type="datetime-local" class="input" :min="form.startsAt" /></label>
        </div>
        <label class="switch"><input v-model="hasMin" type="checkbox" /><span class="track" />Минимальная сумма заказа</label>
        <div v-if="hasMin" class="suffix"><input v-model.number="form.minOrderAmount" type="number" min="0" class="input" /><span>сум</span></div>

        <div class="field">
          <span>Филиалы</span>
          <label class="check"><input v-model="allBranches" type="checkbox" />Все филиалы</label>
          <template v-if="!allBranches">
            <label v-for="b in lookups?.branches" :key="b.id" class="check">
              <input type="checkbox" :checked="form.branchIds.includes(b.id)" @change="toggleBranch(b.id)" />{{ b.name }}
            </label>
          </template>
        </div>
        <label class="switch"><input v-model="form.isActive" type="checkbox" /><span class="track" />Скидка включена</label>
        <div v-if="preview" class="preview">
          «{{ preview.name }}»: <s>{{ money(preview.before) }}</s> → <b :class="{ bad: preview.after <= 0 }">{{ money(preview.after) }}</b>
        </div>
      </div>

      <div class="col">
        <div class="field">
          <span>Товары * <em class="faint">выбрано: {{ form.productIds.length }}</em></span>
          <input v-model="search" class="input" placeholder="Найти товар" aria-label="Найти товар" />
        </div>
        <ul class="plist">
          <li v-for="p in visible" :key="p.id">
            <label class="pick" :class="{ on: selected.has(p.id) }">
              <input type="checkbox" :checked="selected.has(p.id)" @change="toggle(p.id)" />
              <ProductThumb :url="p.imageUrl" :name="loc(p.name)" :size="28" />
              <span class="grow">{{ loc(p.name) }}</span>
              <span class="num faint small">{{ money(p.price) }}</span>
            </label>
          </li>
        </ul>
      </div>
    </div>
    <div v-if="error" class="error-banner" style="margin-top: 12px">{{ error }}</div>
    <template #footer>
      <button class="btn" @click="emit('close')">Отмена</button>
      <button class="btn btn-primary" :disabled="saving" @click="save">Сохранить</button>
    </template>
  </Modal>
</template>

<style scoped>
.grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
.col { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
.seg { display: grid; grid-template-columns: 1fr 1fr; background: var(--bg); padding: 3px; border-radius: 9px; }
.seg button { border: 0; background: none; font: inherit; font-size: 13px; font-weight: 550; padding: 6px; border-radius: 7px; cursor: pointer; color: var(--text-2); }
.seg button.on { background: var(--surface); color: var(--text); box-shadow: var(--shadow); }
.suffix { display: flex; align-items: center; gap: 8px; }
.suffix .input { flex: 1; }
.row2 { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.row2 .input { font-size: 13px; padding: 0 6px; }
.check { display: flex; gap: 8px; align-items: center; font-weight: 400; color: var(--text); cursor: pointer; font-size: 14px; }
.check input, .pick input { accent-color: var(--plum-600); width: 16px; height: 16px; }
.plist { list-style: none; margin: -6px 0 0; padding: 4px; border: 1px solid var(--border); border-radius: 10px; max-height: 380px; overflow-y: auto; }
.pick { display: flex; align-items: center; gap: 8px; padding: 6px 8px; border-radius: 8px; cursor: pointer; }
.pick:hover { background: var(--surface-2); }
.pick.on { background: var(--plum-50); }
.preview { font-size: 13px; padding: 8px 10px; background: var(--good-bg); border-radius: 8px; }
.bad { color: var(--bad); }
.small { font-size: 12px; }
em { font-style: normal; font-weight: 400; }
@media (max-width: 700px) { .grid { grid-template-columns: 1fr; } }
</style>
