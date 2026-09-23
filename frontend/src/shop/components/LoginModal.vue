<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { demo, loadDemo } from '../../auth'
import { shopApi } from '../api'
import { t } from '../i18n'
import { favoriteIds } from '../state/favorites'
import { storeSlug } from '../state/store'
import { viewedIds } from '../state/viewed'
import { cancelLogin, onSignedIn } from '../state/auth'
import { chatToken } from '../state/chat'
import PhoneInput from './PhoneInput.vue'
import SModal from './SModal.vue'

// Sign in by phone number + name (MVP spec). Opened by requireLogin() before checkout or the profile.
const phone = ref('')
const name = ref('')
const busy = ref(false)
const error = ref('')
const touched = ref(false)

// In the demo shop, offer the account that already has orders and reviews instead of a blank one.
onMounted(loadDemo)
function useDemoAccount() {
  if (!demo.value) return
  const { phone: demoPhone, name: demoName, favorites, viewed } = demo.value.customer
  phone.value = demoPhone.replace(/\D/g, '').slice(-9)
  name.value = demoName.split(' ')[0]
  // Favourites and browsing history are per-browser, so give this one the demo account's.
  if (!favoriteIds.value.length) favoriteIds.value = [...favorites]
  if (!viewedIds.value.length) viewedIds.value = [...viewed]
  submit()
}

async function submit() {
  touched.value = true
  error.value = ''
  if (phone.value.length !== 9 || name.value.trim().length < 2) return
  busy.value = true
  try {
    // The guest chat (if any) is attached to the account on sign-in.
    const res = await shopApi.login(phone.value, name.value.trim(), chatToken.value)
    onSignedIn(res.token, res.me)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <SModal :title="t('loginTitle')" width="420px" @close="cancelLogin">
    <p class="hint">{{ t('loginHint') }}</p>
    <form id="login-form" class="form" novalidate @submit.prevent="submit" @keydown.enter.prevent="submit">
      <label>
        <span>{{ t('phone') }}</span>
        <PhoneInput v-model="phone" :invalid="touched && phone.length !== 9" autofocus />
        <small v-if="touched && phone.length !== 9" class="err">{{ t('phoneInvalid') }}</small>
      </label>
      <label>
        <span>{{ t('firstName') }}</span>
        <input v-model="name" class="s-input" :class="{ invalid: touched && name.trim().length < 2 }" autocomplete="given-name" maxlength="60" />
        <small v-if="touched && name.trim().length < 2" class="err">{{ t('nameInvalid') }}</small>
      </label>
      <p v-if="error" class="err">{{ error }}</p>
    </form>
    <button v-if="demo && storeSlug === demo.storeSlug" type="button" class="demo" :disabled="busy" @click="useDemoAccount">
      Войти как демо-покупатель · {{ demo.customer.phone }}
    </button>
    <template #footer>
      <button class="s-btn" type="submit" form="login-form" :disabled="busy">{{ t('signIn') }}</button>
    </template>
  </SModal>
</template>

<style scoped>
.hint { margin: 0 0 16px; color: var(--ink-2); line-height: 1.5; }
.form { display: flex; flex-direction: column; gap: 14px; }
label { display: flex; flex-direction: column; gap: 6px; font-size: 14px; font-weight: 550; color: var(--ink-2); }
.err { color: var(--red); font-size: 13px; font-weight: 500; margin: 0; }
.demo {
  width: 100%; margin-top: 14px; height: 42px; border: 0; border-radius: 12px; background: var(--blue-50);
  color: var(--blue); font-weight: 600; font-size: 13.5px; cursor: pointer;
}
.demo:hover:not(:disabled) { background: var(--blue-100); }
.demo:disabled { opacity: .6; cursor: default; }
</style>
