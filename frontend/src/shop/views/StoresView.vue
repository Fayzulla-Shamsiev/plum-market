<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { openStore, storeSlug } from '../state/store'

// Every administrator who registers gets their own storefront. On one installation they all share a host, so
// this page is how a shopper picks which shop to open; in production each one would live on its own address.
interface StoreCard { slug: string; name: string; products: number; about: string | null }

const route = useRoute()
const router = useRouter()
const stores = ref<StoreCard[] | null>(null)
const failed = ref(false)

const next = () => (typeof route.query.next === 'string' && route.query.next.startsWith('/') ? route.query.next : '/')

function open(slug: string) {
  openStore(slug)
  router.replace(next())
}

onMounted(async () => {
  try {
    const list = await fetch('/api/stores').then(r => r.json()) as StoreCard[]
    // A single shop needs no choice — it is the shop.
    if (list.length === 1) return open(list[0].slug)
    stores.value = list
  } catch {
    failed.value = true
  }
})

const plural = (n: number) => (n % 10 === 1 && n % 100 !== 11 ? 'товар' : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20) ? 'товара' : 'товаров')
</script>

<template>
  <div class="pick">
    <header>
      <div class="mark">
        <svg viewBox="0 0 40 40" width="34" height="34" aria-hidden="true">
          <rect width="40" height="40" rx="12" fill="#1f7aec" />
          <path d="M14 29V12.5h7.2a6.2 6.2 0 0 1 0 12.4H14" fill="none" stroke="#fff" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round" />
          <circle cx="28.5" cy="28.5" r="3.2" fill="#35d07f" />
        </svg>
        <span>Plum <b>Market</b></span>
      </div>
      <RouterLink to="/login" class="alt">Кабинет продавца</RouterLink>
    </header>

    <main>
      <h1>Магазины на платформе</h1>
      <p class="lead">Выберите магазин, чтобы открыть его каталог, корзину и заказы.</p>

      <p v-if="failed" class="none">Не удалось загрузить список магазинов. Обновите страницу.</p>
      <p v-else-if="!stores" class="none">Загружаем…</p>
      <div v-else-if="stores.length === 0" class="none">
        <b>Пока ни одного магазина нет.</b>
        <span>Магазин появляется, как только предприниматель регистрируется в панели управления.</span>
        <RouterLink to="/register" class="go">Создать первый магазин</RouterLink>
      </div>

      <ul v-else class="shops">
        <li v-for="s in stores" :key="s.slug">
          <button type="button" :class="{ current: s.slug === storeSlug }" @click="open(s.slug)">
            <span class="avatar">{{ s.name.trim().charAt(0).toUpperCase() }}</span>
            <span class="body">
              <b>{{ s.name }}</b>
              <small>{{ s.about ?? 'Магазин ещё заполняется.' }}</small>
              <i>{{ s.products }} {{ plural(s.products) }} · /{{ s.slug }}</i>
            </span>
            <svg class="chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="m9 6 6 6-6 6" /></svg>
          </button>
        </li>
      </ul>
    </main>
  </div>
</template>

<style scoped>
.pick {
  --blue: #1f7aec;
  --ink: #152033;
  --ink-2: #4b5567;
  --ink-3: #8a93a3;
  --line: #e2e8f1;
  min-height: 100vh; color: var(--ink); font-size: 15px;
  background: radial-gradient(900px 420px at 50% -140px, #dbe9fd 0%, rgb(219 233 253 / 0%) 70%), #f7f9fc;
}
.pick a { text-decoration: none; }
header { display: flex; align-items: center; gap: 16px; padding: 22px 24px; max-width: 720px; margin: 0 auto; }
.mark { display: inline-flex; align-items: center; gap: 10px; font-size: 17px; font-weight: 600; }
.mark b { font-weight: 800; color: var(--blue); }
.alt { margin-left: auto; height: 38px; display: inline-flex; align-items: center; padding: 0 16px; border-radius: 999px; background: #fff; border: 1px solid var(--line); color: var(--ink-2); font-weight: 600; font-size: 14px; }
.alt:hover { color: var(--blue); border-color: #dbe9fd; }

main { max-width: 720px; margin: 0 auto; padding: 14px 20px 60px; }
h1 { font-size: 30px; letter-spacing: -0.02em; margin: 0; }
.lead { color: var(--ink-2); margin: 8px 0 24px; }

.shops { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: minmax(0, 1fr); gap: 10px; }
.shops button {
  width: 100%; display: flex; align-items: center; gap: 14px; text-align: left; cursor: pointer;
  background: #fff; border: 1px solid var(--line); border-radius: 16px; padding: 14px 16px; font: inherit; color: inherit;
  transition: border-color .15s, box-shadow .15s, transform .15s;
}
.shops button:hover { border-color: #bcd7fb; box-shadow: 0 10px 28px rgb(21 32 51 / 8%); transform: translateY(-1px); }
.shops button.current { border-color: var(--blue); }
.avatar { width: 46px; height: 46px; flex: none; border-radius: 14px; background: #eef5ff; color: var(--blue); display: grid; place-items: center; font-size: 20px; font-weight: 700; }
.body { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.body b { font-size: 16px; font-weight: 650; }
.body small { color: var(--ink-2); font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.body i { font-style: normal; color: var(--ink-3); font-size: 12.5px; }
.chev { width: 20px; height: 20px; margin-left: auto; flex: none; color: var(--ink-3); }

.none { display: flex; flex-direction: column; align-items: flex-start; gap: 6px; background: #fff; border: 1px solid var(--line); border-radius: 16px; padding: 22px; color: var(--ink-2); }
.none b { color: var(--ink); font-size: 16px; }
.go { margin-top: 10px; height: 44px; display: inline-flex; align-items: center; padding: 0 20px; border-radius: 12px; background: var(--blue); color: #fff; font-weight: 600; }
</style>
