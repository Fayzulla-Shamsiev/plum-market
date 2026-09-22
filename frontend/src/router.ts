import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from './views/DashboardView.vue'
import OrdersView from './views/OrdersView.vue'
import CustomersView from './views/CustomersView.vue'
import ComingSoonView from './views/ComingSoonView.vue'

export const router = createRouter({
  history: createWebHistory(),
  // Storefront pages start at the top; opening/closing the quick view (?product=) keeps the scroll position.
  scrollBehavior(to, from, saved) {
    if (saved) return saved
    if (to.path === from.path) return false
    return { top: 0 }
  },
  routes: [
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
    { path: '/products/ikpu', component: () => import('./views/catalog/IkpuView.vue') },
    { path: '/products/stock', component: () => import('./views/catalog/StockView.vue') },

    { path: '/marketing', redirect: '/marketing/broadcasts' },
    { path: '/marketing/broadcasts', component: () => import('./views/marketing/BroadcastsView.vue') },
    { path: '/marketing/broadcasts/new', component: () => import('./views/marketing/BroadcastNewView.vue') },
    { path: '/marketing/promocodes', component: () => import('./views/marketing/PromoCodesView.vue') },
    { path: '/marketing/sources', component: () => import('./views/marketing/SourcesView.vue') },
    { path: '/marketing/sms', component: () => import('./views/marketing/SmsView.vue') },
    { path: '/marketing/channel-post', component: () => import('./views/marketing/ChannelPostView.vue') },
    { path: '/marketing/banners', component: () => import('./views/marketing/BannersView.vue') },
    { path: '/marketing/banners/new', component: () => import('./views/marketing/BannerFormView.vue') },
    { path: '/marketing/banners/:id(\\d+)', component: () => import('./views/marketing/BannerFormView.vue'), props: true },
    { path: '/marketing/reviews', component: () => import('./views/marketing/ReviewsView.vue') },

    // Everything else from the spec is stubbed until the next iterations.
    { path: '/:section(.*)', component: ComingSoonView },
  ],
})
