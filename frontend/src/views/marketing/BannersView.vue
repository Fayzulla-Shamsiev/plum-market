<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { marketingApi, type BannerRow } from '../../api'
import Icon from '../../components/Icon.vue'
import { date } from '../../format'

const rows = ref<BannerRow[] | null>(null)
async function load() { rows.value = await marketingApi.banners() }
onMounted(load)

const groups = computed(() => [
  { type: 'Main', title: 'Основные баннеры', hint: 'Слайдер на главной странице магазина — в этом порядке', items: rows.value?.filter(b => b.type === 'Main') ?? [] },
  { type: 'Category', title: 'Баннеры категорий', hint: 'Показываются над товарами выбранной категории', items: rows.value?.filter(b => b.type === 'Category') ?? [] },
])

async function toggle(b: BannerRow) {
  b.isActive = !b.isActive
  await marketingApi.setBannerActive(b.id, b.isActive)
}
async function move(b: BannerRow, dir: number) {
  await marketingApi.moveBanner(b.id, dir)
  load()
}
async function remove(b: BannerRow) {
  if (!confirm(`Удалить баннер «${b.title}»?`)) return
  await marketingApi.deleteBanner(b.id)
  load()
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Баннеры</h1>
      <RouterLink to="/marketing/banners/new" class="btn btn-primary"><Icon name="plus" />Добавить баннер</RouterLink>
    </div>
    <div v-if="!rows" class="skeleton" style="height: 300px" />
    <section v-for="g in groups" v-else :key="g.type" class="group">
      <h2>{{ g.title }} <span class="faint small">· {{ g.hint }}</span></h2>
      <div class="card">
        <ul class="list">
          <li v-for="(b, i) in g.items" :key="b.id" :class="{ off: !b.isActive }">
            <div class="order">
              <button class="btn btn-ghost btn-icon btn-sm" :disabled="i === 0" aria-label="Выше" @click="move(b, -1)"><Icon name="arrowUp" /></button>
              <button class="btn btn-ghost btn-icon btn-sm" :disabled="i === g.items.length - 1" aria-label="Ниже" @click="move(b, 1)"><Icon name="arrowDown" /></button>
            </div>
            <div class="thumbs">
              <figure><video v-if="b.desktopMediaType === 'video' && b.desktopUrl" :src="b.desktopUrl" muted /><img v-else-if="b.desktopUrl" :src="b.desktopUrl" alt="" /><span v-else>—</span><figcaption>ПК</figcaption></figure>
              <figure class="m"><video v-if="b.mobileMediaType === 'video' && b.mobileUrl" :src="b.mobileUrl" muted /><img v-else-if="b.mobileUrl" :src="b.mobileUrl" alt="" /><span v-else>—</span><figcaption>Моб.</figcaption></figure>
            </div>
            <div class="info grow">
              <RouterLink :to="`/marketing/banners/${b.id}`"><b>{{ b.title }}</b></RouterLink>
              <div class="small muted">
                <template v-if="b.category">Категория: {{ b.category }} · </template>
                Переход: {{ b.linkLabel ?? 'без ссылки' }}
              </div>
              <div class="faint small">Создан {{ date(b.createdAt) }}</div>
            </div>
            <label class="switch" :title="b.isActive ? 'Скрыть' : 'Показать'">
              <input type="checkbox" :checked="b.isActive" :aria-label="`Показывать: ${b.title}`" @change="toggle(b)" /><span class="track" />
            </label>
            <RouterLink :to="`/marketing/banners/${b.id}`" class="btn btn-sm btn-ghost btn-icon" aria-label="Редактировать"><Icon name="edit" /></RouterLink>
            <button class="btn btn-sm btn-ghost btn-icon btn-danger" aria-label="Удалить" @click="remove(b)"><Icon name="trash" /></button>
          </li>
          <li v-if="!g.items.length" class="empty">Баннеров нет</li>
        </ul>
      </div>
    </section>
  </div>
</template>

<style scoped>
.group { margin-bottom: 20px; }
.group h2 { margin-bottom: 10px; }
.list { list-style: none; margin: 0; padding: 0; }
.list li { display: flex; align-items: center; gap: 14px; padding: 12px 14px; border-bottom: 1px solid var(--border); }
.list li:last-child { border-bottom: 0; }
.list li.off .thumbs, .list li.off .info { opacity: .45; }
.order { display: flex; flex-direction: column; }
.thumbs { display: flex; gap: 8px; }
figure { margin: 0; position: relative; width: 180px; aspect-ratio: 3 / 1; border-radius: 8px; overflow: hidden; background: var(--surface-2); border: 1px solid var(--border); display: grid; place-items: center; color: var(--text-3); }
figure.m { width: 100px; aspect-ratio: 2 / 1; }
figure img, figure video { width: 100%; height: 100%; object-fit: cover; }
figcaption { position: absolute; left: 4px; bottom: 4px; font-size: 10px; background: rgb(0 0 0 / 55%); color: #fff; padding: 1px 5px; border-radius: 4px; }
.small { font-size: 12px; font-weight: 400; }
@media (max-width: 800px) { figure { width: 120px; } .thumbs figure.m { display: none; } }
</style>
