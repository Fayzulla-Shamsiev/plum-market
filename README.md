# Plum Market — storefront + кабинет мерчанта (prototype)

**Регистрация и вход администратора** (`ТЗ … (2).pdf`): an entrepreneur registers with имя + номер телефона +
пароль, which creates the administrator **and their store**, and lands them in the admin panel. The store is a
website from that moment; a **Telegram Mini App** (`ТЗ … (3).pdf`) is added later from **Платформы** in the
panel, and opens the very same shop inside the merchant's own bot. Every administrator sees only their own store, products, categories, customers
and orders. A store created by registration starts empty with a ready template (settings, one branch, order
auto-replies); alongside it the installation keeps one **demo store** with a year of trading in it, so the product
can be shown without setting anything up (see below).

**Storefront (витрина покупателя)** from the MVP spec (`ТЗ на разработку Plum Market с нуля (1).pdf`). The
«Пользовательский workflow», «корзина и оформление заказа», «Избранное» and «Профиль» sections are done. It's served at `/`, and the merchant
admin lives on its existing paths (`/dashboard`, `/orders`, …). Both read the same database, so any price, discount,
category or on/off change made in the admin shows on the storefront on the next page load.

**Merchant admin** follows the MVP spec's «Административная часть»: products and categories, customers and orders,
the order status flow, delivery control and completion, and a dashboard. It has no platforms, payment types,
staff, tariffs, Telegram bot, SMS, broadcasts or traffic sources: the storefront is the only sales channel, and
payment is cash on receipt.

Stack: **ASP.NET Core 10** (controllers, EF Core + SQLite, ClosedXML, QuestPDF, Anthropic C# SDK) + **Vue 3** (Vite,
TypeScript, vue-router, Chart.js, Leaflet). UI language: Russian.

## Run

Requirements: .NET SDK 10 and Node 20+. No database server needed: SQLite creates `plum.db` on first start and
fills it with the demo store. Open http://localhost:5090/login — the demo accounts are on that page, one click
away — or http://localhost:5090/register to create your own empty store.

```bash
# API on http://localhost:5090 (also serves the built frontend from wwwroot)
cd backend/PlumMarket.Api
dotnet run --launch-profile http          # add `-- --reset` to drop and reseed the DB

# Frontend dev server with hot reload on http://localhost:5174 (proxies /api to :5090)
cd frontend
npm install
npm run dev

# Or build the frontend into the API's wwwroot and use only http://localhost:5090
npm run build
```

### AI features (Перевести / Сгенерировать)

The product and category forms have the spec's two AI buttons: **Перевести** fills the other catalog languages
from the one being edited, and **Сгенерировать** writes a storefront description in all of them. They call
**OpenAI** (`gpt-4.1-mini` by default, structured JSON output) through the backend — the browser only ever talks
to `/api/ai/...`, which needs an administrator session.

The API key is read from configuration and nowhere else, so it never reaches the repository or the browser:

```bash
# local development — stored outside the repo, in the user secrets store
cd backend/PlumMarket.Api
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."

# production (Render → Environment): the same setting as an environment variable
OpenAI__ApiKey=sk-...        # OPENAI_API_KEY is accepted too
```

`OpenAI:Model` (env `OpenAI__Model`) picks a different model without touching the code. Never put the key in
`appsettings*.json`, a `.env` file or any frontend code — all of those are served to the browser or committed.

Without a key the buttons still work in **offline mode**: Uzbek Latin ⇄ Cyrillic is an exact script conversion
(`UzTransliterator`, never AI), Russian ⇄ Uzbek says it needs a key, and descriptions fall back to a template.
Both failure and fallback are reported in the form, so nothing silently does nothing.

### Платформы: где магазин открыт

The panel's **Платформы** section (below Маркетинг) is where a shop meets its customers. One shop, two doors —
the catalog, cart and orders behind them are the same.

- **Веб-сайт** — every store has one from the moment it is registered. The page holds the shop's name, «О нас»
  and «Условия возврата и обмена» (the pages a shopper opens from their profile), and a button that opens the
  shop. The address is fixed at registration, so links and a connected bot never go stale.
- **Telegram-бот** — optional. The administrator creates a bot in [@BotFather](https://t.me/BotFather) with
  `/newbot` and pastes its token. The server checks it with `getMe` (which is where the bot's name and username
  come from — they are never typed) and then sets the bot up to sell:
  - `setChatMenuButton` puts **Open Shop** next to the message field;
  - `setMyCommands` + `setMyDescription` give the bot a `/start` and something to say in an empty chat;
  - the bot answers: a customer writes to it and gets a greeting by name with an **«Открыть магазин»** button,
    so the bot sits at the top of their chat list and the shop is one tap away — no searching, no sign-in.

  The page links straight to the bot, re-attaches everything, swaps the bot or lets it go. A bot belongs to one
  shop only, and the buttons always open **that** shop. It has three tabs:

  - **Бот** — the connection itself.
  - **Сообщения бота** — the two texts the merchant owns: what an empty chat shows before «Начать», and the
    answer to `/start` (`{name}`, `{store}`; the shop button is added automatically). Both start from a default
    written around the shop's name, can be rewritten by hand or by **Сгенерировать** (OpenAI), and «Обновить»
    pushes the first straight to Telegram.
  - **Автоответчик** — the per-status order messages, the same editor as Заказы → «Сообщения покупателю».

**Order updates reach the customer in Telegram.** When the shop has a bot and the customer has opened the shop
inside it, every status change is delivered both to their chat with the store on the site and to Telegram, with
the button to reopen the shop; the order's notification log says which. The link between an account and a chat
is made by the Mini App: Telegram signs its launch data with the bot token, the server re-computes that HMAC
(`TelegramBotApi.VerifiedUserId`) and only then stores the chat id, so a page cannot claim someone else's chat.

Telegram can only call a webhook on a public https address, so how the bot hears about a message depends on
where the shop runs: `setWebhook` + `/api/telegram/{storeId}` (checked against a secret Telegram sends back) when
there is one, and `TelegramPollingService` asking `getUpdates` when there isn't — a shop being tried out on a
local machine still has a talking bot. Either way the answer is built in `TelegramGreeter`, so it is the same.

**Inside a bot there is only the shop.** Whoever opened it came to buy something, so the router sends any
non-storefront address — `/dashboard`, `/login`, anything — back to the storefront; the merchant's panel and its
sign-in do not exist in a Mini App. A shop that can't be loaded shows a plain "магазин недоступен" screen.

Telegram opens a Mini App over **https only**, and a shop running on `localhost` has no such address. Rather
than leaving the bot without a button, connecting from a local run points it at the published prototype
(`Telegram:MiniAppUrl`, default `https://plum-market.onrender.com`) and says so in the panel — the button works
immediately, it just opens the deployed shop. Connect the bot **from the deployed site** (or press «Привязать
заново» there) and it opens that administrator's own shop instead. `StoreLinks` builds the address from the
request — Render's `X-Forwarded-Proto` is honoured — or from `PublicUrl` when set.

Inside Telegram the storefront adapts itself (`frontend/src/shop/telegram.ts`): the SDK is loaded only when
Telegram's launch parameters are present, the app reports `ready`, takes the full height, paints Telegram's
header like the page, uses Telegram's own back button for navigation, drops the merchant links a customer has no
use for, and offers the Telegram account's name at sign-in (Telegram never hands over a phone number).

`Telegram:ApiBase` points the Bot API calls somewhere else — used to exercise the whole flow against a stub.

### Демо-магазин (what a presentation opens)

On start-up, when no store with the slug `demo` exists, the app creates **Plum Bakery**: 3 branches, 34 products
in 14 categories, 380 customers, ~3 300 orders over the last 13 months (including a live queue in every status,
two of them overdue), discounts, promo codes, banners, reviews and a chat inbox. Dates are relative to the
moment it is seeded, so the dashboard always shows a store that traded *yesterday*.

| | |
|---|---|
| Администратор | `+998 90 111 11 11` · пароль `demo1234` |
| Покупатель (витрина) | `+998 90 222 22 22` · имя `Малика` |
| Витрина | `/shop/demo` |

Both sign-in pages offer these accounts as a button (`GET /api/auth/demo`), so nothing has to be typed on stage.
The customer account has orders in flight (one waiting for the store, one on its way), finished orders, bonus
points, reviews to leave, a saved address and a chat with the store; favourites and "recently viewed" live in the
browser, so signing in as the demo customer fills those too.

The store is **only created when it is missing** — an existing one is never touched, so anything done during a
demo stays. `Demo__Enabled=false` (or `dotnet run -- --no-demo`) leaves the installation blank.

### Данные и деплой на Render

Render's free instances have no disk: the container's filesystem is discarded on every deploy, which takes
`plum.db` with it. The app handles that by re-creating the demo store on the next start, so a deploy never lands
on an empty site. Orders placed during a demo, however, disappear with that deploy.

To keep everything between deploys, use a paid instance with a disk and point the database at it (both are
prepared, commented, in `render.yaml`):

```
disk:      name plum-data, mountPath /var/data, 1 GB
env var:   ConnectionStrings__Default = Data Source=/var/data/plum.db
```

The app creates the folder if it's missing, and the demo store is then seeded once and kept. Note that changing
`AppDbContext.SchemaVersion` still drops the database on the next start — that is how the prototype replaces
migrations.

### Accounts, stores and where the storefront lives

- `/register`, `/login` — администратор. The session is a bearer token in `localStorage`; every `/api/...` call
  carries it, and the server resolves the store from it (`StoreMiddleware`).
- A shop is reached **by its own address, and only by it** — there is no page that lists the shops on the
  platform, and no API that returns them. An administrator reaches their own shop's panel; any other shop is
  just a shop to them, like it is to any customer.
- In production each store answers on its own subdomain (`bakery.plum.uz`), which `StoreMiddleware` already
  resolves. On this prototype every store shares one host, so `/shop/{slug}` opens a shop and the browser
  remembers it (sent as `X-Store` on every `/api/shop/...` call) while the storefront keeps its usual paths
  (`/`, `/cart`, `/profile`, …). An address that names no shop isn't a shopper's page at all, so it opens the
  merchant's sign-in.
- Because the prototype host names no shop by itself, a bare address falls back to the only store of the
  installation, or to the demo store. That fallback is the one piece that disappears with real subdomains.
- Switching shops clears the cart, favourites and customer session: another shop is another account.

### Database resets on model changes

There are no migrations. `AppDbContext.SchemaVersion` is stored in SQLite's `user_version`. When the code's
version differs from the DB's, the DB is **dropped and created empty on start** — every store and account with it.

## What's implemented — storefront

UI in three languages (Русский / O'zbekcha / Ўзбекча, switch in the header). Catalog names come from the matching
localized field. Blue/green palette; phone layout with a bottom tab bar.

- **Главная** (`/`): the admin's "Главный слайдер" banners (auto-rotating, separate phone and desktop images),
  category tiles, «Выгодно сейчас» (products with a running discount), «Популярное», then one row per top-level
  category with sub-category chips.
- **Карточка в каталоге**: image, name, price, old price with a −% badge, rating with review count, weight, a ♡
  button, and **Купить**, which becomes a −/+ stepper once the item is in the cart. Out-of-stock items are shown last.
  The price is computed on the server: the best running discount that applies to everyone. Branch-specific
  discounts and ones with a minimum order are left for checkout.
- **Категории** (`/catalog/:id`): pick one from the sticky chip rail under the header, or open the **Каталог**
  menu (desktop: two panes with hover; phone: full-screen drill-down). The page has breadcrumbs, sub-category chips
  (a leaf shows its siblings), a "только в наличии" toggle, sorting, and "Показать ещё". The category's
  "Сортировка товаров" setting from the admin is the default sort.
- **Поиск**: a header box with type-ahead (matching categories with their path, then products with the typed
  part highlighted, keyboard navigation, recent searches). Enter opens `/search?q=` with category hits, filters
  per top-level category, and sorting. Matching runs across ru / uz / oz and both Uzbek scripts (`somsa` ⇄
  «Сомса»), ignores ё/е and the apostrophe variants in oʻ/gʻ, needs every word to match («торт мед»), and also
  matches by category name, tags and description.
- **Постоянная навигация**: sticky header (logo → home, Каталог, search) and category rail; on phones, a bottom
  bar (Главная / Каталог / Поиск) and a back-to-top button.
- **Карточка товара** (`/product/:id`): photo/video gallery (thumbnails, arrows, swipe on phones, full-screen
  viewer), current and old price, discount and savings, variant picker (e.g. pizza sizes, each with its own price),
  rating linked to the reviews, **В корзину** with −/+, ♡ **В избранное**, stock ("много" / "N шт" / "нет") with a
  per-branch breakdown, a seller card (store, rating, products, branches), and **Написать продавцу**. A short
  description sits at the top; the full description and characteristics are below. Reviews show a star breakdown
  (click a bar to filter), sorting, paging and the store's replies. On phones, a sticky price + buy bar.
- **Вы недавно смотрели** (last 20 product pages, kept in the browser) and **Рекомендуем**: products bought
  together with this one, then the same category, then bestsellers. Each card has ♡ and Купить.
- **Написать продавцу**: a chat drawer. A visitor who isn't logged in is identified by a random token in the browser.
  The first question carries the product. The thread lands in the admin **Чат** (channel «Сайт»), and the
  merchant's replies show in the drawer (polled every 5 s). The thread is only created on the first message.
- **Избранное** (`/favorites`): ♡ on cards and the product page; the list page and empty state from the spec's
  «Избранное» section. Kept in the browser until phone login exists.
- **Корзина** (`/cart`), basic: lines kept in the browser and re-priced by the server on every change
  (`POST /api/shop/cart/quote`), so admin price, discount and stock changes show immediately. You can change
  quantities (capped at stock, with a notice), remove, add to favourites, see unavailable items, and see the «Ваш
  заказ» totals with the discount. **Перейти к оформлению** is shown but disabled until the next iteration.
- **Корзина**, per the checkout spec: tick or untick lines ("Выбрать все", "Удалить выбранные"). «Ваш заказ» counts
  the ticked lines, and only those go to checkout; unticked ones stay in the cart.
- **Вход** (`POST /api/shop/account/login`): phone (+998 mask) and name, asked for before checkout and the profile.
  An existing customer with that phone is signed in; otherwise a new one is created (platform «Сайт»). The session
  is a random bearer token and only its SHA-256 is stored (`CustomerSession`). A guest chat joins the account.
  ⚠️ No SMS confirmation yet (the MVP spec doesn't ask for it); the login/registration spec needs to add an OTP.
- **Оформление** (`/checkout`), with live totals from `POST /api/shop/checkout/quote`:
  - Receiving: **Самовывоз** shows the branch and its address; "Изменить" lists every branch with what it has or lacks
    from this cart (per-branch stock). **Доставка** offers saved addresses or a new one (street, flat/entrance/floor,
    and an optional map pin with "Моё местоположение"). The nearest branch that has the whole cart fulfils it.
  - Pricing for the branch: branch-only discounts and "from N сум" discounts apply here (e.g. the cake −30 000 at
    Чиланзар from 200 000). Delivery costs 15 000, free from 200 000 (`StoreSettings.DeliveryFee`/`FreeDeliveryFrom`),
    with a "до бесплатной доставки" hint.
  - **Получатель**: name and phone from the profile; "Изменить" sets first name, last name and phone for this order.
  - **Оплата**: cash only, as in the spec. Comment for the courier.
  - **Промокод**: applied with the admin's rules (dates, limit, minimum, first order, platform «Сайт», categories).
    Errors show next to the field.
  - Missing stock blocks the order with the reason. The server re-checks everything when the order is placed.
- **Создание заказа** (`POST /api/shop/orders`): the order appears in the admin's Заказы (platform «Сайт», status
  Новый) with recipient, address, pin, comment, promo code and variant. The «Новый» auto-reply fires. Limited
  stock is deducted from the fulfilling branch and the promo use is counted. Ordered lines leave the cart.
- **Профиль** (`/profile`, asks for phone + name when signed out): name, phone and bonuses, then Редактировать профиль,
  Мои заказы, Мои отзывы, Настройки, О нас, Условия доставки, Условия возврата и обмена, Связаться с нами, Выйти.
  - **Редактировать профиль** (`/profile/edit`): first and last name, phone (unique), e-mail, country, birth date,
    gender (`PUT /api/shop/account/me`).
  - **Мои отзывы** (`/profile/reviews`): "Ожидают оценки" lists products from completed orders that aren't rated yet;
    "Оценённые" shows the customer's reviews with the store's reply. Rate with 1–5 stars and a comment, or edit. A new
    review appears on the product page and in the admin's Обзоры (Маркетинг and Чат), where the merchant can reply.
    Editing reopens it as "Новый".
  - **Настройки** (`/profile/settings`): language (also saved to the account, so auto-replies use it), "Статус
    заказа" notifications (off = no auto-replies for their orders), "Акции и новости" (off = left out of broadcast
    audiences), and clearing viewed/search history on this device.
  - **О нас** (`/about`), **Условия доставки** (`/delivery-terms`), **Условия возврата и обмена** (`/returns`):
    public pages built from `StoreSettings` (phone, hours, about text, terms text where "## " starts a heading) and
    `Branch` (address, phone, hours). The delivery page shows the live fee and free threshold. Also linked in the footer.
  - **Связаться с нами** (`/contact`): the phone number with Позвонить and Скопировать, branch numbers, and Открыть чат.
  - **Чат с поддержкой**: a signed-in customer has one thread across devices. Their orders are shown above the input;
    tapping one attaches it, the message carries a link to the order, and the admin sees "📦 Заказ №N".
- **Мои заказы** (`/profile/orders`): Активные / Все tabs; each card shows ID, status, date and time, recipient,
  number of items and total, with "Отменить заказ". **Order page**: progress with times (Новый → В сборке → Готов к отправке →
  Передан в доставку → В пути → Доставлен → Завершён; pickup: … → Готов к выдаче → Завершён), receiving, recipient, payment, items and totals. Pages
  refresh every 15–20 s, so admin status changes show up. Per the spec, **Отменить заказ** is shown only while the
  store hasn't confirmed the order (Новый). Once the admin moves it to В сборке the button disappears and the page
  points to support. On cancel, stock and the promo use are returned and the reason shows in the admin.
- Header ♡, cart (with counter) and profile; the phone bottom bar is Главная / Каталог / Избранное / Корзина / Профиль.
- Products without photos get a generated placeholder (emoji on a soft tint) until the merchant uploads one.

API: `GET /api/shop/{meta,home,catalog,search,suggest}`, `GET /api/shop/products/{id}[/reviews|/recommended]`,
`GET /api/shop/products?ids=`, `POST /api/shop/cart/quote`, all with `lang=ru|uz|oz` (`ShopController`,
`Services/StorefrontCatalog.cs`), plus `/api/shop/chat/*` (`ShopChatController`). Frontend: `frontend/src/shop/`
(browser-side state in `shop/state/`).

Also `/api/shop/account/*` (`ShopAccountController`, `Services/ShopAuth.cs`) and `/api/shop/checkout/quote`,
`/api/shop/orders*` (`ShopOrdersController`, `Services/CheckoutService.cs`).

Also `/api/shop/info`, `/api/shop/account/{settings,reviews}` (`ShopProfileController`).
Favourites and the cart are still kept in the browser, not the account. Order statuses use the admin's current set; the spec's «Передан в доставку»
step comes when the admin workflow is connected.

## What's implemented — admin (MVP)

Sidebar: Дашборд, Заказы (green badge = new storefront orders), Клиенты, Чат, Каталог (Категории, Товары,
Скидки, Склад), Маркетинг (Промокоды, Баннеры, Отзывы), Магазин.

### Order flow (`Domain/OrderFlow.cs`)
Новый → В сборке → Готов к отправке → Передан в доставку → В пути → Доставлен → Завершён; pickup orders go
Новый → В сборке → Готов к выдаче → Завершён. The store moves an order **one step at a time**. The API refuses
skips and names the correct next step. The store can cancel up to "В пути"; the customer only while it's "Новый".
Cancelling returns limited stock and the promo use. Every change is kept in `OrderStatusChange` (time, and
Покупатель or Магазин), so both the admin order card and the customer's order page show a timeline with times.
"Просрочен" is a flag, not a status: an order still Новый or В сборке after the store's limit (Магазин → Заказы).
Each status sends the customer a message (editable in "Сообщения покупателю") into their chat with the store, unless
they turned order updates off in Настройки. Bonus points are credited on Завершён.

### Заказы
- **Доска** (default): one column per active step, with the "Delivered → Подтвердить завершение" check at the end.
  Each card shows the order, recipient, address or pickup branch, total and age, a "просрочен" flag, and one button
  for the next step (Принять в сборку, Готов, Передать курьеру, Курьер выехал, Доставлен, Подтвердить завершение or
  Выдан покупателю).
- **Список**: tabs Все / Новые / В сборке / Готовы / В доставке / Доставлены / Просроченные / Завершённые и
  отменённые; search; branch, delivery-type and date filters; the same next-step button.
- **Order card**: timeline, next step, cancel with a reason (the customer sees it), recipient, address, comment,
  items, promo code, totals, bonus, and the messages sent to the customer.
- The queue refreshes every 15 s and announces new storefront orders. Also: Лист сборки (PDF/Excel), Экспорт
  (Excel).

### Дашборд
Выручка (with cost, delivery, profit), Заказы, Клиенты (new, returning, average check), Продажи (units sold, per
order), Баланс (cash received for completed orders, plus cash still expected from orders in progress). Each has a
delta vs the previous period, except Баланс, which is all time. Also: revenue chart, "Заказы сейчас" (live count per
step, plus overdue), order dynamics, top-10 products, orders map, top-10 customers. Period and branch filters.

### Клиенты
Name, phone, e-mail, orders, bonus points, sign-up and last visit; search by name, phone or e-mail; bonus settings.
The profile drawer shows what the customer filled in on the storefront (country, birth date, gender, notification
choices) and their orders.

### Чат
Storefront conversations only: support chat, "Написать продавцу" (with the product), questions about an order, and
reviews ("Отзывы"). Categories are Все / Не прочитано / Отзывы. There's an auto-reply for new messages, and status
messages show in the customer's thread.

### Каталог and Маркетинг
Категории, Товары (multilingual with AI translate/generate, photos, variants, per-branch availability, Excel import),
Скидки (store-wide, branch-only, "from N сум"), Склад (stock per branch, used by checkout). Промокоды (checked at
checkout), Баннеры (storefront slider), Отзывы (reply; the customer sees it in Мои отзывы).

### Магазин (`/store`, `StoreController`)
Phone and working hours; delivery fee, free-delivery threshold and delivery terms; the overdue limit; branches (add/edit on a map, copy another branch's stock, delete only without
orders). All of it shows on the storefront's info pages, checkout and branch lists straight away.

## Layout

```
backend/PlumMarket.Api/
  Domain/Entities.cs          orders, customers, branches, templates, settings
  Domain/Tenancy.cs           Store, AdminUser, AdminSession + IStoreOwned (what a merchant owns)
  Data/AppDbContext.cs        DbContext; one global query filter per store-owned entity
  Data/StoreProvisioner.cs    the template a store is created with at registration
  Data/DemoData.cs            the demo store: admin account, 13 months of orders, customers
  Data/DemoCatalog.cs         its catalog, discounts, reviews and chat inbox
  Data/DemoMarketing.cs       its promo codes and banners
  Data/DemoShopper.cs         the demo customer: orders in flight, bonuses, reviews, saved address
  Services/AdminAuth.cs       registration/login by phone + PBKDF2 password, bearer sessions
  Services/TelegramBotApi.cs  checks a bot token (getMe), the menu button, commands, webhook, messages
  Services/TelegramGreeter.cs what the bot answers a customer: hello + the button that opens the shop
  Services/TelegramPolling…   asks Telegram for messages when the bot has no webhook (local runs)
  Services/StoreLinks.cs      the shop's public address, for Telegram and for the administrator
  Middleware/StoreMiddleware  resolves the store of every request (admin token, or shop slug)
  Domain/OrderFlow.cs         the order status flow: steps, next step, who may cancel, overdue rule
  Services/OrderWorkflow.cs   status changes → history, bonus accrual, messages to the customer's chat
  Services/OrderFilter.cs     filters shared by list / export / assembly sheet
  Services/OrderDocuments.cs  Excel export, PDF/Excel assembly sheet
  Domain/Catalog.cs, Chat.cs  categories, products (JSON-localized), stock, discounts, reviews; conversations
  Services/AiContentService   Claude translate/describe + offline fallback; UzTransliterator; IkpuCatalog
  Services/ChatService.cs     incoming/outgoing messages, auto-reply, review replies
  Domain/Marketing.cs         broadcasts (+recipients), promo codes, traffic sources, SMS, channel posts, banners
  Services/MarketingService   audience segments, simulated delivery/moderation, SMS parts, promo validation
  Controllers/                Auth (register/login/me), Platforms (website + Telegram bot),
                              TelegramWebhook (what the bot answers), Dashboard (+setup checklist),
                              Orders, Customers, Settings, Chat, Categories, Products (+import),
                              Discounts, Ikpu, Stock, Uploads, Ai, Broadcasts, PromoCodes, Sources, Sms,
                              Channel, Banners, Reviews, Tracking (/r/{token}, /s/{slug} redirects)
frontend/src/
  auth.ts                     admin session: token, register/login/logout, restore after reload
  views/auth/AuthView.vue     вход и регистрация (one page, two modes)
  shop/telegram.ts            Telegram Mini App: SDK, theme, back button, the customer's name
  views/platforms/*           Платформы: Веб-сайт (name, «О нас», возвраты) and Telegram-бот (connect, re-link, drop)
  views/                      DashboardView (+ SetupChecklist), OrdersView (+ orders/*), CustomersView (+ customers/*),
                              ChatView (+ chat/*), catalog/* (categories, products, discounts, ikpu, stock),
                              marketing/* (broadcasts, promo codes, sources, sms, channel post, banners, reviews)
  components/                 PeriodPicker, KpiCard, OrdersMap, Modal, Pager, PlatformIcon, Icon,
                              LocalizedEditor, MediaUploader, SingleImage, BranchSwitcher, ProductThumb,
                              AudiencePicker, TelegramPreview
```

## Prototype shortcuts (not production)

- Admin passwords are PBKDF2-hashed and sessions are bearer tokens, but there is no SMS confirmation, password
  reset, rate limiting or HTTPS-only cookie — the login spec's OTP step is still to come.
- The storefront API is public per store (the shop slug identifies it); its only writes are orders, reviews and
  visitor chat messages, and customers sign in with phone + name without a code.
- The storefront loads the active catalog into memory per request for pricing/search. That's fine for hundreds
  of products; a real store needs a search index and cached prices.
- SQLite with `EnsureCreated` instead of migrations. The dashboard aggregates in memory, which is fine for
  thousands of orders but needs SQL/materialized aggregates at scale.
- Chat and the order queue use polling instead of websockets.
- The backend still contains unused code from the earlier full admin (broadcasts, SMS, channel posts, traffic
  sources, ИКПУ, employees, Telegram/Instagram channels). The UI no longer reaches it.
- Uploads are stored on local disk (`backend/PlumMarket.Api/uploads/`). SVG is not accepted.
- Anyone can register a store on the demo. Stores are separated from each other, but nothing stops a new
  account from being created — that's deliberate for a prototype, not something to ship.
- The demo accounts and their password are handed out by a public endpoint and printed on the sign-in pages.
  Turn the demo off (`Demo__Enabled=false`) before this is anything but a prototype.
- Bot tokens are stored as plain text in SQLite and never leave the server. A real system would encrypt them,
  and would also verify Telegram's `initData` signature before trusting who the Mini App says its user is — the
  storefront only uses the name it offers, and still asks for a phone number.
