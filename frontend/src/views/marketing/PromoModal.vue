<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { catalogApi, marketingApi, type CategoryRow, type PromoInput, type PromoRow } from '../../api'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'
import { loc } from '../../format'

const props = defineProps<{ promo: PromoRow | null }>()
const emit = defineEmits<{ close: []; saved: [] }>()

const toLocal = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
const monthLater = new Date(Date.now() + 30 * 86400000)
monthLater.setHours(23, 59, 0, 0)

const form = ref<PromoInput>(props.promo
  ? { ...props.promo, startsAt: props.promo.startsAt.slice(0, 16), endsAt: props.promo.endsAt.slice(0, 16),
      categoryIds: [...props.promo.categoryIds], platforms: [...props.promo.platforms] }
  : { code: '', type: 'Percent', value: 10, maxDiscount: null, usageLimit: null, minOrderAmount: null,
      startsAt: toLocal(new Date()), endsAt: toLocal(monthLater), firstOrderOnly: false, categoryIds: [], platforms: [], isActive: true })

const unlimited = ref(form.value.usageLimit === null)
const categories = ref<CategoryRow[]>([])
const saving = ref(false)
const error = ref('')

onMounted(async () => {
  categories.value = await catalogApi.categories()
  if (!props.promo) form.value.code = (await marketingApi.generatePromo()).code
})
const topCategories = computed(() => categories.value.filter(c => !c.parentId))

function toggle<T>(list: T[], v: T) { return list.includes(v) ? list.filter(x => x !== v) : [...list, v] }
async function regenerate() { form.value.code = (await marketingApi.generatePromo()).code }

async function save() {
  saving.value = true
  error.value = ''
  try {
    await marketingApi.savePromo(props.promo?.id ?? null, {
      ...form.value,
      code: form.value.code.trim().toUpperCase(),
      usageLimit: unlimited.value ? null : form.value.usageLimit,
      maxDiscount: form.value.type === 'Percent' ? form.value.maxDiscount || null : null,
      minOrderAmount: form.value.minOrderAmount || null,
    })
    emit('saved')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Modal :title="promo ? `Промокод ${promo.code}` : 'Новый промокод'" width="640px" persistent @close="emit('close')">
    <div class="stack">
      <div class="field">
        <span>Тип скидки</span>
        <div class="seg">
          <button type="button" :class="{ on: form.type === 'Percent' }" @click="form.type = 'Percent'">Процент</button>
          <button type="button" :class="{ on: form.type === 'Fixed' }" @click="form.type = 'Fixed'">Фиксированная сумма</button>
        </div>
      </div>
      <div class="grid2">
        <label class="field">
          <span>Промокод *</span>
          <div class="row nowrap">
            <input v-model="form.code" class="input code grow" maxlength="20" @input="form.code = form.code.toUpperCase()" />
            <button type="button" class="btn btn-icon" title="Сгенерировать" aria-label="Сгенерировать код" @click="regenerate"><Icon name="sparkles" /></button>
          </div>
        </label>
        <label class="field">
          <span>Размер скидки *</span>
          <div class="suffix"><input v-model.number="form.value" type="number" min="1" class="input" /><span>{{ form.type === 'Percent' ? '%' : 'сум' }}</span></div>
        </label>
        <label v-if="form.type === 'Percent'" class="field">
          <span>Максимальная скидка, сум</span>
          <input v-model.number="form.maxDiscount" type="number" min="0" class="input" placeholder="без ограничения" />
        </label>
        <label class="field">
          <span>Минимальная сумма заказа, сум</span>
          <input v-model.number="form.minOrderAmount" type="number" min="0" class="input" placeholder="без ограничения" />
        </label>
        <div class="field">
          <span>Лимит использования</span>
          <label class="check"><input v-model="unlimited" type="checkbox" />Без ограничений</label>
          <input v-if="!unlimited" v-model.number="form.usageLimit" type="number" min="1" class="input" placeholder="Например, 100" />
        </div>
        <div />
        <label class="field"><span>Начало</span><input v-model="form.startsAt" type="datetime-local" class="input" /></label>
        <label class="field"><span>Окончание *</span><input v-model="form.endsAt" type="datetime-local" class="input" :min="form.startsAt" /></label>
      </div>

      <fieldset>
        <legend>Дополнительные ограничения</legend>
        <label class="check"><input v-model="form.firstOrderOnly" type="checkbox" />Только на первый заказ клиента</label>
        <div class="field">
          <span>Категории <em class="faint">(пусто — весь каталог)</em></span>
          <div class="chips">
            <button v-for="c in topCategories" :key="c.id" type="button" class="chip" :class="{ active: form.categoryIds.includes(c.id) }"
                    @click="form.categoryIds = toggle(form.categoryIds, c.id)">{{ loc(c.name) }}</button>
          </div>
        </div>
      </fieldset>
      <label class="switch"><input v-model="form.isActive" type="checkbox" /><span class="track" />Промокод включён</label>
      <div v-if="error" class="error-banner">{{ error }}</div>
    </div>
    <template #footer>
      <button class="btn" @click="emit('close')">Отмена</button>
      <button class="btn btn-primary" :disabled="saving || !form.code.trim()" @click="save">Сохранить</button>
    </template>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 14px; }
.grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.seg { display: grid; grid-template-columns: 1fr 1fr; background: var(--bg); padding: 3px; border-radius: 9px; max-width: 360px; }
.seg button { border: 0; background: none; font: inherit; font-size: 13px; font-weight: 550; padding: 6px; border-radius: 7px; cursor: pointer; color: var(--text-2); }
.seg button.on { background: var(--surface); color: var(--text); box-shadow: var(--shadow); }
.code { font: 600 14px ui-monospace, SFMono-Regular, Menlo, monospace; letter-spacing: .05em; }
.suffix { display: flex; align-items: center; gap: 8px; }
.suffix .input { flex: 1; }
.check { display: flex; gap: 8px; align-items: center; cursor: pointer; font-weight: 400; color: var(--text); font-size: 14px; }
.check input { accent-color: var(--plum-600); width: 16px; height: 16px; }
fieldset { border: 1px solid var(--border); border-radius: 10px; padding: 12px 14px; display: flex; flex-direction: column; gap: 12px; margin: 0; }
legend { font-size: 12px; font-weight: 600; color: var(--text-2); padding: 0 4px; }
.chips { display: flex; flex-wrap: wrap; gap: 6px; }
.chip.active :deep(.pf) { color: #fff; }
em { font-style: normal; font-weight: 400; }
@media (max-width: 600px) { .grid2 { grid-template-columns: 1fr; } }
</style>
