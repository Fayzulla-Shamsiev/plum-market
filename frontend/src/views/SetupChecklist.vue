<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, type Setup } from '../api'
import Icon from '../components/Icon.vue'

// A store is created empty at registration ("готовый шаблон"), so the panel shows what is left to do
// before customers can order — and disappears once the first order arrives.
const setup = ref<Setup | null>(null)
onMounted(() => api.setup().then(s => { setup.value = s }).catch(() => {}))

const steps = computed(() => {
  const s = setup.value
  if (!s) return []
  return [
    { done: s.categories > 0, to: '/products/categories/new', title: 'Создайте категории', text: 'Разделы каталога, по которым покупатель ищет товары.' },
    { done: s.products > 0, to: '/products/items/new', title: 'Добавьте товары', text: 'Название, цена, фото и остаток на складе.' },
    { done: s.branchReady, to: '/store', title: 'Заполните магазин и филиал', text: 'Адрес на карте, телефон, «О нас» и условия доставки.' },
  ]
})
const left = computed(() => steps.value.filter(s => !s.done).length)
// The checklist is guidance for a new store: once orders come in, the dashboard belongs to them.
const show = computed(() => !!setup.value && setup.value.orders === 0 && left.value > 0)
</script>

<template>
  <section v-if="show && setup" class="card card-pad setup">
    <div class="setup-head">
      <h2>Первые шаги</h2>
      <span class="card-sub">Осталось {{ left }} из {{ steps.length }} — потом магазин можно открывать покупателям</span>
      <a v-if="setup.slug" class="btn btn-sm" :href="`/shop/${setup.slug}`" target="_blank" rel="noopener">
        <Icon name="branches" />Посмотреть магазин
      </a>
    </div>
    <ol>
      <li v-for="s in steps" :key="s.title" :class="{ done: s.done }">
        <span class="tick"><Icon v-if="s.done" name="check" /></span>
        <div>
          <RouterLink :to="s.to">{{ s.title }}</RouterLink>
          <p class="faint">{{ s.text }}</p>
        </div>
      </li>
    </ol>
  </section>
</template>

<style scoped>
.setup { margin-bottom: 16px; }
.setup-head { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; }
.setup-head .btn { margin-left: auto; }
ol { list-style: none; display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 10px; margin: 0; padding: 0; }
li { display: flex; gap: 10px; padding: 11px 12px; border: 1px solid var(--border); border-radius: 10px; background: var(--surface-2); }
li.done { background: var(--good-bg); border-color: transparent; }
li p { margin: 2px 0 0; font-size: 12.5px; line-height: 1.4; }
li a { font-weight: 600; }
li.done a { color: var(--text-2); text-decoration: line-through; }
.tick { width: 20px; height: 20px; flex: none; margin-top: 1px; border-radius: 50%; border: 1.5px solid var(--border-strong); display: grid; place-items: center; color: var(--good); }
li.done .tick { border-color: var(--good); }
.tick svg { width: 13px; height: 13px; }
</style>
