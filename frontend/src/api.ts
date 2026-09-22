export type OrderStatus = 'New' | 'InProgress' | 'Overdue' | 'Ready' | 'OnTheWay' | 'Completed' | 'Cancelled'
export type Platform = 'Telegram' | 'Website' | 'Instagram'
export type PaymentMethod = 'Cash' | 'CardToCard' | 'Click' | 'Payme'
export type DeliveryType = 'Pickup' | 'Delivery'

export interface Paged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface Lookups {
  store: { storeName: string; botUsername: string; languages: string[]; overdueMinutes: number }
  branches: { id: number; name: string }[]
  employees: { id: number; name: string; role: string }[]
}

export interface OrderRow {
  id: number
  createdAt: string
  status: OrderStatus
  platform: Platform
  paymentMethod: PaymentMethod
  deliveryType: DeliveryType
  total: number
  customer: { id: number; fullName: string; phone: string }
  branch: string
  employee: string | null
  itemsCount: number
}

export interface OrdersPage extends Paged<OrderRow> {
  counts: Record<string, number>
}

export interface OrderDetail {
  id: number
  createdAt: string
  statusChangedAt: string
  status: OrderStatus
  platform: Platform
  paymentMethod: PaymentMethod
  deliveryType: DeliveryType
  subtotal: number
  deliveryCost: number
  total: number
  costTotal: number
  bonusEarned: number
  address: string | null
  comment: string | null
  recipientName: string | null
  recipientPhone: string | null
  promoCode: string | null
  promoDiscount: number
  cancelReason: string | null
  customer: { id: number; fullName: string; phone: string; username: string | null; language: string; bonusPoints: number }
  branch: { id: number; name: string; address: string }
  employee: { id: number; name: string } | null
  items: { productName: string; quantity: number; price: number; sum: number }[]
  notifications: { id: number; channel: string; language: string; text: string; sentAt: string }[]
}

export interface CustomerRow {
  id: number
  fullName: string
  username: string | null
  phone: string
  platform: Platform
  bonusPoints: number
  createdAt: string
  lastVisitAt: string
  language: string
  orders: number
  spent: number
}

export interface CustomerDetail extends Omit<CustomerRow, 'orders' | 'spent'> {
  stats: { orders: number; completed: number; spent: number; averageOrder: number; bonusEarned: number }
  orders: { id: number; createdAt: string; status: OrderStatus; total: number; platform: Platform; bonusEarned: number }[]
}

export interface RevenueStats { revenue: number; cost: number; delivery: number; profit: number }
export interface OrderCounts { total: number; new: number; completed: number; cancelled: number }
export interface CustomerStats { total: number; new: number; returning: number; averageOrder: number }

export interface Dashboard {
  period: { from: string; to: string; granularity: 'hour' | 'day' | 'month' }
  revenue: { current: RevenueStats; previous: RevenueStats }
  orders: { current: OrderCounts; previous: OrderCounts }
  customers: { current: CustomerStats; previous: CustomerStats; totalAllTime: number }
  revenueChart: { label: string; revenue: number; profit: number }[]
  ordersDynamics: { label: string; new: number; completed: number; cancelled: number }[]
  byPlatform: { platform: Platform; orders: number; revenue: number }[]
  trafficSources: { source: string; users: number }[]
  topProducts: { name: string; quantity: number; revenue: number }[]
  topCustomers: { id: number; name: string; phone: string; orders: number; total: number }[]
  map: {
    branches: { id: number; name: string; address: string; lat: number; lng: number }[]
    orders: { id: number; lat: number; lng: number; status: OrderStatus; total: number; branchId: number }[]
  }
}

export interface AutoReply { status: OrderStatus; language: string; enabled: boolean; text: string }
export interface BonusSettings { enabled: boolean; spendPerPoint: number }

type Query = Record<string, string | number | boolean | null | undefined>

export function qs(params: Query): string {
  const sp = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== '') sp.set(k, String(v))
  }
  const s = sp.toString()
  return s ? `?${s}` : ''
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: init?.body ? { 'Content-Type': 'application/json' } : undefined,
  })
  if (!res.ok) {
    const text = await res.text()
    let message = text
    try {
      message = JSON.parse(text).error ?? text
    } catch { /* plain-text error */ }
    throw new Error(message || `HTTP ${res.status}`)
  }
  return res.json() as Promise<T>
}

export const api = {
  lookups: () => request<Lookups>('/api/lookups'),
  dashboard: (q: Query) => request<Dashboard>(`/api/dashboard${qs(q)}`),

  orders: (q: Query) => request<OrdersPage>(`/api/orders${qs(q)}`),
  order: (id: number) => request<OrderDetail>(`/api/orders/${id}`),
  setStatus: (id: number, status: OrderStatus) =>
    request<OrderDetail>(`/api/orders/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
  setEmployee: (id: number, employeeId: number | null) =>
    request<OrderDetail>(`/api/orders/${id}/employee`, { method: 'PATCH', body: JSON.stringify({ employeeId }) }),
  simulateOrder: () => request<OrderDetail>('/api/orders/simulate', { method: 'POST' }),
  exportUrl: (q: Query) => `/api/orders/export${qs(q)}`,
  assemblyUrl: (q: Query) => `/api/orders/assembly-sheet${qs(q)}`,

  autoReplies: () => request<AutoReply[]>('/api/autoreplies'),
  saveAutoReplies: (items: AutoReply[]) =>
    request<AutoReply[]>('/api/autoreplies', { method: 'PUT', body: JSON.stringify(items) }),

  customers: (q: Query) => request<Paged<CustomerRow>>(`/api/customers${qs(q)}`),
  customer: (id: number) => request<CustomerDetail>(`/api/customers/${id}`),
  bonus: () => request<BonusSettings>('/api/settings/bonus'),
  saveBonus: (b: BonusSettings) =>
    request<BonusSettings>('/api/settings/bonus', { method: 'PUT', body: JSON.stringify(b) }),
}

/** Triggers a browser download for a GET endpoint that returns a file. */
export function download(url: string) {
  const a = document.createElement('a')
  a.href = url
  a.rel = 'noopener'
  document.body.appendChild(a)
  a.click()
  a.remove()
}

// ------------------------------------------------------------------ Catalog

/** Catalog languages: Russian, Uzbek Latin, Uzbek Cyrillic. */
export type Lang = 'ru' | 'uz' | 'oz'
export type Localized = Partial<Record<Lang, string>>
export type StockStatus = 'Unlimited' | 'Limited' | 'OutOfStock'

export interface CategoryRow {
  id: number
  parentId: number | null
  name: Localized
  imageUrl: string | null
  isActive: boolean
  createdAt: string
  sortOrder: number
  layout: string
  subcategoriesCount: number
  productsCount: number
}

export interface Category {
  id?: number
  parentId: number | null
  name: Localized
  description: Localized
  imageUrl: string | null
  bannerUrl: string | null
  layout: string
  productSort: string
  isActive: boolean
}

export interface ProductRow {
  id: number
  name: Localized
  price: number
  oldPrice: number | null
  isActive: boolean
  createdAt: string
  unit: string
  categoryId: number | null
  category: Localized | null
  branches: number
  stock: { status: StockStatus; quantity: number } | null
  imageUrl: string | null
  rating: number | null
  reviewsCount: number
}

export interface Media { url: string; type: 'image' | 'video' }
export interface Attribute { name: string; value: string }
export interface Variant { name: string; price: number | null; sku?: string | null }

export interface Product {
  id?: number
  categoryId: number | null
  name: Localized
  description: Localized
  price: number
  oldPrice: number | null
  costPrice: number
  unit: string
  weightGrams: number | null
  lengthCm: number | null
  widthCm: number | null
  heightCm: number | null
  tags: string[]
  attributes: Attribute[]
  variants: Variant[]
  media: Media[]
  isActive: boolean
  branchIds: number[]
  ikpu?: string | null
}

export interface ImportResult { created: number; updated: number; errors: string[] }
export interface ImportSettings { source: string | null; url: string | null; apiKey: string | null; autoSync: boolean }

export type DiscountType = 'Percent' | 'Fixed'
export interface DiscountRow {
  id: number
  name: string
  type: DiscountType
  value: number
  startsAt: string
  endsAt: string
  minOrderAmount: number | null
  isActive: boolean
  productIds: number[]
  branchIds: number[]
  products: string[]
  branches: string[]
  state: 'active' | 'scheduled' | 'expired' | 'disabled'
}
export interface DiscountInput {
  name: string
  type: DiscountType
  value: number
  productIds: number[]
  branchIds: number[]
  startsAt: string
  endsAt: string
  minOrderAmount: number | null
  isActive: boolean
}

export interface IkpuRow {
  id: number
  name: Localized
  ikpu: string | null
  packageCode: string | null
  unitCode: string | null
  unit: string
  category: Localized | null
  ikpuName: string | null
}
export interface IkpuRef { code: string; name: string; packageCode: string; packageName: string; unitCode: string; unitName: string }

export interface StockRow {
  id: number
  name: Localized
  costPrice: number
  price: number
  weightGrams: number | null
  unit: string
  isActive: boolean
  status: StockStatus
  quantity: number
  updatedAt: string
  imageUrl: string | null
  marginPercent: number
  velocity: number
}
export interface SalesHistory {
  id: number
  name: Localized
  totalQuantity: number
  totalRevenue: number
  firstSaleAt: string | null
  months: { month: string; quantity: number; revenue: number }[]
}

export interface AiResponse { translations: Partial<Record<Lang, Record<string, string>>>; provider: 'claude' | 'offline'; note: string | null }
export interface Upload { url: string; name: string; type: 'image' | 'video' | 'file'; size: number }

// ------------------------------------------------------------------ Chat

export type ChatChannel = 'Telegram' | 'Instagram' | 'Website' | 'Wolt'

export interface ConversationRow {
  id: number
  displayName: string
  handle: string | null
  channel: ChatChannel
  customerId: number | null
  phone: string | null
  reviewId: number | null
  unreadCount: number
  lastMessageAt: string
  lastMessageText: string
}

export interface ChatMessage {
  id: number
  direction: 'In' | 'Out'
  text: string
  attachmentUrl: string | null
  attachmentName: string | null
  attachmentType: 'image' | 'video' | 'file' | null
  isAuto: boolean
  senderName: string | null
  sentAt: string
}

export interface ConversationDetail {
  id: number
  displayName: string
  handle: string | null
  channel: ChatChannel
  reviewId: number | null
  customer: { id: number; fullName: string; phone: string; username: string | null; platform: Platform; language: string; bonusPoints: number; orders: number } | null
  review: { id: number; rating: number; status: 'New' | 'Answered'; productId: number; productName: Localized } | null
  messages: ChatMessage[]
}

export interface ChatSettings {
  chatInGroup: boolean
  chatWithBot: boolean
  autoReplyEnabled: boolean
  autoReplies: Record<ChatChannel, string>
}

async function send<T>(url: string, method: string, body?: unknown): Promise<T> {
  return request<T>(url, { method, body: body === undefined ? undefined : JSON.stringify(body) })
}

async function upload<T>(url: string, file: File): Promise<T> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(url, { method: 'POST', body: form })
  if (!res.ok) {
    const text = await res.text()
    let message = text
    try { message = JSON.parse(text).error ?? text } catch { /* plain text */ }
    throw new Error(message || `HTTP ${res.status}`)
  }
  return res.json() as Promise<T>
}

export const catalogApi = {
  categories: (branchId?: number | '') => request<CategoryRow[]>(`/api/categories${qs({ branchId })}`),
  category: (id: number) => request<Category & { id: number }>(`/api/categories/${id}`),
  saveCategory: (c: Category) => (c.id ? send<Category>(`/api/categories/${c.id}`, 'PUT', c) : send<Category>('/api/categories', 'POST', c)),
  deleteCategory: (id: number) => fetch(`/api/categories/${id}`, { method: 'DELETE' }).then(async r => {
    if (!r.ok) throw new Error((await r.json().catch(() => ({}))).error ?? `HTTP ${r.status}`)
  }),

  products: (q: Query) => request<Paged<ProductRow>>(`/api/products${qs(q)}`),
  product: (id: number) => request<Product & { id: number }>(`/api/products/${id}`),
  saveProduct: (p: Product) => (p.id ? send<Product>(`/api/products/${p.id}`, 'PUT', p) : send<Product>('/api/products', 'POST', p)),
  setProductActive: (id: number, isActive: boolean) => send(`/api/products/${id}/active`, 'PATCH', { isActive }),
  deleteProduct: (id: number) => fetch(`/api/products/${id}`, { method: 'DELETE' }),
  importTemplateUrl: '/api/products/import/template',
  importProducts: (file: File) => upload<ImportResult>('/api/products/import', file),
  importSettings: () => request<ImportSettings>('/api/products/import/settings'),
  saveImportSettings: (s: ImportSettings) => send<ImportSettings>('/api/products/import/settings', 'PUT', s),

  discounts: (branchId?: number | '') => request<DiscountRow[]>(`/api/discounts${qs({ branchId })}`),
  saveDiscount: (id: number | null, d: DiscountInput) => (id ? send(`/api/discounts/${id}`, 'PUT', d) : send('/api/discounts', 'POST', d)),
  deleteDiscount: (id: number) => fetch(`/api/discounts/${id}`, { method: 'DELETE' }),

  ikpu: (q: Query) => request<{ items: IkpuRow[]; missing: number; reference: IkpuRef[] }>(`/api/ikpu${qs(q)}`),
  saveIkpu: (id: number, v: { ikpu: string | null; packageCode: string | null; unitCode: string | null }) =>
    send<{ ikpu: string | null; packageCode: string | null; unitCode: string | null; ikpuName: string | null }>(`/api/ikpu/${id}`, 'PUT', v),
  suggestIkpu: (id: number) => send<{ ikpu: string; packageCode: string; unitCode: string; ikpuName: string }>(`/api/ikpu/${id}/suggest`, 'POST'),
  generateMissingIkpu: () => send<{ filled: number; remaining: number }>('/api/ikpu/generate-missing', 'POST'),

  stock: (q: Query) => request<{ velocityDays: number; items: StockRow[] }>(`/api/stock${qs(q)}`),
  patchStock: (productId: number, branchId: number, patch: Partial<Pick<StockRow, 'costPrice' | 'price' | 'weightGrams' | 'status' | 'quantity'>>) =>
    send<Partial<StockRow>>(`/api/stock/${productId}${qs({ branchId })}`, 'PATCH', patch),
  salesHistory: (productId: number, branchId?: number | '') => request<SalesHistory>(`/api/stock/${productId}/history${qs({ branchId })}`),

  upload: (file: File) => upload<Upload>('/api/uploads', file),
  aiStatus: () => request<{ provider: 'claude' | 'offline' }>('/api/ai/status'),
  translate: (source: Lang, fields: Record<string, string>) => send<AiResponse>('/api/ai/translate', 'POST', { source, fields }),
  describe: (body: { name: string; nameUz?: string; kind: 'product' | 'category'; category?: string; attributes?: Attribute[]; unit?: string; weightGrams?: number | null; existing?: string }) =>
    send<AiResponse>('/api/ai/describe', 'POST', body),
}

export const chatApi = {
  conversations: (q: Query) => request<{ items: ConversationRow[]; counts: { unread: number; reviews: number } }>(`/api/chat/conversations${qs(q)}`),
  conversation: (id: number) => request<ConversationDetail>(`/api/chat/conversations/${id}`),
  send: (id: number, body: { text: string; attachmentUrl?: string; attachmentName?: string; attachmentType?: string; senderName?: string }) =>
    send<ConversationDetail>(`/api/chat/conversations/${id}/messages`, 'POST', body),
  forCustomer: (customerId: number) => send<{ id: number }>(`/api/chat/for-customer/${customerId}`, 'POST'),
  simulate: () => send<{ id: number; displayName: string }>('/api/chat/simulate', 'POST'),
  unread: () => request<{ count: number }>('/api/chat/unread'),
  settings: () => request<ChatSettings>('/api/chat/settings'),
  saveSettings: (s: ChatSettings) => send<ChatSettings>('/api/chat/settings', 'PUT', s),
}

// ------------------------------------------------------------------ Marketing

export interface AudienceFilter {
  platforms?: Platform[]
  languages?: string[]
  minOrders?: number | null
  lastVisitDays?: number | null
  minBonus?: number | null
  customerIds?: number[]
}

export interface BroadcastRow {
  id: number
  name: string
  status: 'Scheduled' | 'Sent'
  sendAt: string
  imageUrl: string | null
  total: number
  sent: number
  notSent: number
  blocked: number
  clicks: number
}

export interface BroadcastDetail {
  id: number
  name: string
  text: string
  imageUrl: string | null
  buttonText: string | null
  buttonUrl: string | null
  status: 'Scheduled' | 'Sent'
  sendAt: string
  createdAt: string
  recipients: { status: 'Sent' | 'NotSent' | 'Blocked'; clickedAt: string | null; token: string; customerId: number; fullName: string; phone: string; platform: Platform }[]
}

export interface PromoRow {
  id: number
  code: string
  type: DiscountType
  value: number
  maxDiscount: number | null
  usageLimit: number | null
  usedCount: number
  minOrderAmount: number | null
  startsAt: string
  endsAt: string
  firstOrderOnly: boolean
  categoryIds: number[]
  platforms: Platform[]
  isActive: boolean
  createdAt: string
  state: 'active' | 'scheduled' | 'expired' | 'exhausted' | 'disabled'
}
export type PromoInput = Omit<PromoRow, 'id' | 'usedCount' | 'createdAt' | 'state'>

export interface SourceRow {
  id: number
  type: 'Telegram' | 'Website'
  name: string
  slug: string
  clicks: number
  newUsers: number
  existingUsers: number
  orders: number
  createdAt: string
  lastVisitAt: string | null
  link: string
  trackedLink: string
  conversion: number
}

export type SmsStatus = 'Moderation' | 'InProgress' | 'Confirmed' | 'Rejected'
export interface SmsCampaignRow {
  id: number
  name: string
  status: SmsStatus
  rejectReason: string | null
  recipients: number
  delivered: number
  segments: number
  createdAt: string
  statusChangedAt: string
  template: string
  text: string
}
export interface SmsTemplateRow {
  id: number
  name: string
  text: string
  status: SmsStatus
  rejectReason: string | null
  createdAt: string
  moderatedAt: string | null
  segments: number
  unicode: boolean
}

export interface ChannelPost { id: number; channel: string; imageUrl: string | null; text: string; buttonText: string | null; buttonUrl: string | null; publishedAt: string }
export interface ChannelState { botUsername: string; channel: string | null; connectedAt: string | null; storeDomain: string; posts: ChannelPost[] }

export type BannerType = 'Main' | 'Category'
export type BannerLinkType = 'None' | 'Category' | 'Product' | 'Url'
export interface Banner {
  id?: number
  title: string
  type: BannerType
  categoryId: number | null
  mobileUrl: string | null
  mobileMediaType: 'image' | 'video'
  desktopUrl: string | null
  desktopMediaType: 'image' | 'video'
  linkType: BannerLinkType
  linkTargetId: number | null
  linkUrl: string | null
  isActive: boolean
}
export interface BannerRow extends Required<Pick<Banner, 'id'>>, Omit<Banner, 'id'> {
  sortOrder: number
  createdAt: string
  category: string | null
  linkLabel: string | null
}

export interface ReviewRow {
  id: number
  rating: number
  status: 'New' | 'Answered'
  comment: string
  reply: string | null
  createdAt: string
  repliedAt: string | null
  productId: number
  productName: Localized
  customer: string
  customerId: number
  conversationId: number | null
}

export const marketingApi = {
  broadcasts: () => request<BroadcastRow[]>('/api/marketing/broadcasts'),
  broadcast: (id: number) => request<BroadcastDetail>(`/api/marketing/broadcasts/${id}`),
  audience: (f: AudienceFilter) =>
    send<{ total: number; reachable: number; sample: { id: number; fullName: string; platform: Platform }[] }>('/api/marketing/broadcasts/audience', 'POST', f),
  createBroadcast: (b: { name: string; text: string; imageUrl: string | null; buttonText: string | null; buttonUrl: string | null; sendAt: string | null; audience: AudienceFilter }) =>
    send<{ id: number }>('/api/marketing/broadcasts', 'POST', b),
  deleteBroadcast: (id: number) => fetch(`/api/marketing/broadcasts/${id}`, { method: 'DELETE' }),

  promos: () => request<PromoRow[]>('/api/marketing/promocodes'),
  savePromo: (id: number | null, p: PromoInput) =>
    id ? send(`/api/marketing/promocodes/${id}`, 'PUT', p) : send('/api/marketing/promocodes', 'POST', p),
  deletePromo: (id: number) => fetch(`/api/marketing/promocodes/${id}`, { method: 'DELETE' }),
  generatePromo: () => request<{ code: string }>('/api/marketing/promocodes/generate'),
  checkPromo: (q: Query) => request<{ valid: boolean; error: string | null; discount: number }>(`/api/marketing/promocodes/check${qs(q)}`),

  sources: () => request<SourceRow[]>('/api/marketing/sources'),
  createSource: (s: { type: 'Telegram' | 'Website'; name: string }) => send<{ id: number }>('/api/marketing/sources', 'POST', s),
  renameSource: (id: number, s: { type: 'Telegram' | 'Website'; name: string }) => send(`/api/marketing/sources/${id}`, 'PUT', s),
  deleteSource: (id: number) => fetch(`/api/marketing/sources/${id}`, { method: 'DELETE' }),

  smsCampaigns: (status?: SmsStatus | '') =>
    request<{ items: SmsCampaignRow[]; counts: Partial<Record<SmsStatus, number>> }>(`/api/marketing/sms/campaigns${qs({ status })}`),
  smsTemplates: () => request<SmsTemplateRow[]>('/api/marketing/sms/templates'),
  createSmsTemplate: (t: { name: string; text: string }) =>
    send<{ id: number; status: SmsStatus; rejectReason: string | null }>('/api/marketing/sms/templates', 'POST', t),
  deleteSmsTemplate: (id: number) => fetch(`/api/marketing/sms/templates/${id}`, { method: 'DELETE' }).then(async r => {
    if (!r.ok) throw new Error((await r.json().catch(() => ({}))).error ?? `HTTP ${r.status}`)
  }),
  smsSegments: (text: string) =>
    send<{ segments: number; length: number; unicode: boolean; max: number; problem: string | null }>('/api/marketing/sms/segments', 'POST', { name: '', text }),
  createSmsCampaign: (c: { name: string; templateId: number; audience: AudienceFilter }) => send<{ id: number }>('/api/marketing/sms/campaigns', 'POST', c),

  channel: () => request<ChannelState>('/api/marketing/channel'),
  connectChannel: (channel: string) => send<{ channel: string; connectedAt: string }>('/api/marketing/channel/connect', 'POST', { channel }),
  disconnectChannel: () => fetch('/api/marketing/channel/connect', { method: 'DELETE' }),
  publishPost: (p: { text: string; imageUrl: string | null; buttonText: string | null; buttonUrl: string | null }) =>
    send<ChannelPost>('/api/marketing/channel/posts', 'POST', p),

  banners: () => request<BannerRow[]>('/api/marketing/banners'),
  banner: (id: number) => request<Banner & { id: number }>(`/api/marketing/banners/${id}`),
  saveBanner: (b: Banner) => (b.id ? send(`/api/marketing/banners/${b.id}`, 'PUT', b) : send('/api/marketing/banners', 'POST', b)),
  setBannerActive: (id: number, isActive: boolean) => send(`/api/marketing/banners/${id}/active`, 'PATCH', { isActive }),
  moveBanner: (id: number, direction: number) => fetch(`/api/marketing/banners/${id}/move`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ direction }),
  }),
  deleteBanner: (id: number) => fetch(`/api/marketing/banners/${id}`, { method: 'DELETE' }),

  reviews: (q: Query) =>
    request<Paged<ReviewRow> & { counts: Partial<Record<'New' | 'Answered', number>>; average: number }>(`/api/marketing/reviews${qs(q)}`),
  replyReview: (id: number, text: string) => send<{ status: string; reply: string; repliedAt: string }>(`/api/marketing/reviews/${id}/reply`, 'POST', { text }),
}
