import { qs } from '../api'
import { lang } from './i18n'
import { authToken, signedOut, type Me } from './state/auth'
import { storeSlug } from './state/store'

export interface Card {
  id: number
  categoryId: number | null
  name: string
  image: string | null
  price: number
  oldPrice: number | null
  discountPercent: number | null
  rating: number
  reviewsCount: number
  unit: string
  weightGrams: number | null
  tags: string[]
  inStock: boolean
  /** How many can be bought; null = no limit. */
  maxQty: number | null
  /** Variant names; the first is the default. */
  variants: string[]
}

export interface CategoryNode {
  id: number
  parentId: number | null
  name: string
  image: string | null
  productsCount: number
  children: CategoryNode[]
}

export interface Home {
  store: { name: string }
  banners: { id: number; title: string; desktopUrl: string | null; mobileUrl: string | null; desktopMediaType: string; mobileMediaType: string; link: string | null }[]
  categories: CategoryNode[]
  deals: Card[]
  popular: Card[]
  sections: { category: CategoryNode; total: number; products: Card[] }[]
}

export interface Meta {
  store: { name: string }
  branches: { id: number; name: string; address: string }[]
  categories: CategoryNode[]
}

export interface Page<T> { total: number; page: number; pageSize: number; items: T[] }

export type Sort = 'manual' | 'relevance' | 'popular' | 'price_asc' | 'price_desc' | 'new' | 'rating'

export interface CatalogPage extends Page<Card> {
  sort: Sort
  category: null | {
    id: number
    parentId: number | null
    name: string
    description: string
    imageUrl: string | null
    bannerUrl: string | null
    trail: { id: number; name: string }[]
    children: CategoryNode[]
    siblings: CategoryNode[]
  }
}

export interface SearchPage extends Page<Card> {
  query: string
  categories: { id: number; name: string; imageUrl: string | null; path: string }[]
  facets: { id: number; name: string; count: number }[]
}

export interface Suggest {
  categories: { id: number; name: string; path: string }[]
  products: Card[]
  total: number
}

export interface ProductPage {
  card: Card
  trail: { id: number; name: string }[]
  media: { url: string; type: string }[]
  description: string
  shortDescription: string
  variants: { name: string; price: number; oldPrice: number | null; discountPercent: number | null }[]
  attributes: { name: string; value: string }[]
  unit: string
  weightGrams: number | null
  lengthCm: number | null
  widthCm: number | null
  heightCm: number | null
  tags: string[]
  stock: {
    unlimited: boolean
    quantity: number
    branches: { id: number; name: string; address: string; status: 'Unlimited' | 'Limited' | 'OutOfStock' | 'None'; quantity: number }[]
  }
  seller: { name: string; rating: number; reviewsCount: number; productsCount: number; branchesCount: number; since: string | null }
}

export interface ReviewsPage {
  average: number
  count: number
  breakdown: { star: number; count: number }[]
  total: number
  page: number
  items: { id: number; rating: number; comment: string; createdAt: string; reply: string | null; repliedAt: string | null; author: string }[]
}

export interface Quote {
  lines: {
    productId: number
    variant: string | null
    card: Card | null
    available: boolean
    qty: number
    maxQty: number | null
    price: number
    oldPrice: number | null
    sum: number
    oldSum: number
  }[]
  count: number
  subtotal: number
  discount: number
  total: number
}

export interface ChatThread {
  id: number
  name: string
  store: string
  messages: { id: number; mine: boolean; text: string; attachmentUrl: string | null; attachmentType: string | null; isAuto: boolean; sentAt: string }[]
}

export type DeliveryType = 'Pickup' | 'Delivery'
export type OrderStatus = 'New' | 'Assembling' | 'Ready' | 'HandedToCourier' | 'OnTheWay' | 'Delivered' | 'Completed' | 'Cancelled'

export interface SavedAddress { id: number; address: string; details: string | null; lat: number | null; lng: number | null; lastUsedAt: string }

export interface CheckoutRequest {
  lines: { productId: number; variant: string | null; qty: number }[]
  deliveryType: DeliveryType
  branchId?: number | null
  lat?: number | null
  lng?: number | null
  promoCode?: string | null
}

export interface CheckoutQuote {
  lines: { productId: number; variant: string | null; card: Card | null; qty: number; price: number; oldPrice: number | null; sum: number; oldSum: number; available: boolean; problem: string | null }[]
  branches: { id: number; name: string; address: string; lat: number; lng: number; canFulfill: boolean; missing: string[] }[]
  branchId: number
  deliveryType: DeliveryType
  itemsCount: number
  subtotal: number
  catalogDiscount: number
  itemsTotal: number
  promo: { code: string; applied: boolean; error: string | null; discount: number } | null
  deliveryFee: number
  deliveryFeeBase: number
  freeDeliveryFrom: number | null
  total: number
  problems: string[]
  canPlace: boolean
}

export interface PlaceOrder {
  order: CheckoutRequest
  recipientName: string
  recipientPhone: string
  address?: string | null
  addressDetails?: string | null
  comment?: string | null
  paymentMethod: 'Cash'
}

export interface ShopOrder {
  id: number
  status: OrderStatus
  createdAt: string
  statusChangedAt: string
  deliveryType: DeliveryType
  paymentMethod: string
  branch: { id: number; name: string; address: string }
  address: string | null
  comment: string | null
  recipientName: string
  recipientPhone: string
  itemsCount: number
  subtotal: number
  promoCode: string | null
  promoDiscount: number
  deliveryCost: number
  total: number
  canCancel: boolean
  cancelReason: string | null
  /** When each step happened. */
  history: { status: OrderStatus; at: string }[]
  /** This order's flow (pickup skips the courier steps). */
  steps: OrderStatus[]
  items: { productId: number; name: string; variant: string | null; qty: number; price: number; sum: number; image: string | null }[]
}

export interface OrdersPage { activeCount: number; allCount: number; total: number; page: number; items: ShopOrder[] }

export interface ProfileUpdate {
  firstName: string; lastName: string; phone: string
  email: string | null; country: string | null; birthDate: string | null; gender: 'male' | 'female' | null
}

export interface StoreInfo {
  store: { name: string; phone: string | null; workingHours: string | null; about: string | null; bot: string }
  branches: { id: number; name: string; address: string; phone: string | null; workingHours: string | null; lat: number; lng: number }[]
  delivery: { fee: number; freeFrom: number | null; terms: string | null }
  returns: { terms: string | null }
}

export interface ReviewProduct { id: number; name: string; image: string | null; available: boolean }
export interface MyReviews {
  pending: { product: ReviewProduct; orderId: number; receivedAt: string }[]
  rated: { id: number; product: ReviewProduct; rating: number; comment: string; createdAt: string; reply: string | null; repliedAt: string | null }[]
}

type Params = Record<string, string | number | boolean | null | undefined>

/** Error from the shop API; `data` carries the JSON body (e.g. a fresh checkout quote when an order is refused). */
export class ApiError extends Error {
  status: number
  data: unknown
  constructor(message: string, status: number, data: unknown) {
    super(message)
    this.status = status
    this.data = data
  }
}

async function send<T>(method: string, path: string, params: Params = {}, body?: unknown, signal?: AbortSignal): Promise<T> {
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (authToken.value) headers.Authorization = `Bearer ${authToken.value}`
  // Which shop this storefront belongs to; the server answers with that store's catalog, prices and orders.
  if (storeSlug.value) headers['X-Store'] = storeSlug.value
  const res = await fetch(`/api/shop/${path}${qs({ ...params, lang: lang.value })}`, {
    method, headers, signal, body: body === undefined ? undefined : JSON.stringify(body),
  })
  // Session expired or revoked: forget it; the page will ask to sign in again.
  if (res.status === 401 && authToken.value) signedOut()
  // The shop this browser remembers is gone (deleted, or the prototype database was reset).
  if (res.status === 404 && storeSlug.value) {
    const body = await res.clone().json().catch(() => null) as { code?: string } | null
    if (body?.code === 'store_not_found') {
      storeSlug.value = ''
      window.location.assign('/shops')
    }
  }
  if (res.status === 204) return undefined as T
  let data: unknown = null
  try { data = await res.json() } catch { /* empty body */ }
  if (!res.ok) throw new ApiError((data as { error?: string } | null)?.error ?? `HTTP ${res.status}`, res.status, data)
  return data as T
}
const get = <T>(path: string, params: Params = {}, signal?: AbortSignal) => send<T>('GET', path, params, undefined, signal)
const post = <T>(path: string, body: unknown) => send<T>('POST', path, {}, body)

export const shopApi = {
  home: () => get<Home>('home'),
  meta: () => get<Meta>('meta'),
  catalog: (p: { categoryId?: number; sort?: Sort; inStockOnly?: boolean; page?: number }) => get<CatalogPage>('catalog', p),
  search: (p: { q: string; categoryId?: number; sort?: Sort; page?: number }) => get<SearchPage>('search', p),
  suggest: (q: string, signal?: AbortSignal) => get<Suggest>('suggest', { q }, signal),
  product: (id: number) => get<ProductPage>(`products/${id}`),
  reviews: (id: number, p: { sort?: string; rating?: number; page?: number }) => get<ReviewsPage>(`products/${id}/reviews`, p),
  recommended: (id: number) => get<Card[]>(`products/${id}/recommended`),
  cards: (ids: number[]) => (ids.length ? get<Card[]>('products', { ids: ids.join(',') }) : Promise.resolve([])),
  quote: (lines: { productId: number; variant: string | null; qty: number }[]) => post<Quote>('cart/quote', { lines }),
  login: (phone: string, name: string, chatToken?: string) => post<{ token: string; me: Me }>('account/login', { phone, name, chatToken, lang: lang.value }),
  logout: () => post<void>('account/logout', {}),
  me: () => get<Me>('account/me'),
  updateMe: (body: ProfileUpdate) => send<Me>('PUT', 'account/me', {}, body),
  updateSettings: (body: { language: string; notifyOrders: boolean; notifyPromos: boolean }) => send<Me>('PUT', 'account/settings', {}, body),
  info: () => get<StoreInfo>('info'),
  myReviews: () => get<MyReviews>('account/reviews'),
  createReview: (productId: number, rating: number, comment: string) => post<{ id: number }>('account/reviews', { productId, rating, comment }),
  updateReview: (id: number, productId: number, rating: number, comment: string) => send<{ id: number }>('PUT', `account/reviews/${id}`, {}, { productId, rating, comment }),
  addresses: () => get<SavedAddress[]>('account/addresses'),
  deleteAddress: (id: number) => send<void>('DELETE', `account/addresses/${id}`),
  checkoutQuote: (req: CheckoutRequest) => post<CheckoutQuote>('checkout/quote', req),
  placeOrder: (body: PlaceOrder) => post<ShopOrder>('orders', body),
  orders: (scope: 'active' | 'all', page = 1) => get<OrdersPage>('orders', { scope, page }),
  order: (id: number) => get<ShopOrder>(`orders/${id}`),
  cancelOrder: (id: number, reason?: string) => post<ShopOrder>(`orders/${id}/cancel`, { reason }),
  chatOpen: (token: string, name?: string) => post<ChatThread>('chat/open', { token, name }),
  chatGet: (token: string) => get<ChatThread>(`chat/${token}`),
  chatSend: (token: string, text: string, ctx: { productId?: number; orderId?: number } = {}) => post<ChatThread>(`chat/${token}/messages`, { text, ...ctx }),
}
