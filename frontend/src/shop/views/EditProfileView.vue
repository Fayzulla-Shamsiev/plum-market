<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { shopApi, type ProfileUpdate } from '../api'
import BackLink from '../components/BackLink.vue'
import PhoneInput from '../components/PhoneInput.vue'
import { t } from '../i18n'
import { me, requireLogin, signedIn } from '../state/auth'
import { showToast } from '../state/toast'

// "Редактировать профиль": name and surname, phone, e-mail, country, birth date, gender.
const countries = ['Узбекистан', 'Казахстан', 'Кыргызстан', 'Таджикистан', 'Туркменистан', 'Россия', 'Турция', 'ОАЭ', 'Другая']
const form = ref<ProfileUpdate>({ firstName: '', lastName: '', phone: '', email: null, country: null, birthDate: null, gender: null })
const error = ref('')
const busy = ref(false)
const today = new Date().toISOString().slice(0, 10)

function fill() {
  const m = me.value
  if (!m) return
  form.value = {
    firstName: m.firstName, lastName: m.lastName, phone: m.phone.replace(/\D/g, '').slice(3),
    email: m.email, country: m.country, birthDate: m.birthDate, gender: m.gender,
  }
}
async function load() {
  try { me.value = await shopApi.me(); fill() } catch { /* signed out */ }
}
onMounted(() => requireLogin(load))
watch(signedIn, s => s && load())

async function save() {
  error.value = ''
  if (!form.value.firstName.trim()) { error.value = t('nameInvalid'); return }
  if (form.value.phone.length !== 9) { error.value = t('phoneInvalid'); return }
  busy.value = true
  try {
    me.value = await shopApi.updateMe({
      ...form.value,
      email: form.value.email?.trim() || null,
      country: form.value.country || null,
      birthDate: form.value.birthDate || null,
    })
    fill()
    showToast(t('saved'))
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="s-wrap narrow">
    <BackLink to="/profile" :label="t('profile')" />
    <h1>{{ t('editProfile') }}</h1>
    <form v-if="signedIn && me" class="card" novalidate @submit.prevent="save">
      <div class="two">
        <label><span>{{ t('firstName') }}</span><input v-model="form.firstName" class="s-input" autocomplete="given-name" maxlength="40" /></label>
        <label><span>{{ t('lastName') }}</span><input v-model="form.lastName" class="s-input" autocomplete="family-name" maxlength="40" /></label>
      </div>
      <label><span>{{ t('phone') }}</span><PhoneInput v-model="form.phone" /></label>
      <label><span>{{ t('email') }}</span><input v-model="form.email" class="s-input" type="email" autocomplete="email" maxlength="120" placeholder="name@example.com" /></label>
      <div class="two">
        <label><span>{{ t('country') }}</span>
          <select v-model="form.country" class="s-input">
            <option :value="null">—</option>
            <option v-for="c in countries" :key="c" :value="c">{{ c }}</option>
          </select>
        </label>
        <label><span>{{ t('birthDate') }}</span><input v-model="form.birthDate" class="s-input" type="date" min="1900-01-01" :max="today" autocomplete="bday" /></label>
      </div>
      <fieldset>
        <legend>{{ t('gender') }}</legend>
        <div class="seg">
          <label :class="{ on: form.gender === 'male' }"><input v-model="form.gender" type="radio" value="male" /> {{ t('male') }}</label>
          <label :class="{ on: form.gender === 'female' }"><input v-model="form.gender" type="radio" value="female" /> {{ t('female') }}</label>
          <label :class="{ on: form.gender === null }"><input v-model="form.gender" type="radio" :value="null" /> {{ t('notSpecified') }}</label>
        </div>
      </fieldset>
      <p v-if="error" class="err">{{ error }}</p>
      <button type="submit" class="s-btn save" :disabled="busy">{{ t('saveBtn') }}</button>
    </form>
  </div>
</template>

<style scoped>
.narrow { max-width: 720px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0 0 18px; }
.card { background: var(--card); border-radius: 20px; padding: 22px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 16px; }
label, fieldset { display: flex; flex-direction: column; gap: 6px; font-size: 14px; font-weight: 550; color: var(--ink-2); }
fieldset { border: 0; padding: 0; margin: 0; }
legend { padding: 0; margin-bottom: 6px; }
.two { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
select.s-input { appearance: auto; }
.seg { display: flex; gap: 8px; flex-wrap: wrap; }
.seg label { flex-direction: row; align-items: center; gap: 8px; padding: 10px 14px; border-radius: 12px; border: 1.5px solid var(--line); cursor: pointer; color: var(--ink); font-weight: 500; }
.seg label.on { border-color: var(--blue); background: var(--blue-50); }
.seg input { accent-color: var(--blue); }
.err { color: var(--red); font-size: 13.5px; margin: 0; }
.save { align-self: flex-start; min-width: 180px; height: 50px; }
@media (max-width: 640px) {
  h1 { font-size: 24px; }
  .two { grid-template-columns: 1fr; }
  .card { padding: 16px; }
  .save { align-self: stretch; }
}
</style>
