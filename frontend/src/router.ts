import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from './views/DashboardView.vue'
import OrdersView from './views/OrdersView.vue'
import CustomersView from './views/CustomersView.vue'
import { admin, adminToken, restore } from './auth'
import { openStore, resolveStore } from './shop/state/store'

export const router = createRouter({
  history: createWebHistory(),
  // Storefront pages start at the top; opening/closing the quick view (?product=) keeps the scroll position.
  scrollBehavior(to, from, saved) {
    if (saved) return saved
    if (to.path === from.path) return false
    return { top: 0 }
  },
  routes: [
    // ---- Вход и регистрация администратора ----
    { path: '/login', component: () => import('./views/auth/AuthView.vue'), props: { mode: 'login' }, meta: { open: true } },
    { path: '/register', component: () => import('./views/auth/AuthView.vue'), props: { mode: 'register' }, meta: { open: true } },

    // ---- A shop is opened by its own address: /shop/{slug} here, its subdomain in production ----
    { path: '/shop/:slug', redirect: to => { openStore(String(to.params.slug)); return '/' } },

    // ---- Customer storefront ----
    {
      path: '/',
      component: () => import('./shop/ShopLayout.vue'),
      meta: { shop: true },
      children: [
        { path: '', component: () => import('./shop/views/HomeView.vue') },
        { path: 'catalog/:id(\\d+)?', component: () => import('./shop/views/CatalogView.vue') },
        { path: 'search', component: () => import('./shop/views/SearchView.vue') },
        { path: 'product/:id(\\d+)', component: () => import('./shop/views/ProductView.vue') },
        { path: 'cart', component: () => import('./shop/views/CartView.vue') },
        { path: 'favorites', component: () => import('./shop/views/FavoritesView.vue') },
        { path: 'checkout', component: () => import('./shop/views/CheckoutView.vue') },
        { path: 'profile', component: () => import('./shop/views/ProfileView.vue') },
        { path: 'profile/orders', component: () => import('./shop/views/OrdersView.vue') },
        { path: 'profile/orders/:id(\\d+)', component: () => import('./shop/views/OrderView.vue') },
        { path: 'profile/edit', component: () => import('./shop/views/EditProfileView.vue') },
        { path: 'profile/reviews', component: () => import('./shop/views/MyReviewsView.vue') },
        { path: 'profile/settings', component: () => import('./shop/views/SettingsView.vue') },
        { path: 'about', component: () => import('./shop/views/AboutView.vue') },
        { path: 'delivery-terms', component: () => import('./shop/views/TermsView.vue'), props: { kind: 'delivery' } },
        { path: 'returns', component: () => import('./shop/views/TermsView.vue'), props: { kind: 'returns' } },
        { path: 'contact', component: () => import('./shop/views/ContactView.vue') },
      ],
    },

    // ---- Merchant admin ----
    { path: '/dashboard', component: DashboardView },
    { path: '/orders', component: OrdersView },
    { path: '/customers', component: CustomersView },
    { path: '/chat/:id(\\d+)?', component: () => import('./views/ChatView.vue') },

    { path: '/products', redirect: '/products/items' },
    { path: '/products/categories', component: () => import('./views/catalog/CategoriesView.vue') },
    { path: '/products/categories/new', component: () => import('./views/catalog/CategoryFormView.vue') },
    { path: '/products/categories/:id(\\d+)', component: () => import('./views/catalog/CategoryFormView.vue'), props: true },
    { path: '/products/items', component: () => import('./views/catalog/ProductsView.vue') },
    { path: '/products/items/new', component: () => import('./views/catalog/ProductFormView.vue') },
    { path: '/products/items/:id(\\d+)', component: () => import('./views/catalog/ProductFormView.vue'), props: true },
    { path: '/products/discounts', component: () => import('./views/catalog/DiscountsView.vue') },
    { path: '/products/stock', component: () => import('./views/catalog/StockView.vue') },

    { path: '/marketing', redirect: '/marketing/promocodes' },
    { path: '/marketing/promocodes', component: () => import('./views/marketing/PromoCodesView.vue') },
    { path: '/marketing/banners', component: () => import('./views/marketing/BannersView.vue') },
    { path: '/marketing/banners/new', component: () => import('./views/marketing/BannerFormView.vue') },
    { path: '/marketing/banners/:id(\\d+)', component: () => import('./views/marketing/BannerFormView.vue'), props: true },
    { path: '/marketing/reviews', component: () => import('./views/marketing/ReviewsView.vue') },

    { path: '/store', component: () => import('./views/StoreView.vue') },

    // Unknown paths (e.g. removed admin sections) go to the storefront home.
    { path: '/:section(.*)', redirect: '/' },
  ],
})

// The storefront needs a shop to show; the admin panel needs a signed-in administrator (spec: "каждый
// администратор видит и управляет только своим магазином").
router.beforeEach(async to => {
  // Which shop the visitor opened: remembered from its address, or asked of the server once. An address that
  // belongs to no shop is not a shopper's page at all, so it goes to the merchant's sign-in.
  if (to.matched[0]?.meta.shop) return (await resolveStore()) ? true : '/login'
  if (to.meta.open) return !adminToken.value ? true : '/dashboard'
  if (!adminToken.value) return { path: '/login', query: { next: to.fullPath } }
  // After a reload only the token is known: fetch the administrator before showing the panel.
  if (!admin.value && !(await restore())) return { path: '/login', query: { next: to.fullPath } }
  return true
})
