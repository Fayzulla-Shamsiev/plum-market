import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// Dev: Vite on :5174 proxies /api to the ASP.NET Core API on :5090.
// Build: output goes straight into the API's wwwroot so `dotnet run` serves the whole app.
// API_URL lets a second dev server point at another backend instance.
const api = process.env.API_URL ?? 'http://localhost:5090'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5174,
    // /r/ and /s/ are the public click-tracking redirects (broadcast buttons, traffic sources).
    proxy: { '/api': api, '/uploads': api, '/r/': api, '/s/': api },
  },
  build: {
    outDir: '../backend/PlumMarket.Api/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1200,
  },
})
