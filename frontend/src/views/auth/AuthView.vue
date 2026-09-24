<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PhoneInput from '../../shop/components/PhoneInput.vue'
import { AuthError, demo, loadDemo, login, register, type StorePlatform } from '../../auth'

// Вход и регистрация администратора. One page, two modes:
// новый — имя → номер телефона → пароль → выбор платформы (веб-сайт или Telegram Mini App) → аккаунт и магазин;
// существующий — номер телефона → пароль.
const props = defineProps<{ mode: 'login' | 'register' }>()
const route = useRoute()
const router = useRouter()

const name = ref('')
const storeName = ref('')
const phone = ref('')
const password = ref('')
const showPassword = ref(false)
// Где будет работать магазин. Telegram-магазин живёт в собственном боте администратора, поэтому нужен токен.
const platform = ref<StorePlatform>('Website')
const botToken = ref('')
const busy = ref(false)
const error = ref('')
const badField = ref('')

const isRegister = computed(() => props.mode === 'register')
// Where to land afterwards: the page the admin was heading for, or the dashboard.
const next = computed(() => (typeof route.query.next === 'string' && route.query.next.startsWith('/') ? route.query.next : '/dashboard'))

watch(() => props.mode, () => { error.value = ''; badField.value = ''; password.value = '' })

function fail(message: string, field: string) {
  error.value = message
  badField.value = field
  return false
}

function valid() {
  error.value = ''
  badField.value = ''
  if (isRegister.value && name.value.trim().length < 2) return fail('Введите имя — так к вам будет обращаться панель.', 'name')
  if (phone.value.length < 9) return fail('Введите номер телефона полностью.', 'phone')
  if (password.value.length < 6) return fail('Пароль должен быть не короче 6 символов.', 'password')
  if (isRegister.value && platform.value === 'Telegram' && !botToken.value.trim())
    return fail('Вставьте токен бота из @BotFather.', 'botToken')
  return true
}

async function submit() {
  if (busy.value || !valid()) return
  busy.value = true
  try {
    const full = `+998${phone.value}`
    if (isRegister.value)
      await register(name.value.trim(), full, password.value, storeName.value.trim(), platform.value, botToken.value.trim())
    else await login(full, password.value)
    router.replace(next.value)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Не удалось выполнить вход.'
    badField.value = e instanceof AuthError && e.field ? e.field : ''
    // Checking a bot token takes a moment; say what failed rather than leaving the button grey.
  } finally {
    busy.value = false
  }
}

// Ready-made accounts of the demo store, offered so a presentation doesn't start with typing.
onMounted(loadDemo)

async function signInAsDemo() {
  if (!demo.value || busy.value) return
  busy.value = true
  error.value = ''
  try {
    await login(demo.value.admin.phone, demo.value.admin.password)
    router.replace(next.value)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Не удалось войти в демо-магазин.'
  } finally {
    busy.value = false
  }
}

/** "+998901111111" → "+998 90 111 11 11" (the same shape the phone field shows). */
function pretty(phone: string) {
  const d = phone.replace(/\D/g, '').slice(-9)
  return d.length === 9 ? `+998 ${d.slice(0, 2)} ${d.slice(2, 5)} ${d.slice(5, 7)} ${d.slice(7)}` : phone
}

const perks = [
  { title: 'Готовый магазин', text: 'Сайт с каталогом, корзиной и оформлением заказа — сразу после регистрации.' },
  { title: 'Всё в одной панели', text: 'Категории, товары, цены и фото. Изменения видны покупателям мгновенно.' },
  { title: 'Заказы по этапам', text: 'Подтверждение, сборка и доставка — клиент видит актуальный статус.' },
]
</script>

<template>
  <div class="auth">
    <header class="auth-top">
      <span class="mark">
        <svg viewBox="0 0 40 40" width="34" height="34" aria-hidden="true">
          <rect width="40" height="40" rx="12" fill="#1f7aec" />
          <path d="M14 29V12.5h7.2a6.2 6.2 0 0 1 0 12.4H14" fill="none" stroke="#fff" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round" />
          <circle cx="28.5" cy="28.5" r="3.2" fill="#35d07f" />
        </svg>
        <span>Plum <b>Market</b></span>
      </span>
      <RouterLink class="alt" :to="isRegister ? '/login' : '/register'">
        <span class="long">{{ isRegister ? 'У меня уже есть магазин' : 'Создать магазин' }}</span>
        <span class="short">{{ isRegister ? 'Войти' : 'Создать' }}</span>
      </RouterLink>
    </header>

    <main class="auth-main">
      <div class="intro">
        <h1>{{ isRegister ? 'Откройте свой магазин' : 'С возвращением' }}</h1>
        <p>
          {{ isRegister
            ? 'Имя, номер телефона и пароль — этого хватит, чтобы получить магазин и панель управления им.'
            : 'Войдите, чтобы вернуться к заказам, товарам и клиентам своего магазина.' }}
        </p>
      </div>

      <form class="a-panel" novalidate @submit.prevent="submit">
        <div class="tabs" role="tablist">
          <RouterLink to="/login" role="tab" class="tab" :class="{ on: !isRegister }">Вход</RouterLink>
          <RouterLink to="/register" role="tab" class="tab" :class="{ on: isRegister }">Регистрация</RouterLink>
        </div>

        <label v-if="isRegister" class="a-row">
          <span>Ваше имя</span>
          <input v-model="name" class="in" :class="{ bad: badField === 'name' }" autocomplete="name" placeholder="Например, Азиз" />
        </label>

        <label class="a-row">
          <span>Номер телефона</span>
          <PhoneInput v-model="phone" :invalid="badField === 'phone'" />
        </label>

        <label class="a-row">
          <span>Пароль</span>
          <span class="pw">
            <input v-model="password" class="in" :class="{ bad: badField === 'password' }" :type="showPassword ? 'text' : 'password'"
              :autocomplete="isRegister ? 'new-password' : 'current-password'" :placeholder="isRegister ? 'Минимум 6 символов' : 'Ваш пароль'" />
            <button type="button" class="eye" :aria-label="showPassword ? 'Скрыть пароль' : 'Показать пароль'" @click="showPassword = !showPassword">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
                <path d="M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Z" />
                <circle cx="12" cy="12" r="3.2" />
                <path v-if="!showPassword" d="M4 20 20 4" />
              </svg>
            </button>
          </span>
        </label>

        <fieldset v-if="isRegister" class="a-row platform">
          <span>Где будет работать магазин</span>
          <div class="choices">
            <label :class="{ on: platform === 'Website' }">
              <input v-model="platform" type="radio" value="Website" />
              <b>Веб-сайт</b>
              <small>Магазин открывается по обычной ссылке в браузере.</small>
            </label>
            <label :class="{ on: platform === 'Telegram' }">
              <input v-model="platform" type="radio" value="Telegram" />
              <b>Telegram Mini App</b>
              <small>Магазин открывается внутри вашего Telegram-бота.</small>
            </label>
          </div>
        </fieldset>

        <div v-if="isRegister && platform === 'Telegram'" class="a-row bot">
          <ol>
            <li>Откройте <a href="https://t.me/BotFather" target="_blank" rel="noopener">@BotFather</a> и отправьте команду <code>/newbot</code>.</li>
            <li>Укажите название бота и его username — он должен заканчиваться на <code>bot</code>.</li>
            <li>Скопируйте токен из ответа и вставьте его ниже.</li>
          </ol>
          <label>
            <span>Токен бота</span>
            <input v-model="botToken" class="in" :class="{ bad: badField === 'botToken' }" spellcheck="false"
              autocomplete="off" placeholder="8123456789:AAF..." />
            <small>Название и username бота подставятся сами — мы проверим токен в Telegram.</small>
          </label>
        </div>

        <label v-if="isRegister" class="a-row">
          <span>Название магазина <i>необязательно</i></span>
          <input v-model="storeName" class="in" placeholder="Например, Plum Bakery" />
          <small>Так магазин увидят покупатели. Название можно изменить позже.</small>
        </label>

        <p v-if="error" class="error">{{ error }}</p>

        <button class="go" type="submit" :disabled="busy">
          {{ busy ? (isRegister && platform === 'Telegram' ? 'Проверяем бота…' : 'Минуту…')
            : isRegister ? 'Создать магазин' : 'Войти' }}
        </button>

        <p class="foot">
          {{ isRegister ? 'Уже зарегистрированы?' : 'Ещё нет магазина?' }}
          <RouterLink :to="isRegister ? '/login' : '/register'">{{ isRegister ? 'Войти' : 'Зарегистрироваться' }}</RouterLink>
        </p>
      </form>

      <!-- Demo store: a filled-in shop to look around in, for people who don't want to build one first. -->
      <section v-if="demo" class="demo">
        <div class="demo-head">
          <b>Демо-магазин «{{ demo.storeName }}»</b>
          <span>Каталог, клиенты и заказы за год — чтобы сразу посмотреть, как всё работает.</span>
        </div>
        <button type="button" class="demo-go" :disabled="busy" @click="signInAsDemo">Войти как демо-администратор</button>
        <dl>
          <div>
            <dt>Администратор</dt>
            <dd>{{ pretty(demo.admin.phone) }} · пароль {{ demo.admin.password }}</dd>
          </div>
          <div>
            <dt>Покупатель</dt>
            <dd>{{ pretty(demo.customer.phone) }} · имя {{ demo.customer.name.split(' ')[0] }}</dd>
          </div>
        </dl>
        <RouterLink :to="`/shop/${demo.storeSlug}`" class="demo-shop">Открыть витрину магазина ↗</RouterLink>
      </section>

      <ul class="perks">
        <li v-for="p in perks" :key="p.title">
          <b>{{ p.title }}</b>
          <span>{{ p.text }}</span>
        </li>
      </ul>
    </main>

    <footer class="auth-foot">
      <span>Прототип · MVP</span>
    </footer>
  </div>
</template>

<style scoped>
/* Own palette: the storefront blue, so the panel a merchant signs in to looks like the shop it runs. */
.auth {
  --blue: #1f7aec;
  --blue-600: #1766cf;
  --blue-50: #eef5ff;
  --blue-100: #dbe9fd;
  --green: #35d07f;
  --ink: #152033;
  --ink-2: #4b5567;
  --ink-3: #8a93a3;
  --line: #e2e8f1;
  --card: #fff;

  min-height: 100vh;
  display: flex;
  flex-direction: column;
  color: var(--ink);
  background:
    radial-gradient(900px 420px at 50% -120px, #dbe9fd 0%, rgb(219 233 253 / 0%) 70%),
    linear-gradient(180deg, #f2f6fc 0%, #f7f9fc 100%);
  font-size: 15px;
}
.auth a { text-decoration: none; }

.auth-top { display: flex; align-items: center; gap: 16px; padding: 22px 24px; max-width: 1080px; width: 100%; margin: 0 auto; }
.mark { display: inline-flex; align-items: center; gap: 10px; font-size: 17px; font-weight: 600; color: var(--ink); }
.mark b { font-weight: 800; color: var(--blue); }
.alt .short { display: none; }
.alt { margin-left: auto; white-space: nowrap; height: 38px; display: inline-flex; align-items: center; padding: 0 16px; border-radius: 999px; background: #fff; border: 1px solid var(--line); color: var(--ink-2); font-weight: 600; font-size: 14px; }
.alt:hover { border-color: var(--blue-100); color: var(--blue); }

.auth-main { width: 100%; max-width: 460px; margin: 0 auto; padding: 8px 20px 40px; }
.intro { text-align: center; margin-bottom: 20px; }
.intro h1 { font-size: 29px; line-height: 1.15; letter-spacing: -0.02em; font-weight: 700; }
.intro p { margin: 10px auto 0; max-width: 420px; color: var(--ink-2); font-size: 14.5px; line-height: 1.5; }

.a-panel {
  background: var(--card); border: 1px solid var(--line); border-radius: 20px; padding: 10px 24px 24px;
  box-shadow: 0 1px 2px rgb(21 32 51 / 4%), 0 18px 44px rgb(21 32 51 / 8%);
  display: flex; flex-direction: column; gap: 16px;
}
.tabs { display: grid; grid-template-columns: 1fr 1fr; gap: 4px; padding: 4px; margin: 4px -4px 2px; background: #f2f5fa; border-radius: 12px; }
.tab { height: 38px; display: grid; place-items: center; border-radius: 9px; font-weight: 600; font-size: 14.5px; color: var(--ink-3); }
.tab.on { background: #fff; color: var(--ink); box-shadow: 0 1px 2px rgb(21 32 51 / 10%); }

.a-row { display: flex; flex-direction: column; gap: 7px; }
.a-row > span { font-size: 13.5px; font-weight: 600; color: var(--ink-2); }
.a-row > span i { font-style: normal; font-weight: 500; color: var(--ink-3); }
.a-row small { color: var(--ink-3); font-size: 12.5px; }
.in {
  height: 50px; width: 100%; border: 1.5px solid var(--line); border-radius: 14px; background: #fff;
  padding: 0 14px; font: inherit; font-size: 16px; color: var(--ink); outline: none;
  transition: border-color .15s, box-shadow .15s;
}
.in::placeholder { color: #b3bac6; }
.in:focus { border-color: var(--blue); box-shadow: 0 0 0 4px var(--blue-50); }
.in.bad { border-color: #e5484d; }
.pw { position: relative; display: block; }
.pw .in { padding-right: 48px; }
.eye { position: absolute; right: 6px; top: 6px; width: 38px; height: 38px; display: grid; place-items: center; border: 0; background: none; border-radius: 10px; color: var(--ink-3); cursor: pointer; }
.eye:hover { background: var(--blue-50); color: var(--blue); }
.eye svg { width: 20px; height: 20px; }

.error { margin: -4px 0 0; padding: 10px 12px; border-radius: 12px; background: #fdecec; color: #b42318; font-size: 13.5px; }
.go {
  height: 50px; border: 0; border-radius: 14px; background: var(--blue); color: #fff; font: inherit; font-size: 15.5px;
  font-weight: 650; cursor: pointer; transition: background .15s;
}
.go:hover:not(:disabled) { background: var(--blue-600); }
.go:disabled { opacity: .6; cursor: default; }
.foot { margin: 0; text-align: center; color: var(--ink-3); font-size: 13.5px; }
.foot a { color: var(--blue); font-weight: 600; }

.platform { border: 0; margin: 0; padding: 0; }
.platform > span { font-size: 13.5px; font-weight: 600; color: var(--ink-2); }
.choices { display: grid; gap: 8px; }
.choices label {
  display: grid; grid-template-columns: auto 1fr; gap: 2px 10px; align-items: center; cursor: pointer;
  border: 1.5px solid var(--line); border-radius: 14px; padding: 11px 14px; transition: border-color .15s, background .15s;
}
.choices label.on { border-color: var(--blue); background: var(--blue-50); }
.choices input { grid-row: span 2; width: 18px; height: 18px; accent-color: var(--blue); margin: 0; }
.choices b { font-size: 14.5px; font-weight: 650; }
.choices small { color: var(--ink-2); font-size: 12.5px; line-height: 1.4; }

.bot { gap: 12px; }
.bot ol { margin: 0; padding-left: 20px; display: grid; gap: 5px; color: var(--ink-2); font-size: 13px; line-height: 1.45; }
.bot ol a { color: var(--blue); font-weight: 600; }
.bot code { background: #f2f5fa; border-radius: 5px; padding: 1px 5px; font-size: 12.5px; }
.bot label { display: flex; flex-direction: column; gap: 7px; }
.bot label > span { font-size: 13.5px; font-weight: 600; color: var(--ink-2); }
.bot .in { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 14px; }
.bot small { color: var(--ink-3); font-size: 12.5px; }

.demo { margin-top: 18px; background: #fff; border: 1px solid var(--line); border-radius: 18px; padding: 16px 18px 18px; }
.demo-head { display: flex; flex-direction: column; gap: 3px; margin-bottom: 12px; }
.demo-head b { font-size: 14.5px; font-weight: 650; }
.demo-head span { font-size: 13px; color: var(--ink-2); line-height: 1.45; }
.demo-go {
  width: 100%; height: 44px; border: 0; border-radius: 12px; background: var(--blue-50); color: var(--blue);
  font: inherit; font-size: 14.5px; font-weight: 650; cursor: pointer; transition: background .15s;
}
.demo-go:hover:not(:disabled) { background: var(--blue-100); }
.demo-go:disabled { opacity: .6; cursor: default; }
.demo dl { margin: 12px 0 0; display: grid; gap: 6px; }
.demo dl div { display: flex; gap: 8px; font-size: 13px; }
.demo dt { color: var(--ink-3); min-width: 106px; }
.demo dd { margin: 0; color: var(--ink); font-variant-numeric: tabular-nums; }
.demo-shop { display: inline-block; margin-top: 12px; font-size: 13px; color: var(--blue); font-weight: 600; }

.perks { list-style: none; margin: 22px 0 0; padding: 0; display: grid; gap: 10px; }
.perks li { display: flex; flex-direction: column; gap: 2px; padding: 12px 14px 13px 16px; border-left: 2px solid var(--green); background: rgb(255 255 255 / 55%); border-radius: 0 12px 12px 0; }
.perks b { font-size: 13.5px; font-weight: 650; }
.perks span { font-size: 13px; color: var(--ink-2); line-height: 1.45; }

.auth-foot { margin-top: auto; display: flex; gap: 14px; align-items: center; justify-content: center; padding: 18px; color: var(--ink-3); font-size: 12.5px; }
.auth-foot a { color: var(--ink-3); }
.auth-foot a:hover { color: var(--blue); }

@media (min-width: 900px) {
  .auth-main { max-width: 1080px; display: grid; grid-template-columns: minmax(0, 1fr) 460px; align-items: start; gap: 56px; padding-top: 48px; }
  .intro { grid-column: 1; text-align: left; margin-top: 18px; }
  .intro h1 { font-size: 40px; }
  .intro p { margin-left: 0; font-size: 16px; }
  .a-panel { grid-column: 2; grid-row: 1 / span 3; }
  .demo { grid-column: 1; margin-top: 26px; }
  .perks { grid-column: 1; margin-top: 18px; }
}
@media (max-width: 460px) {
  .auth-top { padding: 16px; }
  .mark { font-size: 15.5px; white-space: nowrap; }
  .alt { height: 34px; padding: 0 14px; font-size: 13.5px; }
  .alt .long { display: none; }
  .alt .short { display: inline; }
  .intro h1 { font-size: 25px; }
}
</style>
