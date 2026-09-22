<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { catalogApi, marketingApi, type Banner, type CategoryRow, type ProductRow } from '../../api'
import Icon from '../../components/Icon.vue'
import { loc } from '../../format'
import { useLookups } from '../../store'

const props = defineProps<{ id?: string }>()
const router = useRouter()
const { lookups } = useLookups()

const form = ref<Banner>({
  title: '', type: 'Main', categoryId: null, mobileUrl: null, mobileMediaType: 'image', desktopUrl: null, desktopMediaType: 'image',
  linkType: 'None', linkTargetId: null, linkUrl: null, isActive: true,
})
const categories = ref<CategoryRow[]>([])
const products = ref<ProductRow[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const uploading = ref<'' | 'mobile' | 'desktop'>('')

onMounted(async () => {
  const [c, p] = await Promise.all([catalogApi.categories(), catalogApi.products({ pageSize: 100 })])
  categories.value = c
  products.value = p.items
  if (props.id) form.value = await marketingApi.banner(Number(props.id))
  loading.value = false
})

async function upload(kind: 'mobile' | 'desktop', ev: Event) {
  const f = (ev.target as HTMLInputElement).files?.[0]
  ;(ev.target as HTMLInputElement).value = ''
  if (!f) return
  uploading.value = kind
  error.value = ''
  try {
    const u = await catalogApi.upload(f)
    if (u.type === 'file') throw new Error('Нужно изображение или видео')
    if (kind === 'mobile') { form.value.mobileUrl = u.url; form.value.mobileMediaType = u.type }
    else { form.value.desktopUrl = u.url; form.value.desktopMediaType = u.type }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    uploading.value = ''
  }
}

const categoryOptions = computed(() => {
  const out: { id: number; label: string }[] = []
  const walk = (parent: number | null, depth: number) => {
    for (const c of categories.value.filter(x => x.parentId === parent)) {
      out.push({ id: c.id, label: `${'— '.repeat(depth)}${loc(c.name)}` })
      walk(c.id, depth + 1)
    }
  }
  walk(null, 0)
  return out
})
const categoryName = computed(() => loc(categories.value.find(c => c.id === form.value.categoryId)?.name))
const previewTarget = computed(() => {
  const f = form.value
  if (f.linkType === 'Category') return loc(categories.value.find(c => c.id === f.linkTargetId)?.name)
  if (f.linkType === 'Product') return loc(products.value.find(p => p.id === f.linkTargetId)?.name)
  if (f.linkType === 'Url') return f.linkUrl
  return null
})

async function save() {
  saving.value = true
  error.value = ''
  try {
    await marketingApi.saveBanner(form.value)
    router.push('/marketing/banners')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <RouterLink to="/marketing/banners" class="btn btn-ghost btn-icon" aria-label="Назад"><Icon name="chevronLeft" /></RouterLink>
      <h1>{{ id ? 'Редактировать баннер' : 'Новый баннер' }}</h1>
    </div>
    <div v-if="loading" class="skeleton" style="height: 500px" />
    <div v-else class="layout">
      <form class="stack" @submit.prevent="save">
        <section class="card card-pad">
          <label class="field"><span>Название (для вас)</span><input v-model="form.title" class="input" placeholder="Например: Кофе −20% по утрам" /></label>
          <div class="field">
            <span>Тип баннера</span>
            <div class="choice">
              <label :class="{ on: form.type === 'Main' }"><input v-model="form.type" type="radio" value="Main" /><b>Основной</b><small>Слайдер на главной</small></label>
              <label :class="{ on: form.type === 'Category' }"><input v-model="form.type" type="radio" value="Category" /><b>Баннер категории</b><small>Над товарами категории</small></label>
            </div>
          </div>
          <label v-if="form.type === 'Category'" class="field"><span>Категория</span>
            <select v-model="form.categoryId" class="select">
              <option :value="null" disabled>Выберите категорию</option>
              <option v-for="c in categoryOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
            </select>
          </label>
        </section>

        <section class="card card-pad">
          <h2>Изображение или видео</h2>
          <div class="media">
            <div v-for="kind in (['mobile', 'desktop'] as const)" :key="kind" class="slot">
              <b>{{ kind === 'mobile' ? 'Мобильная версия' : 'Десктопная версия' }}</b>
              <small class="faint">{{ kind === 'mobile' ? 'рекомендуется 800×400 (2:1)' : 'рекомендуется 1200×400 (3:1)' }}</small>
              <label class="drop" :class="kind">
                <template v-if="kind === 'mobile' ? form.mobileUrl : form.desktopUrl">
                  <video v-if="(kind === 'mobile' ? form.mobileMediaType : form.desktopMediaType) === 'video'" :src="(kind === 'mobile' ? form.mobileUrl : form.desktopUrl)!" muted autoplay loop />
                  <img v-else :src="(kind === 'mobile' ? form.mobileUrl : form.desktopUrl)!" alt="" />
                </template>
                <span v-else class="ph"><Icon name="upload" />{{ uploading === kind ? 'Загрузка…' : 'Загрузить' }}</span>
                <input type="file" accept="image/*,video/*" hidden @change="upload(kind, $event)" />
              </label>
              <button v-if="kind === 'mobile' ? form.mobileUrl : form.desktopUrl" type="button" class="btn btn-sm btn-ghost btn-danger"
                      @click="kind === 'mobile' ? (form.mobileUrl = null) : (form.desktopUrl = null)">Удалить</button>
            </div>
          </div>
        </section>

        <section class="card card-pad">
          <h2>Переход по нажатию</h2>
          <div class="seg">
            <button v-for="t in (['None', 'Category', 'Product', 'Url'] as const)" :key="t" type="button" :class="{ on: form.linkType === t }"
                    @click="form.linkType = t; form.linkTargetId = null">{{ { None: 'Без ссылки', Category: 'Категория', Product: 'Товар', Url: 'Ссылка' }[t] }}</button>
          </div>
          <select v-if="form.linkType === 'Category'" v-model="form.linkTargetId" class="select" aria-label="Категория для перехода">
            <option :value="null" disabled>Выберите категорию</option>
            <option v-for="c in categoryOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
          </select>
          <select v-if="form.linkType === 'Product'" v-model="form.linkTargetId" class="select" aria-label="Товар для перехода">
            <option :value="null" disabled>Выберите товар</option>
            <option v-for="p in products" :key="p.id" :value="p.id">{{ loc(p.name) }}</option>
          </select>
          <input v-if="form.linkType === 'Url'" v-model="form.linkUrl" class="input" placeholder="https://…" aria-label="Ссылка" />
        </section>

        <section class="card card-pad">
          <label class="switch"><input v-model="form.isActive" type="checkbox" /><span class="track" />Показывать в магазине</label>
        </section>
        <div v-if="error" class="error-banner">{{ error }}</div>
        <div class="actions">
          <RouterLink to="/marketing/banners" class="btn">Отмена</RouterLink>
          <button type="submit" class="btn btn-primary" :disabled="saving">Сохранить</button>
        </div>
      </form>

      <aside class="phone-wrap">
        <span class="faint small">Предпросмотр на телефоне</span>
        <div class="phone">
          <div class="notch" />
          <div class="screen">
            <div class="store-head"><b>{{ lookups?.store.storeName ?? 'Магазин' }}</b><Icon name="search" /></div>
            <div v-if="form.type === 'Category'" class="crumb">{{ categoryName || 'Категория' }}</div>
            <div class="banner">
              <video v-if="form.mobileUrl && form.mobileMediaType === 'video'" :src="form.mobileUrl" muted autoplay loop />
              <img v-else-if="form.mobileUrl" :src="form.mobileUrl" alt="" />
              <span v-else>Мобильный баннер</span>
            </div>
            <div v-if="form.type === 'Main'" class="dots"><i class="on" /><i /><i /></div>
            <p v-if="previewTarget" class="tap">По нажатию → {{ previewTarget }}</p>
            <div class="tiles"><i v-for="n in 4" :key="n" /></div>
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.layout { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 20px; align-items: start; max-width: 1100px; }
.stack { display: flex; flex-direction: column; gap: 16px; }
.card { display: flex; flex-direction: column; gap: 12px; }
.card h2 { margin: 0; }
.choice { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.choice label { border: 1px solid var(--border-strong); border-radius: 10px; padding: 10px 12px; cursor: pointer; display: flex; flex-direction: column; gap: 2px; }
.choice input { position: absolute; opacity: 0; }
.choice small { color: var(--text-2); font-size: 12px; }
.choice .on { border-color: var(--plum-500); background: var(--plum-50); box-shadow: 0 0 0 1px var(--plum-500); }
.media { display: grid; grid-template-columns: 2fr 3fr; gap: 14px; }
.slot { display: flex; flex-direction: column; gap: 4px; align-items: flex-start; }
.slot small { font-size: 11px; }
.drop { width: 100%; border-radius: 10px; overflow: hidden; border: 1.5px dashed var(--border-strong); cursor: pointer; display: grid; place-items: center; background: var(--surface-2); margin-top: 4px; }
.drop.mobile { aspect-ratio: 2 / 1; }
.drop.desktop { aspect-ratio: 3 / 1; }
.drop:hover { border-color: var(--plum-500); }
.drop img, .drop video { width: 100%; height: 100%; object-fit: cover; }
.ph { display: flex; flex-direction: column; align-items: center; gap: 4px; font-size: 12px; color: var(--text-2); }
.ph svg { width: 22px; height: 22px; }
.seg { display: flex; background: var(--bg); padding: 3px; border-radius: 9px; align-self: flex-start; flex-wrap: wrap; }
.seg button { border: 0; background: none; font: inherit; font-size: 13px; font-weight: 550; padding: 6px 12px; border-radius: 7px; cursor: pointer; color: var(--text-2); }
.seg button.on { background: var(--surface); color: var(--text); box-shadow: var(--shadow); }
.actions { display: flex; justify-content: flex-end; gap: 8px; }
.phone-wrap { position: sticky; top: 16px; display: flex; flex-direction: column; gap: 8px; align-items: center; }
.phone { width: 260px; height: 520px; border-radius: 36px; background: #111; padding: 12px; position: relative; box-shadow: var(--shadow-lg); }
.notch { position: absolute; top: 12px; left: 50%; transform: translateX(-50%); width: 90px; height: 20px; background: #111; border-radius: 0 0 12px 12px; z-index: 2; }
.screen { width: 100%; height: 100%; border-radius: 26px; background: #fff; overflow: hidden; padding: 30px 12px 12px; display: flex; flex-direction: column; gap: 10px; }
.store-head { display: flex; justify-content: space-between; align-items: center; font-size: 14px; }
.store-head svg { width: 16px; height: 16px; color: var(--text-3); }
.crumb { font-size: 12px; font-weight: 600; color: var(--plum-600); }
.banner { aspect-ratio: 2 / 1; border-radius: 12px; overflow: hidden; background: var(--plum-100); display: grid; place-items: center; color: var(--plum-600); font-size: 12px; }
.banner img, .banner video { width: 100%; height: 100%; object-fit: cover; }
.dots { display: flex; justify-content: center; gap: 4px; margin-top: -4px; }
.dots i { width: 6px; height: 6px; border-radius: 3px; background: #ddd; }
.dots i.on { width: 14px; background: var(--plum-500); }
.tap { margin: 0; font-size: 11px; color: var(--text-2); text-align: center; }
.tiles { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.tiles i { aspect-ratio: 1; border-radius: 10px; background: #f1f0f4; }
.small { font-size: 12px; }
@media (max-width: 960px) { .layout { grid-template-columns: 1fr; } .phone-wrap { position: static; } .media { grid-template-columns: 1fr; } }
</style>
