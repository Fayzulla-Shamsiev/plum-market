<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { catalogApi, type CategoryRow, type Paged, type ProductRow } from '../../api'
import BranchSwitcher from '../../components/BranchSwitcher.vue'
import Icon from '../../components/Icon.vue'
import Pager from '../../components/Pager.vue'
import ProductThumb from '../../components/ProductThumb.vue'
import { date, loc, money, stockStatusLabel } from '../../format'
import { catalogBranch } from '../../store'
import ImportModal from './ImportModal.vue'

const route = useRoute()
const search = ref('')
const categoryId = ref<number | ''>(route.query.category ? Number(route.query.category) : '')
const active = ref<'' | 'true' | 'false'>('')
const page = ref(1)
const data = ref<Paged<ProductRow> | null>(null)
const categories = ref<CategoryRow[]>([])
const error = ref('')
const importOpen = ref(false)

async function load() {
  error.value = ''
  try {
    data.value = await catalogApi.products({
      search: search.value.trim(), categoryId: categoryId.value, branchId: catalogBranch.value, active: active.value, page: page.value,
    })
  } catch (e) {
    error.value = (e as Error).message
  }
}

let timer = 0
watch(search, () => { clearTimeout(timer); timer = window.setTimeout(() => { page.value = 1; load() }, 300) })
watch([categoryId, active, catalogBranch], () => { page.value = 1; load() })
watch(page, load)
onMounted(async () => {
  load()
  categories.value = await catalogApi.categories()
})

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

async function toggle(p: ProductRow) {
  const next = !p.isActive
  p.isActive = next
  try {
    await catalogApi.setProductActive(p.id, next)
  } catch {
    p.isActive = !next
  }
}

async function remove(p: ProductRow) {
  if (!confirm(`Удалить товар «${loc(p.name)}»? История заказов сохранится.`)) return
  await catalogApi.deleteProduct(p.id)
  load()
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Продукты</h1>
      <BranchSwitcher />
      <button class="btn" @click="importOpen = true"><Icon name="upload" />Импорт</button>
      <RouterLink to="/products/items/new" class="btn btn-primary"><Icon name="plus" />Добавить продукт</RouterLink>
    </div>

    <div class="card">
      <div class="toolbar">
        <label class="search">
          <Icon name="search" />
          <input v-model="search" class="input" placeholder="Название на любом языке или ID" aria-label="Поиск товаров" />
        </label>
        <select v-model="categoryId" class="select" aria-label="Категория">
          <option value="">Все категории</option>
          <option v-for="c in categoryOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
        </select>
        <select v-model="active" class="select" aria-label="Статус">
          <option value="">Любой статус</option>
          <option value="true">Активные</option>
          <option value="false">Неактивные</option>
        </select>
      </div>
      <div v-if="error" class="error-banner" style="margin: 12px">{{ error }}</div>
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>Название</th><th class="right">Цена</th><th>Рейтинг</th><th class="right">Обзоры</th>
              <th v-if="catalogBranch">Наличие</th><th>Создан</th><th>Активен</th><th class="right">Действия</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in data?.items" :key="p.id" :class="{ inactive: !p.isActive }">
              <td>
                <div class="prod">
                  <ProductThumb :url="p.imageUrl" :name="loc(p.name)" />
                  <div class="min0">
                    <RouterLink :to="`/products/items/${p.id}`" class="pname"><b>{{ loc(p.name) }}</b></RouterLink>
                    <div class="faint small">{{ loc(p.category) || 'Без категории' }}<span v-if="!catalogBranch"> · филиалов: {{ p.branches }}</span></div>
                  </div>
                </div>
              </td>
              <td class="right num nowrap">
                <b>{{ money(p.price) }}</b>
                <div v-if="p.oldPrice" class="old">{{ money(p.oldPrice) }}</div>
              </td>
              <td class="nowrap">
                <span v-if="p.rating !== null" class="rating"><Icon name="star" />{{ p.rating.toFixed(1) }}</span>
                <span v-else class="faint">—</span>
              </td>
              <td class="right num">{{ p.reviewsCount }}</td>
              <td v-if="catalogBranch">
                <span v-if="p.stock" class="stock" :class="p.stock.status">
                  {{ stockStatusLabel[p.stock.status] }}<template v-if="p.stock.status === 'Limited'">: {{ p.stock.quantity }}</template>
                </span>
              </td>
              <td class="num nowrap">{{ date(p.createdAt) }}</td>
              <td>
                <label class="switch" :title="p.isActive ? 'Деактивировать' : 'Активировать'">
                  <input type="checkbox" :checked="p.isActive" :aria-label="`Активен: ${loc(p.name)}`" @change="toggle(p)" /><span class="track" />
                </label>
              </td>
              <td class="right nowrap">
                <RouterLink :to="`/products/items/${p.id}`" class="btn btn-sm btn-ghost btn-icon" title="Редактировать" aria-label="Редактировать"><Icon name="edit" /></RouterLink>
                <button class="btn btn-sm btn-ghost btn-icon btn-danger" title="Удалить" aria-label="Удалить" @click="remove(p)"><Icon name="trash" /></button>
              </td>
            </tr>
            <tr v-if="data && !data.items.length"><td :colspan="catalogBranch ? 8 : 7" class="empty">Товаров не найдено</td></tr>
            <tr v-if="!data"><td :colspan="8"><div class="skeleton" style="height: 300px" /></td></tr>
          </tbody>
        </table>
      </div>
      <Pager v-if="data" v-model:page="page" :page-size="data.pageSize" :total="data.total" />
    </div>

    <ImportModal v-if="importOpen" @close="importOpen = false" @imported="load" />
  </div>
</template>

<style scoped>
.toolbar { display: flex; gap: 10px; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--border); flex-wrap: wrap; }
.prod { display: flex; align-items: center; gap: 10px; }
.min0 { min-width: 0; }
.pname { color: var(--text); }
.old { font-size: 12px; color: var(--text-3); text-decoration: line-through; }
.rating { display: inline-flex; align-items: center; gap: 4px; font-weight: 600; }
.rating svg { width: 15px; height: 15px; color: #eda100; fill: #eda100; }
.stock { font-size: 12px; font-weight: 600; padding: 2px 8px; border-radius: 10px; white-space: nowrap; }
.stock.Unlimited { background: var(--good-bg); color: var(--good); }
.stock.Limited { background: var(--warn-bg); color: var(--warn); }
.stock.OutOfStock { background: var(--bad-bg); color: var(--bad); }
tr.inactive td:not(:nth-last-child(-n+2)) { opacity: .5; }
.small { font-size: 12px; }
</style>
