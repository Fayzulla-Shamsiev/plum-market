<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError, shopApi, type CheckoutQuote, type CheckoutRequest, type DeliveryType, type SavedAddress } from '../api'
import PhoneInput from '../components/PhoneInput.vue'
import PinMap from '../components/PinMap.vue'
import ProductImage from '../components/ProductImage.vue'
import SIcon from '../components/SIcon.vue'
import SModal from '../components/SModal.vue'
import { formatPhone, lang, price, t } from '../i18n'
import { me, requireLogin, signedIn } from '../state/auth'
import { removeLines, selectedLines } from '../state/cart'
import { persisted } from '../state/persist'

const router = useRouter()

// ---- Choices remembered between visits ----
const prefs = persisted<{ deliveryType: DeliveryType; branchId: number | null }>('plum.shop.checkout', { deliveryType: 'Pickup', branchId: null })
const deliveryType = computed({ get: () => prefs.value.deliveryType, set: v => { prefs.value.deliveryType = v } })

// ---- Recipient (defaults to the profile; "Изменить" edits it for this order) ----
const recipient = ref({ firstName: '', lastName: '', phone: '' })
const recipientOpen = ref(false)
const draft = ref({ firstName: '', lastName: '', phone: '' })
const draftTouched = ref(false)
function fillRecipient() {
  if (!me.value) return
  recipient.value = { firstName: me.value.firstName, lastName: me.value.lastName, phone: me.value.phone.replace(/\D/g, '').slice(3) }
}
function editRecipient() {
  draft.value = { ...recipient.value }
  draftTouched.value = false
  recipientOpen.value = true
}
function saveRecipient() {
  draftTouched.value = true
  if (!draft.value.firstName.trim() || draft.value.phone.length !== 9) return
  recipient.value = { ...draft.value, firstName: draft.value.firstName.trim(), lastName: draft.value.lastName.trim() }
  recipientOpen.value = false
}
const recipientName = computed(() => `${recipient.value.firstName} ${recipient.value.lastName}`.trim())

// ---- Delivery address ----
const addresses = ref<SavedAddress[]>([])
const addressId = ref<number | 'new'>('new')
const newAddress = ref({ address: '', details: '' })
const pin = ref<{ lat: number; lng: number } | null>(null)
const chosenAddress = computed(() => (addressId.value === 'new' ? null : addresses.value.find(a => a.id === addressId.value) ?? null))
const addressText = computed(() => chosenAddress.value?.address ?? newAddress.value.address.trim())
const addressDetails = computed(() => (chosenAddress.value ? chosenAddress.value.details : newAddress.value.details.trim()) || null)
const coords = computed(() => (chosenAddress.value?.lat != null ? { lat: chosenAddress.value.lat, lng: chosenAddress.value.lng! } : pin.value))
async function forgetAddress(a: SavedAddress) {
  await shopApi.deleteAddress(a.id).catch(() => {})
  addresses.value = addresses.value.filter(x => x.id !== a.id)
  if (addressId.value === a.id) addressId.value = addresses.value[0]?.id ?? 'new'
}

// ---- Promo code ----
const promoOpen = ref(false)
const promoInput = ref('')
const promoCode = ref<string | null>(null)
function applyPromo() {
  const v = promoInput.value.trim()
  if (v) promoCode.value = v
}
function dropPromo() {
  promoCode.value = null
  promoInput.value = ''
}

const comment = ref('')
const branchOpen = ref(false)

// ---- Live quote ----
const quote = ref<CheckoutQuote | null>(null)
const quoteError = ref(false)
const request = computed<CheckoutRequest>(() => ({
  lines: selectedLines.value.map(l => ({ productId: l.id, variant: l.variant, qty: l.qty })),
  deliveryType: deliveryType.value,
  branchId: prefs.value.branchId,
  lat: deliveryType.value === 'Delivery' ? coords.value?.lat ?? null : null,
  lng: deliveryType.value === 'Delivery' ? coords.value?.lng ?? null : null,
  promoCode: promoCode.value,
}))
let seq = 0
async function requote() {
  if (!request.value.lines.length) return
  const mine = ++seq
  try {
    const q = await shopApi.checkoutQuote(request.value)
    if (mine !== seq) return
    quote.value = q
    quoteError.value = false
    if (q.deliveryType === 'Pickup' && prefs.value.branchId !== q.branchId) prefs.value.branchId = q.branchId
  } catch {
    if (mine === seq) quoteError.value = true
  }
}
watch([() => JSON.stringify(request.value), lang], requote)

const branch = computed(() => quote.value?.branches.find(b => b.id === quote.value!.branchId) ?? null)
const toFree = computed(() => {
  const q = quote.value
  if (!q || q.deliveryType !== 'Delivery' || !q.freeDeliveryFrom || q.deliveryFee === 0) return 0
  return Math.max(0, q.freeDeliveryFrom - (q.itemsTotal - (q.promo?.discount ?? 0)))
})
const promoProblem = computed(() => (quote.value?.promo && !quote.value.promo.applied ? quote.value.promo.error : null))
// Problems other than the promo code (that one is shown next to the code field).
const blocking = computed(() => quote.value?.problems.filter(p => p !== promoProblem.value) ?? [])

// ---- Place the order ----
const placing = ref(false)
const tried = ref(false)
const placeError = ref('')
const missingAddress = computed(() => deliveryType.value === 'Delivery' && !addressText.value)
async function place() {
  tried.value = true
  placeError.value = ''
  if (!quote.value || missingAddress.value || blocking.value.length) return
  if (promoProblem.value) { placeError.value = promoProblem.value; return }
  if (!recipient.value.firstName || recipient.value.phone.length !== 9) { editRecipient(); return }
  placing.value = true
  try {
    const order = await shopApi.placeOrder({
      order: request.value,
      recipientName: recipientName.value,
      recipientPhone: recipient.value.phone,
      address: deliveryType.value === 'Delivery' ? addressText.value : null,
      addressDetails: deliveryType.value === 'Delivery' ? addressDetails.value : null,
      comment: comment.value.trim() || null,
      paymentMethod: 'Cash',
    })
    // Ordered lines leave the cart; unticked ones stay for later.
    removeLines(request.value.lines.map(l => ({ id: l.productId, variant: l.variant })))
    router.replace({ path: `/profile/orders/${order.id}`, query: { placed: '1' } })
  } catch (e) {
    placeError.value = (e as Error).message
    const fresh = e instanceof ApiError ? (e.data as { quote?: CheckoutQuote } | null)?.quote : null
    if (fresh) quote.value = fresh
  } finally {
    placing.value = false
  }
}

// ---- Start: must be signed in and have something ticked in the cart ----
async function start() {
  if (!selectedLines.value.length) return router.replace('/cart')
  try { me.value = await shopApi.me() } catch { return }
  fillRecipient()
  addresses.value = await shopApi.addresses().catch(() => [])
  if (addresses.value.length) addressId.value = addresses.value[0]!.id
  requote()
}
onMounted(() => requireLogin(start))
watch(signedIn, s => { if (!s) requireLogin(start) })
</script>

<template>
  <div class="s-wrap">
    <nav class="crumbs">
      <RouterLink to="/cart"><SIcon name="chevronLeft" :size="16" /> {{ t('backToCart') }}</RouterLink>
    </nav>
    <h1>{{ t('checkoutTitle') }}</h1>

    <div v-if="!signedIn" class="s-empty">
      <div class="big">🔐</div>
      <h3>{{ t('loginHint') }}</h3>
      <button class="s-btn" @click="requireLogin(start)">{{ t('signIn') }}</button>
    </div>

    <div v-else-if="quoteError && !quote" class="s-empty">
      <h3>{{ t('error') }}</h3>
      <button class="s-btn ghost" @click="requote">{{ t('retry') }}</button>
    </div>

    <div v-else-if="quote" class="layout">
      <div class="steps">
        <!-- 1. Receive method -->
        <section class="card">
          <h2><span class="n">1</span> {{ t('receiveMethod') }}</h2>
          <div class="ways" role="radiogroup">
            <button type="button" role="radio" :aria-checked="deliveryType === 'Pickup'" :class="{ on: deliveryType === 'Pickup' }" @click="deliveryType = 'Pickup'">
              <SIcon name="store" :size="22" />
              <span><b>{{ t('pickup') }}</b><small>{{ t('pickupHint') }} · {{ t('free') }}</small></span>
            </button>
            <button type="button" role="radio" :aria-checked="deliveryType === 'Delivery'" :class="{ on: deliveryType === 'Delivery' }" @click="deliveryType = 'Delivery'">
              <SIcon name="box" :size="22" />
              <span><b>{{ t('delivery') }}</b><small>{{ t('deliveryHint') }} · {{ quote.deliveryFeeBase ? price(quote.deliveryFeeBase) : t('free') }}</small></span>
            </button>
          </div>

          <!-- Pickup: the branch and its address -->
          <div v-if="deliveryType === 'Pickup' && branch" class="branch" :class="{ bad: !branch.canFulfill }">
            <SIcon name="pin" :size="20" class="pin" />
            <div class="grow">
              <div class="faint small">{{ t('pickupBranch') }}</div>
              <b>{{ branch.name }}</b>
              <div class="addr">{{ branch.address }}</div>
              <div v-if="!branch.canFulfill" class="warn">{{ t('branchMissing') }} {{ branch.missing.join(', ') }}</div>
            </div>
            <button type="button" class="link" @click="branchOpen = true">{{ t('change') }}</button>
          </div>

          <!-- Delivery: saved addresses or a new one -->
          <div v-if="deliveryType === 'Delivery'" class="delivery">
            <div class="label">{{ t('deliveryAddress') }}</div>
            <div v-if="addresses.length" class="saved">
              <label v-for="a in addresses" :key="a.id" class="opt" :class="{ on: addressId === a.id }">
                <input v-model="addressId" type="radio" :value="a.id" name="addr" />
                <span class="grow"><b>{{ a.address }}</b><small v-if="a.details">{{ a.details }}</small></span>
                <button type="button" class="icon" :aria-label="t('remove')" @click.prevent="forgetAddress(a)"><SIcon name="trash" :size="15" /></button>
              </label>
              <label class="opt" :class="{ on: addressId === 'new' }">
                <input v-model="addressId" type="radio" value="new" name="addr" />
                <span class="grow"><b>+ {{ t('newAddress') }}</b></span>
              </label>
            </div>
            <div v-if="addressId === 'new'" class="new">
              <input v-model="newAddress.address" class="s-input" :class="{ invalid: tried && missingAddress }" :placeholder="t('addressPh')" autocomplete="street-address" maxlength="200" />
              <input v-model="newAddress.details" class="s-input" :placeholder="t('addressDetailsPh')" maxlength="120" />
              <small v-if="tried && missingAddress" class="err">{{ t('needAddress') }}</small>
              <PinMap v-model="pin" :branches="quote.branches" />
            </div>
            <div v-if="branch" class="faint small by">{{ t('fulfilledBy') }}: <b>{{ branch.name }}</b></div>
          </div>
        </section>

        <!-- 2. Recipient -->
        <section class="card">
          <h2><span class="n">2</span> {{ t('recipient') }}</h2>
          <div class="recipient">
            <span class="ava">{{ (recipient.firstName[0] ?? '?') + (recipient.lastName[0] ?? '') }}</span>
            <div class="grow">
              <b>{{ recipientName || '—' }}</b>
              <div class="faint">+998 {{ formatPhone(recipient.phone) }}</div>
            </div>
            <button type="button" class="link" @click="editRecipient">{{ t('change') }}</button>
          </div>
        </section>

        <!-- 3. Payment -->
        <section class="card">
          <h2><span class="n">3</span> {{ t('payment') }}</h2>
          <label class="opt on pay">
            <input type="radio" checked name="pay" />
            <span class="cash">💵</span>
            <b class="grow">{{ t('cash') }}</b>
          </label>
        </section>

        <!-- 4. Comment -->
        <section class="card">
          <h2><span class="n">4</span> {{ t('comment') }}</h2>
          <textarea v-model="comment" class="s-input" rows="2" maxlength="500" :placeholder="t('commentPh')" />
        </section>
      </div>

      <!-- Order summary -->
      <aside class="summary">
        <h2>{{ t('yourOrder') }}</h2>
        <div class="thumbs">
          <div v-for="l in quote.lines" :key="l.productId + (l.variant ?? '')" class="th" :class="{ bad: !l.available }" :title="l.card?.name">
            <ProductImage v-if="l.card" :id="l.productId" :name="l.card.name" :src="l.card.image" />
            <span v-if="l.qty > 1" class="q">×{{ l.qty }}</span>
          </div>
        </div>
        <ul v-if="quote.lines.some(l => !l.available)" class="probs">
          <li v-for="l in quote.lines.filter(x => !x.available)" :key="l.productId">{{ l.card?.name ?? '#' + l.productId }} — {{ l.problem }}</li>
        </ul>

        <dl>
          <dt>{{ t('goods') }} ({{ quote.itemsCount }})</dt><dd>{{ price(quote.subtotal) }}</dd>
          <template v-if="quote.catalogDiscount"><dt>{{ t('discount') }}</dt><dd class="green">−{{ price(quote.catalogDiscount) }}</dd></template>
          <template v-if="quote.promo?.applied"><dt>{{ t('promo') }} {{ quote.promo.code }}</dt><dd class="green">−{{ price(quote.promo.discount) }}</dd></template>
          <template v-if="quote.deliveryType === 'Delivery'">
            <dt>{{ t('deliveryFee') }}</dt><dd :class="{ green: !quote.deliveryFee }">{{ quote.deliveryFee ? price(quote.deliveryFee) : t('free') }}</dd>
          </template>
        </dl>
        <div v-if="toFree" class="free-hint">{{ t('toFree') }}: <b>{{ price(toFree) }}</b></div>

        <div class="promo">
          <button v-if="!promoOpen && !promoCode" type="button" class="link" @click="promoOpen = true">🏷 {{ t('havePromo') }}</button>
          <div v-else-if="quote.promo?.applied" class="promo-ok">
            <SIcon name="check" :size="16" /> <b>{{ quote.promo.code }}</b>
            <button type="button" class="icon" :aria-label="t('remove')" @click="dropPromo"><SIcon name="close" :size="14" /></button>
          </div>
          <form v-else class="promo-form" @submit.prevent="applyPromo">
            <input v-model="promoInput" class="s-input" :class="{ invalid: promoProblem }" :placeholder="t('promo')" autocapitalize="characters" maxlength="30" />
            <button type="submit" class="s-btn ghost">{{ t('apply') }}</button>
          </form>
          <small v-if="promoProblem" class="err">{{ promoProblem }} <button type="button" class="link small" @click="dropPromo">{{ t('clear') }}</button></small>
        </div>

        <div class="total"><span>{{ t('total') }}</span><strong>{{ price(quote.total) }}</strong></div>
        <p v-for="p in blocking" :key="p" class="err">{{ p }}</p>
        <p v-if="placeError && !blocking.includes(placeError)" class="err">{{ placeError }}</p>
        <button type="button" class="s-btn place" :disabled="placing || !!blocking.length" @click="place">
          {{ placing ? t('placing') : t('placeOrder') }}
        </button>
        <div class="faint small center">💵 {{ t('cash') }}</div>
      </aside>
    </div>

    <div v-else class="layout">
      <div class="s-skel" style="height: 420px" />
      <div class="s-skel" style="height: 320px" />
    </div>

    <!-- Branch picker -->
    <SModal v-if="branchOpen && quote" :title="t('chooseBranch')" width="480px" @close="branchOpen = false">
      <div class="branches">
        <button v-for="b in quote.branches" :key="b.id" type="button" class="opt" :class="{ on: b.id === quote.branchId }"
          @click="prefs.branchId = b.id; branchOpen = false">
          <SIcon name="pin" :size="18" class="pin" />
          <span class="grow">
            <b>{{ b.name }}</b>
            <small>{{ b.address }}</small>
            <small v-if="b.canFulfill" class="ok">✓ {{ t('branchHasAll') }}</small>
            <small v-else class="warn">{{ t('branchMissing') }} {{ b.missing.join(', ') }}</small>
          </span>
        </button>
      </div>
    </SModal>

    <!-- Recipient editor -->
    <SModal v-if="recipientOpen" :title="t('recipientEdit')" @close="recipientOpen = false">
      <form id="rcp" class="form" novalidate @submit.prevent="saveRecipient">
        <label><span>{{ t('firstName') }}</span>
          <input v-model="draft.firstName" class="s-input" :class="{ invalid: draftTouched && !draft.firstName.trim() }" autocomplete="given-name" maxlength="40" />
        </label>
        <label><span>{{ t('lastName') }}</span><input v-model="draft.lastName" class="s-input" autocomplete="family-name" maxlength="40" /></label>
        <label><span>{{ t('phone') }}</span><PhoneInput v-model="draft.phone" :invalid="draftTouched && draft.phone.length !== 9" /></label>
      </form>
      <template #footer>
        <button type="button" class="s-btn ghost" @click="recipientOpen = false">{{ t('cancel') }}</button>
        <button type="submit" form="rcp" class="s-btn">{{ t('saveBtn') }}</button>
      </template>
    </SModal>
  </div>
</template>

<style scoped>
.crumbs { padding: 18px 0 6px; font-size: 14px; }
.crumbs a { display: inline-flex; align-items: center; gap: 2px; color: var(--ink-2); }
.crumbs a:hover { color: var(--blue); }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0 0 18px; }
.layout { display: grid; grid-template-columns: minmax(0, 1fr) 370px; gap: 24px; align-items: start; }
.steps { display: flex; flex-direction: column; gap: 14px; }
.card { background: var(--card); border-radius: 20px; padding: 20px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 14px; }
.card h2 { font-size: 18px; margin: 0; display: flex; align-items: center; gap: 10px; }
.n { width: 28px; height: 28px; border-radius: 50%; background: var(--blue-50); color: var(--blue); display: grid; place-items: center; font-size: 14px; }
.ways { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.ways button {
  display: flex; align-items: center; gap: 12px; text-align: left; padding: 14px; border-radius: 16px; border: 1.5px solid var(--line);
  background: var(--card); cursor: pointer; transition: border-color .15s, background .15s;
}
.ways button svg { color: var(--ink-3); flex: none; }
.ways button span { display: flex; flex-direction: column; gap: 2px; }
.ways small { color: var(--ink-3); font-size: 12.5px; }
.ways button.on { border-color: var(--blue); background: var(--blue-50); }
.ways button.on svg { color: var(--blue); }
.branch, .recipient { display: flex; align-items: center; gap: 12px; padding: 14px; border-radius: 16px; background: var(--page); }
.branch.bad { background: #fff4e5; }
.pin { color: var(--blue); flex: none; }
.grow { flex: 1; min-width: 0; }
.addr { color: var(--ink-2); font-size: 14px; }
.warn { color: #c77700; font-size: 13px; font-weight: 550; }
.faint { color: var(--ink-3); }
.small { font-size: 12.5px; }
.center { text-align: center; }
.link { border: 0; background: none; color: var(--blue); font-weight: 600; cursor: pointer; padding: 0; font-size: 14px; }
.link.small { font-size: 12.5px; }
.label { font-size: 14px; font-weight: 600; color: var(--ink-2); }
.delivery, .new { display: flex; flex-direction: column; gap: 10px; }
.saved { display: flex; flex-direction: column; gap: 8px; }
.opt { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border-radius: 14px; border: 1.5px solid var(--line); cursor: pointer; background: var(--card); text-align: left; width: 100%; }
.opt.on { border-color: var(--blue); background: var(--blue-50); }
.opt input { accent-color: var(--blue); width: 18px; height: 18px; flex: none; }
.opt .grow { display: flex; flex-direction: column; gap: 2px; }
.opt small { color: var(--ink-3); font-size: 12.5px; }
.opt small.ok { color: var(--green); font-weight: 600; }
.opt small.warn { color: #c77700; font-weight: 550; }
.icon { width: 30px; height: 30px; border: 0; border-radius: 8px; background: none; color: var(--ink-3); cursor: pointer; display: grid; place-items: center; }
.icon:hover { background: var(--line); color: var(--red); }
.by { margin-top: 2px; }
.ava { width: 44px; height: 44px; border-radius: 50%; background: var(--blue); color: #fff; display: grid; place-items: center; font-weight: 700; flex: none; }
.pay { cursor: default; }
.cash { font-size: 22px; }
.err { color: var(--red); font-size: 13px; margin: 0; }

.summary { position: sticky; top: 150px; background: var(--card); border-radius: 20px; padding: 20px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 12px; }
.summary h2 { font-size: 19px; margin: 0; }
.thumbs { display: flex; gap: 8px; flex-wrap: wrap; }
.th { position: relative; width: 52px; height: 52px; border-radius: 12px; overflow: hidden; }
.th :deep(.ph) { font-size: 26px; }
.th.bad { outline: 2px solid var(--red); opacity: .6; }
.q { position: absolute; right: 2px; bottom: 2px; background: var(--ink); color: #fff; font-size: 11px; font-weight: 700; padding: 0 5px; border-radius: 6px; }
.probs { margin: 0; padding-left: 18px; color: var(--red); font-size: 13px; }
dl { display: grid; grid-template-columns: 1fr auto; gap: 8px; margin: 0; font-size: 14.5px; }
dt { color: var(--ink-2); }
dd { margin: 0; text-align: right; white-space: nowrap; }
.green { color: var(--green); font-weight: 600; }
.free-hint { font-size: 13px; background: var(--green-50); color: #0d7a45; padding: 8px 12px; border-radius: 10px; }
.promo { display: flex; flex-direction: column; gap: 6px; padding-top: 4px; }
.promo-form { display: flex; gap: 8px; }
.promo-form .s-input { height: 44px; text-transform: uppercase; }
.promo-form .s-btn { height: 44px; }
.promo-ok { display: flex; align-items: center; gap: 6px; color: var(--green); background: var(--green-50); border-radius: 10px; padding: 6px 6px 6px 10px; }
.promo-ok b { flex: 1; }
.total { display: flex; justify-content: space-between; align-items: baseline; padding-top: 12px; border-top: 1px solid var(--line); }
.total strong { font-size: 26px; letter-spacing: -0.02em; }
.place { width: 100%; height: 54px; font-size: 16px; }
.branches { display: flex; flex-direction: column; gap: 8px; }
.form { display: flex; flex-direction: column; gap: 14px; }
.form label { display: flex; flex-direction: column; gap: 6px; font-size: 14px; font-weight: 550; color: var(--ink-2); }

@media (max-width: 960px) {
  .layout { grid-template-columns: 1fr; }
  .summary { position: static; }
}
@media (max-width: 640px) {
  h1 { font-size: 24px; }
  .ways { grid-template-columns: 1fr; }
  .card { padding: 16px; }
}
</style>
