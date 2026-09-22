<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { shopApi, type Home } from '../api'
import ProductRail from '../components/ProductRail.vue'
import SIcon from '../components/SIcon.vue'
import { count, lang, t } from '../i18n'
import { emojiFor, tintFor } from '../visuals'

const home = ref<Home | null>(null)
const failed = ref(false)
async function load() {
  failed.value = false
  try { home.value = await shopApi.home() } catch { failed.value = true }
}
watch(lang, load, { immediate: true })

// ---- Banner carousel (merchant's "Главный слайдер" banners) ----
const slide = ref(0)
const banners = computed(() => home.value?.banners.filter(b => b.desktopUrl || b.mobileUrl) ?? [])
let timer: number | undefined
function restart() {
  clearInterval(timer)
  if (banners.value.length > 1) timer = window.setInterval(() => { slide.value = (slide.value + 1) % banners.value.length }, 5500)
}
watch(banners, restart)
function go(i: number) {
  slide.value = (i + banners.value.length) % banners.value.length
  restart()
}
const pause = () => clearInterval(timer)
onBeforeUnmount(pause)
const external = (link: string | null) => !!link && /^https?:/.test(link)
</script>

<template>
  <div class="s-wrap">
    <div v-if="failed" class="s-empty">
      <div class="big">⚠️</div>
      <h3>{{ t('error') }}</h3>
      <button class="s-btn ghost" @click="load">{{ t('retry') }}</button>
    </div>

    <template v-else-if="home">
      <!-- Banners -->
      <section v-if="banners.length" class="hero" @mouseenter="pause" @mouseleave="restart">
        <div class="slides" :style="{ transform: `translateX(-${slide * 100}%)` }">
          <component :is="b.link ? (external(b.link) ? 'a' : 'RouterLink') : 'div'" v-for="b in banners" :key="b.id" class="slide"
            v-bind="b.link ? (external(b.link) ? { href: b.link, target: '_blank', rel: 'noopener' } : { to: b.link }) : {}">
            <picture>
              <source v-if="b.mobileUrl && b.mobileMediaType === 'image'" media="(max-width: 640px)" :srcset="b.mobileUrl" />
              <img :src="(b.desktopMediaType === 'image' ? b.desktopUrl : null) ?? b.mobileUrl ?? ''" :alt="b.title" />
            </picture>
          </component>
        </div>
        <template v-if="banners.length > 1">
          <button class="nav prev" aria-label="Предыдущий" @click="go(slide - 1)"><SIcon name="chevronLeft" /></button>
          <button class="nav next" aria-label="Следующий" @click="go(slide + 1)"><SIcon name="chevronRight" /></button>
          <div class="dots">
            <button v-for="(b, i) in banners" :key="b.id" :class="{ on: i === slide }" :aria-label="b.title" @click="go(i)" />
          </div>
        </template>
      </section>

      <!-- Category tiles -->
      <section class="s-section">
        <div class="s-section-head"><h2>{{ t('categories') }}</h2></div>
        <div class="tiles">
          <RouterLink v-for="c in home.categories" :key="c.id" :to="`/catalog/${c.id}`" class="tile" :style="{ background: tintFor(c.id) }">
            <span class="tile-name">{{ c.name }}</span>
            <span class="tile-count">{{ count(c.productsCount, 'items') }}</span>
            <img v-if="c.image" :src="c.image" alt="" class="tile-img" />
            <span v-else class="tile-emoji" aria-hidden="true">{{ emojiFor(c.name) }}</span>
          </RouterLink>
        </div>
      </section>

      <section v-if="home.deals.length" class="s-section deals">
        <div class="s-section-head">
          <h2>🔥 {{ t('deals') }}</h2>
        </div>
        <ProductRail :items="home.deals" />
      </section>

      <section class="s-section">
        <div class="s-section-head">
          <h2>{{ t('popular') }}</h2>
          <RouterLink to="/catalog?sort=popular" class="more">{{ t('seeAll') }} <SIcon name="chevronRight" :size="16" /></RouterLink>
        </div>
        <ProductRail :items="home.popular" />
      </section>

      <section v-for="s in home.sections" :key="s.category.id" class="s-section">
        <div class="s-section-head">
          <h2>{{ s.category.name }}</h2>
          <span class="count">{{ s.total }}</span>
          <RouterLink :to="`/catalog/${s.category.id}`" class="more">{{ t('seeAll') }} <SIcon name="chevronRight" :size="16" /></RouterLink>
        </div>
        <div v-if="s.category.children.length" class="sub-chips s-scroll">
          <RouterLink v-for="c in s.category.children" :key="c.id" :to="`/catalog/${c.id}`" class="s-chip">
            {{ c.name }} <small>{{ c.productsCount }}</small>
          </RouterLink>
        </div>
        <ProductRail :items="s.products" />
      </section>
    </template>

    <template v-else>
      <div class="s-skel" style="aspect-ratio: 3 / 1; margin-top: 20px; border-radius: 22px" />
      <div class="tiles" style="margin-top: 32px">
        <div v-for="i in 5" :key="i" class="s-skel" style="height: 128px" />
      </div>
    </template>
  </div>
</template>

<style scoped>
.hero { position: relative; margin-top: 20px; border-radius: 24px; overflow: hidden; background: var(--line); }
.slides { display: flex; transition: transform .5s cubic-bezier(.3, .7, .2, 1); }
.slide { flex: 0 0 100%; display: block; }
.slide img { display: block; width: 100%; aspect-ratio: 3 / 1; object-fit: cover; }
.nav {
  position: absolute; top: 50%; transform: translateY(-50%); width: 44px; height: 44px; border-radius: 50%; border: 0;
  background: rgb(255 255 255 / 85%); backdrop-filter: blur(6px); cursor: pointer; display: grid; place-items: center; opacity: 0; transition: opacity .2s;
}
.hero:hover .nav { opacity: 1; }
.prev { left: 16px; }
.next { right: 16px; }
.dots { position: absolute; left: 0; right: 0; bottom: 14px; display: flex; justify-content: center; gap: 6px; }
.dots button { width: 8px; height: 8px; border-radius: 4px; border: 0; padding: 0; background: rgb(255 255 255 / 55%); cursor: pointer; transition: width .25s, background .25s; }
.dots button.on { width: 24px; background: #fff; }

.tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 14px; }
.tile {
  position: relative; height: 128px; border-radius: var(--r-lg); padding: 16px; overflow: hidden; display: flex; flex-direction: column;
  transition: transform .18s, box-shadow .18s;
}
.tile:hover { transform: translateY(-2px); box-shadow: var(--lift); }
.tile-name { font-weight: 700; font-size: 16px; line-height: 1.25; max-width: 70%; position: relative; z-index: 1; }
.tile-count { font-size: 13px; color: var(--ink-2); margin-top: 4px; position: relative; z-index: 1; }
.tile-emoji { position: absolute; right: -6px; bottom: -12px; font-size: 76px; line-height: 1; transform: rotate(-10deg); transition: transform .25s; }
.tile:hover .tile-emoji { transform: rotate(0) scale(1.06); }
.tile-img { position: absolute; right: 0; bottom: 0; width: 55%; height: 80%; object-fit: cover; border-top-left-radius: 16px; }

.deals { background: linear-gradient(135deg, #e9f8f0, #eef5ff); border-radius: 24px; padding: 22px 22px 26px; }
.sub-chips { margin: -4px 0 14px; }

@media (max-width: 640px) {
  .hero { margin-top: 12px; border-radius: 18px; }
  .slide img { aspect-ratio: 2 / 1; }
  .nav { display: none; }
  .tiles { grid-template-columns: repeat(2, 1fr); gap: 10px; }
  .tile { height: 104px; padding: 13px; }
  .tile-name { font-size: 14.5px; }
  .tile-emoji { font-size: 58px; }
  .deals { margin-left: -14px; margin-right: -14px; border-radius: 0; padding: 18px 14px 22px; }
}
</style>
