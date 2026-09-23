import { createApp } from 'vue'
import 'leaflet/dist/leaflet.css'
import './styles.css'
import App from './App.vue'
import { router } from './router'
import { setSignOutHandler } from './auth'

// A rejected session (expired or signed out elsewhere) sends the panel back to the login page.
setSignOutHandler(() => {
  const next = router.currentRoute.value.fullPath
  router.replace({ path: '/login', query: next.startsWith('/login') ? {} : { next } })
})

createApp(App).use(router).mount('#app')
