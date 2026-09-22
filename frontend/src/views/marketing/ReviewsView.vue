<script setup lang="ts">
import { ref, watch } from 'vue'
import { marketingApi, type Paged, type ReviewRow } from '../../api'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'
import Pager from '../../components/Pager.vue'
import { dateTime, loc } from '../../format'

const status = ref<'' | 'New' | 'Answered'>('')
const rating = ref<number | ''>('')
const search = ref('')
const page = ref(1)
const data = ref<(Paged<ReviewRow> & { counts: Partial<Record<'New' | 'Answered', number>>; average: number }) | null>(null)

async function load() {
  data.value = await marketingApi.reviews({ status: status.value, rating: rating.value, search: search.value.trim(), page: page.value })
}
let timer = 0
watch(search, () => { clearTimeout(timer); timer = window.setTimeout(() => { page.value = 1; load() }, 300) })
watch([status, rating], () => { page.value = 1; load() })
watch(page, load, { immediate: true })

const stars = (n: number) => '★'.repeat(n) + '☆'.repeat(5 - n)

// Reply
const replying = ref<ReviewRow | null>(null)
const replyText = ref('')
const error = ref('')
const templates = ['Спасибо за отзыв! Рады, что вам понравилось 🙏', 'Спасибо, что написали. Нам жаль, что так получилось — передали команде и исправим.']
function openReply(r: ReviewRow) {
  replying.value = r
  replyText.value = r.reply ?? ''
  error.value = ''
}
async function sendReply() {
  if (!replying.value) return
  error.value = ''
  try {
    await marketingApi.replyReview(replying.value.id, replyText.value)
    replying.value = null
    load()
  } catch (e) {
    error.value = (e as Error).message
  }
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Обзоры</h1>
      <span v-if="data" class="avg"><span class="stars">★</span> {{ data.average.toLocaleString('ru-RU') }} средняя оценка</span>
    </div>

    <div class="filters">
      <button class="chip" :class="{ active: status === '' }" @click="status = ''">Все<span class="count">{{ (data?.counts.New ?? 0) + (data?.counts.Answered ?? 0) }}</span></button>
      <button class="chip" :class="{ active: status === 'New' }" @click="status = 'New'">Новый<span class="count">{{ data?.counts.New ?? 0 }}</span></button>
      <button class="chip" :class="{ active: status === 'Answered' }" @click="status = 'Answered'">Отвечено<span class="count">{{ data?.counts.Answered ?? 0 }}</span></button>
    </div>

    <div class="card">
      <div class="toolbar">
        <label class="search"><Icon name="search" /><input v-model="search" class="input" placeholder="Товар, клиент или текст отзыва" aria-label="Поиск" /></label>
        <select v-model="rating" class="select" aria-label="Оценка">
          <option value="">Любая оценка</option>
          <option v-for="n in [5, 4, 3, 2, 1]" :key="n" :value="n">{{ stars(n) }}</option>
        </select>
      </div>
      <div class="table-wrap">
        <table class="data">
          <thead><tr><th>Продукт</th><th>Рейтинг</th><th>Статус</th><th>Клиент</th><th>Комментарий</th><th>Создан</th><th class="right">Действия</th></tr></thead>
          <tbody>
            <tr v-for="r in data?.items" :key="r.id">
              <td><RouterLink :to="`/products/items/${r.productId}`"><b>{{ loc(r.productName) }}</b></RouterLink></td>
              <td class="nowrap"><span class="stars" :class="{ low: r.rating <= 3 }">{{ stars(r.rating) }}</span></td>
              <td><span class="badge" :class="r.status === 'New' ? 'New' : 'Completed'">{{ r.status === 'New' ? 'Новый' : 'Отвечено' }}</span></td>
              <td class="nowrap"><RouterLink :to="{ path: '/customers', query: { open: r.customerId } }">{{ r.customer }}</RouterLink></td>
              <td class="comment">
                {{ r.comment }}
                <div v-if="r.reply" class="reply"><Icon name="chat" />{{ r.reply }}</div>
              </td>
              <td class="num nowrap small">{{ dateTime(r.createdAt) }}</td>
              <td class="right nowrap">
                <button class="btn btn-sm" :class="{ 'btn-primary': r.status === 'New' }" @click="openReply(r)">{{ r.status === 'New' ? 'Ответить' : 'Изменить ответ' }}</button>
                <RouterLink v-if="r.conversationId" :to="`/chat/${r.conversationId}`" class="btn btn-sm btn-ghost btn-icon" title="Открыть в чате" aria-label="Открыть в чате"><Icon name="chat" /></RouterLink>
              </td>
            </tr>
            <tr v-if="data && !data.items.length"><td colspan="7" class="empty">Отзывов не найдено</td></tr>
            <tr v-if="!data"><td colspan="7"><div class="skeleton" style="height: 240px" /></td></tr>
          </tbody>
        </table>
      </div>
      <Pager v-if="data" v-model:page="page" :page-size="data.pageSize" :total="data.total" />
    </div>

    <Modal v-if="replying" title="Ответ на отзыв" width="520px" persistent @close="replying = null">
      <div class="stack">
        <div class="quote">
          <span class="stars">{{ stars(replying.rating) }}</span> <b>{{ loc(replying.productName) }}</b>
          <p>{{ replying.comment }}</p>
          <span class="faint small">{{ replying.customer }}</span>
        </div>
        <div class="row">
          <button v-for="t in templates" :key="t" type="button" class="btn btn-sm btn-ghost" @click="replyText = t">{{ t.slice(0, 28) }}…</button>
        </div>
        <textarea v-model="replyText" class="textarea" rows="4" placeholder="Ваш ответ будет опубликован под отзывом" aria-label="Ответ" />
        <div v-if="error" class="error-banner">{{ error }}</div>
      </div>
      <template #footer>
        <button class="btn" @click="replying = null">Отмена</button>
        <button class="btn btn-primary" :disabled="!replyText.trim()" @click="sendReply">Опубликовать ответ</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.avg { font-weight: 600; color: var(--text-2); }
.stars { color: #d99100; letter-spacing: 1px; }
.stars.low { color: #c46a1a; }
.filters { display: flex; gap: 8px; margin-bottom: 12px; }
.toolbar { display: flex; gap: 10px; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--border); flex-wrap: wrap; }
.comment { max-width: 380px; }
.reply { display: flex; gap: 6px; margin-top: 6px; padding: 6px 8px; border-radius: 8px; background: var(--plum-50); font-size: 12px; color: var(--text-2); }
.reply svg { width: 13px; height: 13px; flex: none; margin-top: 2px; color: var(--plum-600); }
.stack { display: flex; flex-direction: column; gap: 12px; }
.quote { padding: 10px 12px; border-left: 3px solid var(--plum-500); background: var(--surface-2); border-radius: 0 8px 8px 0; }
.quote p { margin: 6px 0 4px; }
.small { font-size: 12px; }
</style>
