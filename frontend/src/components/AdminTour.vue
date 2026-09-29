<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { admin } from '../auth'
import { endTour, tourActive, tourSidebar } from '../tour'
import Icon from './Icon.vue'

// Guided tour of the admin panel: the page dims, one feature is spotlit, and a box explains what it does and what it
// keeps. Steps either point at a menu item (the page opens behind it) or at a part of that page.

interface Step {
  /** Page to open for this step. */
  route?: string
  /** CSS selectors tried in order; none (or none found) = the box sits in the middle of the screen. */
  target?: string[]
  /** Menu steps need the sidebar, which is a drawer on a phone. */
  menu?: boolean
  title: string
  text: string
}

const store = computed(() => admin.value?.store.name ?? 'вашего магазина')
const nav = (path: string) => [`[data-tour="nav-${path}"]`]

const steps = computed<Step[]>(() => [
  {
    route: '/dashboard',
    title: 'Добро пожаловать в панель управления',
    text: `За пару минут покажем, где что находится в магазине «${store.value}»: заказы, товары, клиенты, маркетинг и настройки. ` +
      'Листайте кнопкой «Далее» или стрелками на клавиатуре. Тур можно пропустить и пройти позже — кнопка «?» внизу меню слева.',
  },
  {
    route: '/dashboard', target: nav('/dashboard'), menu: true, title: 'Дашборд',
    text: 'Главная страница — сводка по магазину. Здесь собрана статистика за выбранный период: выручка, себестоимость, прибыль, ' +
      'заказы, клиенты и полученные деньги.',
  },
  {
    route: '/dashboard', target: ['main .page-head'], title: 'Период и филиал',
    text: 'Выберите период — сегодня, неделя, месяц, квартал, год или свои даты — и филиал. Все цифры и графики на странице пересчитаются.',
  },
  {
    route: '/dashboard', target: ['main .grid-kpi'], title: 'Ключевые показатели',
    text: 'Выручка и прибыль считаются по завершённым заказам. Ниже — графики доходов, заказы на карте, популярные товары и лучшие клиенты.',
  },
  {
    route: '/orders', target: nav('/orders'), menu: true, title: 'Заказы',
    text: 'Все заказы с сайта и из Telegram. У каждого хранятся товары, клиент, адрес или филиал самовывоза, сумма, промокод и история статусов. ' +
      'Число рядом с пунктом меню — новые заказы, которые ждут вас.',
  },
  {
    route: '/orders', target: ['main .board', 'main .card:not(.toolbar-card)'], title: 'Доска заказов',
    text: 'Заказы разложены по статусам: Новый → В сборке → Готов → Передан в доставку → В пути → Доставлен → Завершён. ' +
      'Откройте заказ, чтобы перевести его на следующий шаг, — покупатель сразу получит уведомление. Просроченные заказы подсвечиваются.',
  },
  {
    route: '/orders', target: ['main .page-head'], title: 'Список, сообщения, сборка, экспорт',
    text: '«Доска» и «Список» — два вида одних заказов. «Сообщения покупателю» — тексты уведомлений для каждого статуса, ' +
      '«Лист сборки» — что собрать по филиалу, «Экспорт» — выгрузка в Excel. Ниже — поиск по номеру, имени или телефону и фильтры.',
  },
  {
    route: '/customers', target: nav('/customers'), menu: true, title: 'Клиенты',
    text: 'База покупателей: имя, телефон, число заказов, сумма покупок, бонусные баллы и платформа, с которой пришёл клиент. ' +
      'Отсюда можно открыть карточку клиента или написать ему в чат.',
  },
  {
    route: '/customers', target: ['main .page-head .btn'], title: 'Бонусные баллы',
    text: 'Включите бонусную программу: покупатель получает баллы за покупки и оплачивает ими следующие заказы (1 балл = 1 сум). ' +
      'Здесь задаётся, за какую сумму начисляется балл.',
  },
  {
    route: '/chat', target: nav('/chat'), menu: true, title: 'Чат',
    text: 'Переписка с покупателями с сайта и из Telegram в одном окне. Хранится вся история диалогов и вложения; можно включить ' +
      'автоответ и сразу видеть, о каком заказе спрашивает клиент.',
  },
  {
    route: '/products/categories', target: nav('/products/categories'), menu: true, title: 'Каталог → Категории',
    text: 'Разделы витрины, по которым покупатель ищет товары. У категории есть название на русском и узбекском, картинка, баннер ' +
      'и порядок товаров. Категории можно вкладывать друг в друга.',
  },
  {
    route: '/products/items', target: nav('/products/items'), menu: true, title: 'Каталог → Товары',
    text: 'Карточки товаров: названия и описания на трёх языках, фото и видео, цена, старая цена, себестоимость, варианты, ' +
      'характеристики, вес и размеры, филиалы, где товар продаётся.',
  },
  {
    route: '/products/items', target: ['main .page-head'], title: 'Добавление и импорт',
    text: '«Добавить продукт» открывает карточку, где ИИ-кнопки переведут текст и напишут описание. Много товаров сразу — через ' +
      'импорт из Excel. Переключатель «Филиал» показывает ассортимент одной точки.',
  },
  {
    route: '/products/discounts', target: nav('/products/discounts'), menu: true, title: 'Каталог → Скидки',
    text: 'Акции на выбранные товары: скидка в процентах или фиксированной суммой, срок действия, минимальная сумма заказа и филиалы, ' +
      'где действует акция. Покупатель видит зачёркнутую цену.',
  },
  {
    route: '/products/stock', target: nav('/products/stock'), menu: true, title: 'Каталог → Склад',
    text: 'Остатки по каждому филиалу. Для товара хранится статус — безлимитный, ограничено или нет в наличии, — количество, ' +
      'цена закупки и продажи, маржа и скорость продаж.',
  },
  {
    route: '/products/stock', target: ['main .card'], title: 'Таблица остатков',
    text: 'Значения меняются прямо в таблице и сохраняются сразу. Когда остаток заканчивается, товар в этом филиале становится недоступен ' +
      'для заказа. Нажмите на товар — откроется история его продаж по месяцам.',
  },
  {
    route: '/marketing/promocodes', target: nav('/marketing/promocodes'), menu: true, title: 'Маркетинг → Промокоды',
    text: 'Коды, которые покупатель вводит при оформлении заказа: скидка в процентах или суммой, лимит использований, минимальный заказ, ' +
      'срок, «только на первый заказ». Хранится, сколько раз код уже использовали.',
  },
  {
    route: '/marketing/banners', target: nav('/marketing/banners'), menu: true, title: 'Маркетинг → Баннеры',
    text: 'Рекламные картинки на главной странице магазина и в категориях — отдельно для телефона и компьютера — с переходом в категорию, ' +
      'на товар или по ссылке.',
  },
  {
    route: '/marketing/reviews', target: nav('/marketing/reviews'), menu: true, title: 'Маркетинг → Отзывы',
    text: 'Оценки и отзывы покупателей о товарах. Отвечайте на них — ответ магазина увидят все на странице товара.',
  },
  {
    route: '/platforms/website', target: nav('/platforms/website'), menu: true, title: 'Платформы → Веб-сайт',
    text: 'Ваш магазин в интернете. Здесь хранятся название магазина, текст «О нас» и условия возврата — их покупатель открывает из профиля.',
  },
  {
    route: '/platforms/telegram', target: nav('/platforms/telegram'), menu: true, title: 'Платформы → Telegram-бот',
    text: 'Подключите своего бота из @BotFather — магазин откроется прямо в Telegram кнопкой «Open Shop». Здесь же тексты бота и ' +
      'автоответчик: бот сам пишет покупателю, когда меняется статус заказа.',
  },
  {
    route: '/store', target: nav('/store'), menu: true, title: 'Магазин',
    text: 'Контакты, стоимость и условия доставки, лимит времени на заказ и филиалы с адресами на карте. Филиал — это точка самовывоза ' +
      'и склад, у которого свои остатки.',
  },
  {
    route: '/store', target: ['[data-tour="assistant"]'], menu: true, title: 'ИИ-помощник',
    text: 'Помощник, который знает ваш магазин: добавит товары из текста или фото, поправит описания, настроит доставку и подскажет, ' +
      'как получить больше заказов.',
  },
  {
    route: '/store', target: ['[data-tour="shop"]'], menu: true, title: 'Открыть магазин',
    text: 'Так ваш магазин видят покупатели. Всё, что вы меняете в панели, появляется на сайте сразу.',
  },
  {
    route: '/store', target: ['[data-tour="account"]'], menu: true, title: 'Аккаунт и обучение',
    text: 'Ваше имя и телефон, кнопка «?», чтобы пройти этот тур ещё раз, и выход из панели.',
  },
  {
    route: '/dashboard',
    title: 'Готово!',
    text: 'Теперь вы знаете, где что находится. Начните с товаров и первого заказа, а если что-то непонятно — спросите ИИ-помощника.',
  },
])

const router = useRouter()
const index = ref(0)
const step = computed(() => steps.value[index.value])
const last = computed(() => index.value === steps.value.length - 1)
const target = ref<Element | null>(null)
const rect = ref<{ top: number; left: number; width: number; height: number } | null>(null)
const boxEl = ref<HTMLElement>()
const box = ref({ top: 0, left: 0, width: 360 })
const busy = ref(false)
const mobile = () => window.innerWidth <= 960

/** Waits for a page's element to appear: pages load their data after they open. */
async function find(selectors: string[] | undefined): Promise<Element | null> {
  if (!selectors?.length) return null
  for (let i = 0; i < 40; i++) {
    for (const s of selectors) {
      const el = document.querySelector(s)
      if (el && el.getBoundingClientRect().width > 0) return el
    }
    await new Promise(r => setTimeout(r, 75))
  }
  return null
}

async function show(i: number) {
  busy.value = true
  index.value = i
  const s = steps.value[i]
  target.value = null
  rect.value = null
  if (s.route && router.currentRoute.value.path !== s.route) await router.push(s.route)
  await nextTick()
  tourSidebar.value = !!s.menu && mobile()
  if (tourSidebar.value) await new Promise(r => setTimeout(r, 230)) // the drawer slides in
  const el = await find(s.target)
  if (index.value !== i) return
  if (el && !s.menu) el.scrollIntoView({ block: 'center', behavior: 'instant' as ScrollBehavior })
  target.value = el
  busy.value = false
  place()
}

const next = () => (last.value ? endTour() : !busy.value && show(index.value + 1))
const back = () => index.value > 0 && !busy.value && show(index.value - 1)

/** Keeps the spotlight on its element while the page scrolls, loads or the sidebar slides. */
let frame = 0
function place() {
  cancelAnimationFrame(frame)
  const vw = window.innerWidth
  const vh = window.innerHeight
  const width = Math.min(360, vw - 24)
  const height = boxEl.value?.offsetHeight ?? 200
  const el = target.value
  if (el) {
    const r = el.getBoundingClientRect()
    const pad = 6
    // A tall element (a whole table) is only lit where it is on screen.
    const top = Math.max(r.top - pad, 8)
    const bottom = Math.min(r.bottom + pad, vh - 8)
    rect.value = { top, left: r.left - pad, width: r.width + pad * 2, height: Math.max(bottom - top, 24) }
    const R = rect.value
    const clampX = (x: number) => Math.min(Math.max(x, 12), vw - width - 12)
    const clampY = (y: number) => Math.min(Math.max(y, 12), vh - height - 12)
    if (R.left + R.width + 16 + width <= vw - 12 && R.width < vw * 0.45)
      box.value = { left: R.left + R.width + 16, top: clampY(R.top + R.height / 2 - height / 2), width }
    else if (R.top + R.height + 14 + height <= vh - 12)
      box.value = { left: clampX(R.left + R.width / 2 - width / 2), top: R.top + R.height + 14, width }
    else if (R.top - 14 - height >= 12)
      box.value = { left: clampX(R.left + R.width / 2 - width / 2), top: R.top - 14 - height, width }
    else
      box.value = { left: clampX(vw / 2 - width / 2), top: vh - height - 16, width }
  } else {
    rect.value = null
    box.value = { left: vw / 2 - width / 2, top: Math.max(vh / 2 - height / 2, 12), width }
  }
  frame = requestAnimationFrame(place)
}

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') endTour()
  else if (e.key === 'ArrowRight' || e.key === 'Enter') { e.preventDefault(); next() }
  else if (e.key === 'ArrowLeft') back()
}

onMounted(() => {
  window.addEventListener('keydown', onKey)
  show(0)
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKey)
  cancelAnimationFrame(frame)
  tourSidebar.value = false
})
watch(tourActive, on => { if (!on) cancelAnimationFrame(frame) })
</script>

<template>
  <div class="tour" role="dialog" aria-modal="true" :aria-label="`Обучение: ${step.title}`">
    <!-- Catches clicks: during the tour the panel is to be looked at, not used. -->
    <div class="catch" :class="{ plain: !rect }" />
    <div v-if="rect" class="spot" :style="{ top: `${rect.top}px`, left: `${rect.left}px`, width: `${rect.width}px`, height: `${rect.height}px` }" />

    <section ref="boxEl" class="box" :class="{ center: !rect }" :style="{ top: `${box.top}px`, left: `${box.left}px`, width: `${box.width}px` }">
      <div class="box-top">
        <span class="count">{{ index + 1 }} / {{ steps.length }}</span>
        <button class="skip" @click="endTour">{{ last ? 'Закрыть' : 'Пропустить' }}</button>
      </div>
      <h2>{{ step.title }}</h2>
      <p>{{ step.text }}</p>
      <div class="dots" aria-hidden="true">
        <i v-for="(_, i) in steps" :key="i" :class="{ on: i === index, done: i < index }" />
      </div>
      <div class="box-foot">
        <button v-if="index > 0" class="btn btn-ghost" :disabled="busy" @click="back"><Icon name="chevronLeft" />Назад</button>
        <button class="btn btn-primary" :disabled="busy && !last" @click="next">
          {{ index === 0 ? 'Начать' : last ? 'Начать работу' : 'Далее' }}<Icon v-if="!last" name="chevronRight" />
        </button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.tour { position: fixed; inset: 0; z-index: 1000; }
.catch { position: absolute; inset: 0; }
/* Without a spotlight the dim comes from here; with one, from the spotlight's shadow. */
.catch.plain { background: rgb(20 8 30 / 62%); }
.spot {
  position: absolute; border-radius: 12px; pointer-events: none;
  box-shadow: 0 0 0 9999px rgb(20 8 30 / 62%), 0 0 0 3px rgb(255 255 255 / 85%), 0 0 24px 6px rgb(197 139 224 / 55%);
  transition: top .25s ease, left .25s ease, width .25s ease, height .25s ease;
}
.box {
  position: absolute; background: var(--surface); border-radius: 14px; padding: 16px 18px 14px;
  box-shadow: var(--shadow-lg); display: flex; flex-direction: column; gap: 8px;
  transition: top .25s ease, left .25s ease;
}
.box.center { transition: none; }
.box-top { display: flex; align-items: center; }
.count { font-size: 12px; font-weight: 650; color: var(--plum-600); background: var(--plum-50); padding: 2px 8px; border-radius: 999px; }
.skip { margin-left: auto; border: 0; background: none; color: var(--text-3); font: inherit; font-size: 13px; cursor: pointer; padding: 2px 4px; }
.skip:hover { color: var(--text); text-decoration: underline; }
h2 { font-size: 16.5px; }
p { margin: 0; color: var(--text-2); font-size: 14px; line-height: 1.55; }
.dots { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 2px; }
.dots i { width: 6px; height: 6px; border-radius: 50%; background: var(--border-strong); }
.dots i.done { background: var(--plum-500); opacity: .45; }
.dots i.on { background: var(--plum-600); width: 16px; border-radius: 3px; }
.box-foot { display: flex; justify-content: flex-end; gap: 8px; margin-top: 4px; }
</style>
