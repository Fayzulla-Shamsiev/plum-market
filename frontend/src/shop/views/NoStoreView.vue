<script setup lang="ts">
// Reached when the address doesn't belong to a shop — a deleted store, a stale link, or the bare platform
// host. There is no list of shops to offer: a shop is reached by its own address (its subdomain in production,
// /shop/{slug} on this prototype host), and everything else belongs to the merchant's panel.
</script>

<template>
  <div class="empty-shop">
    <div class="mark">
      <svg viewBox="0 0 40 40" width="34" height="34" aria-hidden="true">
        <rect width="40" height="40" rx="12" fill="#1f7aec" />
        <path d="M14 29V12.5h7.2a6.2 6.2 0 0 1 0 12.4H14" fill="none" stroke="#fff" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round" />
        <circle cx="28.5" cy="28.5" r="3.2" fill="#35d07f" />
      </svg>
      <span>Plum <b>Market</b></span>
    </div>
    <h1>Магазин не найден</h1>
    <p>
      Проверьте адрес — возможно, магазин закрыт или ссылка устарела. У каждого магазина на Plum Market
      свой адрес, и открыть его можно только по нему.
    </p>
    <RouterLink to="/login" class="go">Кабинет продавца</RouterLink>
  </div>
</template>

<style scoped>
.empty-shop {
  --blue: #1f7aec;
  --ink: #152033;
  --ink-2: #4b5567;
  min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center;
  gap: 10px; padding: 24px; text-align: center; color: var(--ink); font-size: 15px;
  background: radial-gradient(900px 420px at 50% -140px, #dbe9fd 0%, rgb(219 233 253 / 0%) 70%), #f7f9fc;
}
.mark { display: inline-flex; align-items: center; gap: 10px; font-size: 17px; font-weight: 600; margin-bottom: 14px; }
.mark b { font-weight: 800; color: var(--blue); }
h1 { font-size: 27px; letter-spacing: -0.02em; margin: 0; }
p { margin: 0; max-width: 430px; color: var(--ink-2); line-height: 1.55; }
.go {
  margin-top: 16px; height: 44px; display: inline-flex; align-items: center; padding: 0 22px; border-radius: 12px;
  background: var(--blue); color: #fff; font-weight: 600; text-decoration: none;
}
</style>
