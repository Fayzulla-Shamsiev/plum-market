<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, type Platforms } from '../../api'
import { admin } from '../../auth'
import Icon from '../../components/Icon.vue'

// Платформы → Веб-сайт: the shop's name and what it tells customers about itself. The same shop is behind the
// Telegram bot, so everything here shows up in both.
const data = ref<Platforms | null>(null)
const name = ref('')
const about = ref('')
const returnTerms = ref('')
const busy = ref(false)
const error = ref('')
const done = ref('')

function fill(p: Platforms) {
  data.value = p
  name.value = p.website.name
  about.value = p.website.about ?? ''
  returnTerms.value = p.website.returnTerms ?? ''
  if (admin.value) admin.value.store = { ...admin.value.store, name: p.website.name }
}
onMounted(() => api.platforms().then(fill))

async function save() {
  if (busy.value) return
  busy.value = true
  error.value = ''
  done.value = ''
  try {
    fill(await api.saveWebsite({
      name: name.value.trim(),
      about: about.value.trim() || null,
      returnTerms: returnTerms.value.trim() || null,
    }))
    done.value = 'Сохранено.'
    setTimeout(() => (done.value = ''), 2500)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="page narrow">
    <div class="page-head">
      <h1>Веб-сайт</h1>
      <a v-if="data" class="btn btn-primary" :href="data.website.url" target="_blank" rel="noopener">
        <Icon name="branches" />Открыть магазин
      </a>
    </div>

    <div v-if="!data" class="skeleton" style="height: 420px" />

    <form v-else class="stack" @submit.prevent="save">
      <section class="card card-pad">
        <div class="card-head"><h2>Название магазина</h2><span class="card-sub">его видят покупатели</span></div>
        <input v-model="name" class="input" maxlength="80" required />
      </section>

      <section class="card card-pad">
        <div class="card-head"><h2>О нас</h2><span class="card-sub">покупатель открывает из профиля</span></div>
        <textarea v-model="about" class="input area" rows="6" maxlength="4000"
          placeholder="Чем вы занимаетесь, что продаёте и почему у вас стоит покупать." />
      </section>

      <section class="card card-pad">
        <div class="card-head"><h2>Условия возврата и обмена</h2><span class="card-sub">покупатель открывает из профиля</span></div>
        <textarea v-model="returnTerms" class="input area" rows="8" maxlength="8000"
          placeholder="В какой срок принимаете товар обратно, что для этого нужно и как возвращаете деньги." />
        <p class="faint small">Абзацы разделяйте пустой строкой. Строка, начинающаяся с «## », станет заголовком.</p>
      </section>

      <div v-if="error" class="error-banner">{{ error }}</div>
      <p v-else-if="done" class="ok">{{ done }}</p>

      <div class="actions">
        <button class="btn btn-primary" type="submit" :disabled="busy">{{ busy ? 'Сохраняем…' : 'Сохранить' }}</button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 16px; }
.input { width: 100%; }
.area { width: 100%; height: auto; padding: 10px 12px; line-height: 1.5; resize: vertical; }
.small { font-size: 12.5px; margin: 8px 0 0; }
.ok { margin: 0; color: var(--good); font-size: 13px; font-weight: 500; }
.actions { position: sticky; bottom: 0; padding: 12px 0; background: linear-gradient(transparent, var(--bg) 40%); }
</style>
