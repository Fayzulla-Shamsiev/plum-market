<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, type Availability } from '../../api'
import Icon from '../../components/Icon.vue'
import { money } from '../../format'
import PhotoSlot from './PhotoSlot.vue'

// Under «Добавлено товаров»: in which branches each new product is sold, and how many are there. The same stock
// rows Каталог → Склад shows — a ticked branch with an empty box is «Безлимитный», a number makes it «Ограничено».
const props = defineProps<{ title: string; productIds: number[]; link: string | null; linkLabel: string | null }>()

interface Cell { on: boolean; qty: string }
const data = ref<Availability | null>(null)
const cells = ref<Record<number, Record<number, Cell>>>({})
const error = ref('')
const saving = ref(false)
const saved = ref(false)
const dirty = ref(false)

function fill(a: Availability) {
  data.value = a
  cells.value = Object.fromEntries(a.products.map(p => [p.id, Object.fromEntries(a.branches.map(b => {
    const s = p.stock.find(x => x.branchId === b.id)
    return [b.id, { on: !!s, qty: s && s.status !== 'Unlimited' ? String(s.quantity) : '' }]
  }))]))
  dirty.value = false
}

onMounted(async () => {
  try {
    fill(await api.availability(props.productIds))
  } catch (e) {
    error.value = (e as Error).message
  }
})

const noPhoto = computed(() => data.value?.products.filter(p => !p.imageUrl).length ?? 0)
const many = computed(() => (data.value?.branches.length ?? 0) > 1)
const nowhere = computed(() => data.value?.products.filter(p => !Object.values(cells.value[p.id] ?? {}).some(c => c.on)) ?? [])

function touch() {
  dirty.value = true
  saved.value = false
}
/** «Везде»: ticks one branch for every product at once. */
function column(branchId: number, on: boolean) {
  for (const p of data.value?.products ?? []) cells.value[p.id][branchId].on = on
  touch()
}
const columnOn = (branchId: number) => data.value?.products.every(p => cells.value[p.id]?.[branchId]?.on) ?? false

async function save() {
  if (!data.value || saving.value) return
  error.value = ''
  const items = data.value.products.map(p => ({
    productId: p.id,
    branches: Object.entries(cells.value[p.id]).filter(([, c]) => c.on).map(([id, c]) => ({
      branchId: Number(id),
      quantity: c.qty.trim() === '' ? null : Number(c.qty),
    })),
  }))
  if (items.some(i => i.branches.some(b => b.quantity !== null && (!Number.isInteger(b.quantity) || b.quantity < 0)))) {
    error.value = 'Остаток — целое число от 0, или пусто для «без ограничений».'
    return
  }
  saving.value = true
  try {
    fill(await api.saveAvailability(items))
    saved.value = true
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="av">
    <div class="av-title"><span class="ok"><Icon name="check" /></span>{{ title }}</div>
    <p class="av-hint">
      {{ many ? 'Отметьте, в каких филиалах есть каждый товар, и сколько штук там осталось.' : 'Укажите, сколько штук есть в наличии.' }}
      Пустое поле — без ограничений.
    </p>

    <p v-if="!data && !error" class="faint">Загружаем…</p>
    <div v-else-if="data" class="scroll">
      <table>
        <thead>
          <tr>
            <th class="name">Товар</th>
            <th v-for="b in data.branches" :key="b.id">
              <label v-if="many" class="head-check" :title="`Отметить «${b.name}» для всех`">
                <input type="checkbox" :checked="columnOn(b.id)" @change="column(b.id, ($event.target as HTMLInputElement).checked)" />
                <span>{{ b.name }}</span>
              </label>
              <span v-else>{{ b.name }}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in data.products" :key="p.id" :class="{ warn: nowhere.includes(p) }">
            <td class="name">
              <div class="prod">
                <PhotoSlot kind="product" :id="p.id" :url="p.imageUrl" :label="p.name" @changed="u => (p.imageUrl = u)" />
                <div class="prod-text">
                  <RouterLink :to="`/products/items/${p.id}`">{{ p.name }}</RouterLink>
                  <small>{{ money(p.price) }} / {{ p.unit }}</small>
                </div>
              </div>
            </td>
            <td v-for="b in data.branches" :key="b.id">
              <div class="cell" :class="{ off: !cells[p.id][b.id].on }">
                <input v-if="many" v-model="cells[p.id][b.id].on" type="checkbox" :aria-label="`${p.name} в «${b.name}»`" @change="touch" />
                <input v-model="cells[p.id][b.id].qty" class="qty" inputmode="numeric" placeholder="∞" :disabled="!cells[p.id][b.id].on"
                  :aria-label="`Остаток «${p.name}» в «${b.name}»`" @input="touch" />
                <span class="unit">шт</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-if="noPhoto" class="photo-hint">
      Нажмите на квадрат слева, чтобы добавить фото — товары с фото покупают заметно чаще. Можно и позже.
    </p>
    <p v-if="nowhere.length" class="warn-text">Без филиала товар не продаётся: {{ nowhere.map(p => p.name).join(', ') }}.</p>
    <p v-if="error" class="err">{{ error }}</p>
    <div class="av-foot">
      <button class="save" :disabled="!dirty || saving" @click="save">{{ saving ? 'Сохраняем…' : saved ? 'Сохранено ✓' : 'Сохранить наличие' }}</button>
      <RouterLink v-if="link" :to="link" class="link">{{ linkLabel ?? 'Открыть' }} →</RouterLink>
    </div>
  </div>
</template>

<style scoped>
.av { display: flex; flex-direction: column; gap: 8px; }
.av-title { display: flex; align-items: center; gap: 8px; font-weight: 650; }
.ok { width: 20px; height: 20px; flex: none; border-radius: 50%; display: grid; place-items: center; background: #e3f8ec; color: #139a55; }
.ok svg { width: 13px; height: 13px; }
.av-hint { margin: 0; color: var(--ink-2); font-size: 13px; }
.faint { color: var(--ink-3); margin: 0; font-size: 13px; }
.scroll { overflow-x: auto; margin: 0 -4px; }
table { border-collapse: collapse; width: 100%; font-size: 13.5px; }
th { font-size: 12px; font-weight: 600; color: var(--ink-3); text-align: left; padding: 6px 4px; white-space: nowrap; border-bottom: 1px solid var(--line); }
td { padding: 6px 4px; border-bottom: 1px solid #f0f3f8; vertical-align: middle; }
tr:last-child td { border-bottom: 0; }
.name { min-width: 200px; }
.prod { display: flex; align-items: center; gap: 10px; }
.prod-text { display: flex; flex-direction: column; min-width: 0; }
td.name a { color: var(--ink); font-weight: 600; }
td.name a:hover { color: var(--blue); }
td.name small { color: var(--ink-3); font-size: 12px; }
tr.warn td.name a { color: #b42318; }
.head-check { display: inline-flex; align-items: center; gap: 6px; cursor: pointer; }
.cell { display: inline-flex; align-items: center; gap: 6px; }
.cell.off .qty { background: #f4f6fa; }
input[type='checkbox'] { width: 16px; height: 16px; accent-color: var(--blue); cursor: pointer; margin: 0; }
.qty {
  width: 64px; height: 32px; border: 1.5px solid var(--line); border-radius: 8px; padding: 0 8px; font: inherit; font-size: 13.5px;
  text-align: right; outline: none; color: var(--ink); background: #fff; font-variant-numeric: tabular-nums;
}
.qty:focus { border-color: var(--blue); }
.qty:disabled { color: var(--ink-3); cursor: not-allowed; }
.qty::placeholder { color: var(--ink-3); }
.unit { font-size: 12px; color: var(--ink-3); }
.photo-hint { margin: 0; font-size: 12.5px; color: var(--ink-3); }
.warn-text { margin: 0; font-size: 13px; color: #b42318; }
.err { margin: 0; font-size: 13px; color: #b42318; }
.av-foot { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
.save { height: 36px; padding: 0 16px; border: 0; border-radius: 10px; background: var(--blue); color: #fff; font: inherit; font-size: 14px; font-weight: 650; cursor: pointer; }
.save:hover:not(:disabled) { background: var(--blue-600); }
.save:disabled { opacity: .55; cursor: default; }
.link { font-size: 13.5px; font-weight: 600; color: var(--blue); }
.link:hover { text-decoration: underline; }
</style>
