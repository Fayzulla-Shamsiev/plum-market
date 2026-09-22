<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { marketingApi, type AudienceFilter, type SmsCampaignRow, type SmsStatus, type SmsTemplateRow } from '../../api'
import AudiencePicker from '../../components/AudiencePicker.vue'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'
import { count, dateTime } from '../../format'

const tab = ref<'campaigns' | 'templates'>('campaigns')
const status = ref<SmsStatus | ''>('')
const campaigns = ref<SmsCampaignRow[] | null>(null)
const counts = ref<Partial<Record<SmsStatus, number>>>({})
const templates = ref<SmsTemplateRow[] | null>(null)

const statusLabel: Record<SmsStatus, string> = { Moderation: 'На модерации', InProgress: 'В процессе', Confirmed: 'Подтверждённый', Rejected: 'Отклонённый' }
const statusClass: Record<SmsStatus, string> = { Moderation: 'OnTheWay', InProgress: 'InProgress', Confirmed: 'Completed', Rejected: 'Overdue' }
const filters: (SmsStatus | '')[] = ['', 'Moderation', 'InProgress', 'Confirmed', 'Rejected']

async function load() {
  const [c, t] = await Promise.all([marketingApi.smsCampaigns(status.value), marketingApi.smsTemplates()])
  campaigns.value = c.items
  counts.value = c.counts
  templates.value = t
}
watch(status, load)

// Moderation and sending happen on the gateway side; refresh while anything is pending.
let poll = 0
onMounted(() => {
  load()
  poll = window.setInterval(() => {
    const pending = campaigns.value?.some(c => c.status === 'Moderation' || c.status === 'InProgress')
      || templates.value?.some(t => t.status === 'Moderation')
    if (pending) load()
  }, 5000)
})
onBeforeUnmount(() => clearInterval(poll))
const total = computed(() => Object.values(counts.value).reduce((a, b) => a + (b ?? 0), 0))

// ---- new template
const tplOpen = ref(false)
const tpl = ref({ name: '', text: '' })
const seg = ref<{ segments: number; length: number; unicode: boolean; max: number; problem: string | null } | null>(null)
const tplError = ref('')
let segTimer = 0
watch(() => tpl.value.text, t => {
  clearTimeout(segTimer)
  segTimer = window.setTimeout(async () => { seg.value = t ? await marketingApi.smsSegments(t) : null }, 200)
})
async function saveTemplate() {
  tplError.value = ''
  try {
    const r = await marketingApi.createSmsTemplate(tpl.value)
    tplOpen.value = false
    tab.value = 'templates'
    tpl.value = { name: '', text: '' }
    if (r.status === 'Rejected') alert(`Шаблон отклонён: ${r.rejectReason}`)
    load()
  } catch (e) {
    tplError.value = (e as Error).message
  }
}
async function removeTemplate(t: SmsTemplateRow) {
  if (!confirm(`Удалить шаблон «${t.name}»?`)) return
  try { await marketingApi.deleteSmsTemplate(t.id); load() } catch (e) { alert((e as Error).message) }
}

// ---- new campaign: template first, then recipients
const campOpen = ref(false)
const camp = ref<{ name: string; templateId: number | null }>({ name: '', templateId: null })
const audience = ref<AudienceFilter>({})
const stats = ref({ total: 0, reachable: 0 })
const campError = ref('')
const approved = computed(() => templates.value?.filter(t => t.status === 'Confirmed') ?? [])
const chosen = computed(() => approved.value.find(t => t.id === camp.value.templateId))
function openCampaign() {
  camp.value = { name: '', templateId: approved.value[0]?.id ?? null }
  audience.value = {}
  campError.value = ''
  campOpen.value = true
}
async function saveCampaign() {
  campError.value = ''
  try {
    await marketingApi.createSmsCampaign({ name: camp.value.name, templateId: camp.value.templateId!, audience: audience.value })
    campOpen.value = false
    status.value = ''
    load()
  } catch (e) {
    campError.value = (e as Error).message
  }
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>СМС-рассылка</h1>
      <button class="btn" @click="tplOpen = true"><Icon name="plus" />Шаблон сообщения</button>
      <button class="btn btn-primary" :disabled="!approved.length" :title="approved.length ? '' : 'Нужен одобренный шаблон'" @click="openCampaign">
        <Icon name="send" />Новая SMS-рассылка
      </button>
    </div>

    <div class="tabs-main">
      <button class="chip" :class="{ active: tab === 'campaigns' }" @click="tab = 'campaigns'">Рассылки</button>
      <button class="chip" :class="{ active: tab === 'templates' }" @click="tab = 'templates'">Шаблоны <span class="count">{{ templates?.length ?? '·' }}</span></button>
    </div>

    <template v-if="tab === 'campaigns'">
      <div class="filters">
        <button v-for="f in filters" :key="f" class="chip" :class="{ active: status === f }" @click="status = f">
          {{ f ? statusLabel[f] : 'Все' }}<span class="count">{{ f ? counts[f] ?? 0 : total }}</span>
        </button>
      </div>
      <div class="card">
        <div class="table-wrap">
          <table class="data">
            <thead><tr><th>Рассылка</th><th>Шаблон</th><th class="right">Получатели</th><th class="right">Доставлено</th><th class="right">SMS на номер</th><th>Статус</th><th>Создана</th></tr></thead>
            <tbody>
              <tr v-for="c in campaigns" :key="c.id">
                <td><b>{{ c.name }}</b></td>
                <td class="tpl-cell"><span class="faint small">{{ c.template }}</span><div class="small clamp">{{ c.text }}</div></td>
                <td class="right num">{{ count(c.recipients) }}</td>
                <td class="right num">{{ c.status === 'Confirmed' ? count(c.delivered) : '—' }}</td>
                <td class="right num">{{ c.segments }}</td>
                <td>
                  <span class="badge" :class="statusClass[c.status]">{{ statusLabel[c.status] }}</span>
                  <div v-if="c.rejectReason" class="reason">{{ c.rejectReason }}</div>
                </td>
                <td class="num nowrap small">{{ dateTime(c.createdAt) }}</td>
              </tr>
              <tr v-if="campaigns && !campaigns.length"><td colspan="7" class="empty">Нет рассылок с таким статусом</td></tr>
              <tr v-if="!campaigns"><td colspan="7"><div class="skeleton" style="height: 160px" /></td></tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>

    <div v-else class="card">
      <div class="table-wrap">
        <table class="data">
          <thead><tr><th>Шаблон</th><th>Текст</th><th class="right">SMS</th><th>Статус</th><th>Создан</th><th></th></tr></thead>
          <tbody>
            <tr v-for="t in templates" :key="t.id">
              <td><b>{{ t.name }}</b></td>
              <td class="tpl-cell small">{{ t.text }}</td>
              <td class="right num">{{ t.segments }}<span v-if="t.unicode" class="faint small"> кир.</span></td>
              <td>
                <span class="badge" :class="statusClass[t.status]">{{ t.status === 'Confirmed' ? 'Одобрен' : statusLabel[t.status] }}</span>
                <div v-if="t.rejectReason" class="reason">{{ t.rejectReason }}</div>
              </td>
              <td class="num nowrap small">{{ dateTime(t.createdAt) }}</td>
              <td class="right"><button class="btn btn-sm btn-ghost btn-icon btn-danger" aria-label="Удалить шаблон" title="Удалить" @click="removeTemplate(t)"><Icon name="trash" /></button></td>
            </tr>
            <tr v-if="templates && !templates.length"><td colspan="6" class="empty">Шаблонов нет</td></tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal v-if="tplOpen" title="Новый шаблон SMS" width="520px" persistent @close="tplOpen = false">
      <div class="stack">
        <p class="warn-box">⚠️ Все SMS-шаблоны проходят модерацию у оператора перед отправкой. Рассылку можно создать только по одобренному шаблону.</p>
        <label class="field"><span>Имя шаблона</span><input v-model="tpl.name" class="input" placeholder="Например: Промо выходных" /></label>
        <label class="field">
          <span>Текст сообщения</span>
          <textarea v-model="tpl.text" class="textarea" rows="5" placeholder="Plum Bakery: …" />
        </label>
        <div v-if="seg" class="counter" :class="{ bad: seg.segments > seg.max || seg.problem }">
          <span>{{ seg.length }} симв.</span>
          <span><b>{{ seg.segments }}</b> SMS</span>
          <span>{{ seg.unicode ? 'кириллица/Unicode: 70 символов в SMS' : 'латиница: 160 символов в SMS' }}</span>
        </div>
        <p v-if="seg?.problem" class="reason">Будет отклонено: {{ seg.problem }}</p>
        <div v-if="tplError" class="error-banner">{{ tplError }}</div>
      </div>
      <template #footer>
        <button class="btn" @click="tplOpen = false">Отмена</button>
        <button class="btn btn-primary" :disabled="!tpl.name.trim() || !tpl.text.trim()" @click="saveTemplate">Отправить на модерацию</button>
      </template>
    </Modal>

    <Modal v-if="campOpen" title="Новая SMS-рассылка" width="680px" persistent @close="campOpen = false">
      <div class="stack">
        <label class="field"><span>Название</span><input v-model="camp.name" class="input" placeholder="Например: Выходные — все клиенты" /></label>
        <label class="field"><span>Шаблон (только одобренные)</span>
          <select v-model="camp.templateId" class="select">
            <option v-for="t in approved" :key="t.id" :value="t.id">{{ t.name }}</option>
          </select>
        </label>
        <p v-if="chosen" class="sms-preview">{{ chosen.text }}</p>
        <AudiencePicker v-model="audience" v-model:stats="stats" channel="sms" />
        <p v-if="chosen && stats.reachable" class="faint small">Будет отправлено {{ count(stats.reachable * chosen.segments) }} SMS ({{ chosen.segments }} на номер).</p>
        <div v-if="campError" class="error-banner">{{ campError }}</div>
      </div>
      <template #footer>
        <button class="btn" @click="campOpen = false">Отмена</button>
        <button class="btn btn-primary" :disabled="!camp.name.trim() || !camp.templateId || !stats.reachable" @click="saveCampaign">Создать рассылку</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.tabs-main, .filters { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
.filters .chip { height: 28px; font-size: 13px; }
.tpl-cell { max-width: 380px; }
.clamp { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; color: var(--text-2); }
.reason { font-size: 12px; color: var(--bad); margin-top: 4px; max-width: 280px; }
.stack { display: flex; flex-direction: column; gap: 14px; }
.warn-box { margin: 0; padding: 10px 12px; border-radius: 8px; background: var(--warn-bg); color: var(--warn); font-size: 13px; }
.counter { display: flex; gap: 16px; font-size: 12px; color: var(--text-2); padding: 6px 10px; border-radius: 8px; background: var(--surface-2); }
.counter.bad { background: var(--bad-bg); color: var(--bad); }
.sms-preview { margin: 0; padding: 10px 14px; border-radius: 14px 14px 14px 4px; background: #e9e9ee; font-size: 14px; max-width: 420px; }
.small { font-size: 12px; }
</style>
