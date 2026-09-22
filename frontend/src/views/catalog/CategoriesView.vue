<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { catalogApi, type CategoryRow } from '../../api'
import BranchSwitcher from '../../components/BranchSwitcher.vue'
import Icon from '../../components/Icon.vue'
import ProductThumb from '../../components/ProductThumb.vue'
import { date, loc } from '../../format'
import { catalogBranch } from '../../store'

const rows = ref<CategoryRow[] | null>(null)
const error = ref('')

async function load() {
  error.value = ''
  try {
    rows.value = await catalogApi.categories(catalogBranch.value)
  } catch (e) {
    error.value = (e as Error).message
  }
}
watch(catalogBranch, load, { immediate: true })

/** Parent → children order with depth, so the table reads as a tree. */
const tree = computed(() => {
  const all = rows.value ?? []
  const out: (CategoryRow & { depth: number })[] = []
  const walk = (parent: number | null, depth: number) => {
    for (const c of all.filter(x => x.parentId === parent)) {
      out.push({ ...c, depth })
      walk(c.id, depth + 1)
    }
  }
  walk(null, 0)
  return out
})

async function remove(c: CategoryRow) {
  if (!confirm(`Удалить категорию «${loc(c.name)}»?`)) return
  try {
    await catalogApi.deleteCategory(c.id)
    load()
  } catch (e) {
    alert((e as Error).message)
  }
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Категории</h1>
      <BranchSwitcher />
      <RouterLink to="/products/categories/new" class="btn btn-primary"><Icon name="plus" />Добавить категорию</RouterLink>
    </div>
    <div v-if="error" class="error-banner">{{ error }}</div>
    <div class="card">
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>Название</th><th class="right">Подкатегории</th><th class="right">Товары</th><th>Статус</th><th>Создана</th><th class="right">Действия</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in tree" :key="c.id">
              <td>
                <div class="name" :style="{ paddingLeft: c.depth * 28 + 'px' }">
                  <span v-if="c.depth" class="branch-line">└</span>
                  <ProductThumb :url="c.imageUrl" :name="loc(c.name)" :size="34" />
                  <div>
                    <RouterLink :to="`/products/categories/${c.id}`"><b>{{ loc(c.name) }}</b></RouterLink>
                    <div class="faint small">{{ c.name.uz }}</div>
                  </div>
                </div>
              </td>
              <td class="right num">{{ c.subcategoriesCount }}</td>
              <td class="right num">
                <RouterLink :to="{ path: '/products/items', query: { category: c.id } }">{{ c.productsCount }}</RouterLink>
              </td>
              <td><span class="badge" :class="c.isActive ? 'Completed' : 'Cancelled'">{{ c.isActive ? 'Активна' : 'Скрыта' }}</span></td>
              <td class="num">{{ date(c.createdAt) }}</td>
              <td class="right nowrap">
                <RouterLink :to="`/products/categories/${c.id}`" class="btn btn-sm btn-ghost btn-icon" aria-label="Редактировать" title="Редактировать">
                  <Icon name="edit" />
                </RouterLink>
                <button class="btn btn-sm btn-ghost btn-icon btn-danger" aria-label="Удалить" title="Удалить" @click="remove(c)">
                  <Icon name="trash" />
                </button>
              </td>
            </tr>
            <tr v-if="rows && !rows.length"><td colspan="6" class="empty">Категорий пока нет</td></tr>
            <tr v-if="!rows"><td colspan="6"><div class="skeleton" style="height: 240px" /></td></tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.name { display: flex; align-items: center; gap: 10px; }
.branch-line { color: var(--text-3); margin-right: -4px; }
.small { font-size: 12px; }
</style>
