# Plum Market — storefront + кабинет мерчанта (prototype)

**Storefront (витрина покупателя)** from the MVP spec (`ТЗ на разработку Plum Market с нуля (1).pdf`). The
«Пользовательский workflow», «корзина и оформление заказа», «Избранное» and «Профиль» sections are done. It's served at `/`, and the merchant
admin lives on its existing paths (`/dashboard`, `/orders`, …). Both read the same database, so any price, discount,
category or on/off change made in the admin shows on the storefront on the next page load.

Merchant admin from the spec (`ТЗ на разработку Plum Market с нуля.pdf`).
Done: **Дашборд, Заказы, Клиенты** (iteration 1), **Чат, Продукты** (iteration 2), **Маркетинг** (iteration 3).
The other 8 sections are in the sidebar as "скоро" placeholders.

Stack: **ASP.NET Core 10** (controllers, EF Core + SQLite, ClosedXML, QuestPDF, Anthropic C# SDK) + **Vue 3** (Vite,
TypeScript, vue-router, Chart.js, Leaflet). UI language: Russian.

## Run

Requirements: .NET SDK 10 and Node 20+. No database server needed, since SQLite creates `plum.db` and seeds
~13 months of demo data on first start.

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

The product and category forms have the spec's two AI buttons. They call **Claude** (`claude-opus-5`, via the
official Anthropic SDK) when credentials are configured:

```bash
export ANTHROPIC_API_KEY=sk-ant-...        # or set it in Rider: Run configuration → Environment variables
```

Without a key they still work in **offline mode**. Uzbek Latin ⇄ Cyrillic is always an exact script conversion
(`UzTransliterator`), not AI. Russian ⇄ Uzbek needs Claude; offline, the form tells you so. Descriptions fall back
to a template. Requests opt into server-side refusal fallbacks (`fallbacks: default`), so a declined request is
retried on Anthropic's recommended fallback model instead of failing.

### Database resets on model changes

There are no migrations. `AppDbContext.SchemaVersion` is stored in SQLite's `user_version`. When the code's
version differs from the DB's, the DB is **dropped and re-seeded on start**. Upgrading from iteration 1 therefore
resets demo data.

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
  number of items and total, with "Отменить заказ". **Order page**: progress (Новый → В сборке → Готов к отправке →
  В пути → Доставлен; pickup: … → Готов к выдаче → Выдан), receiving, recipient, payment, items and totals. Pages
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

## What's implemented — admin

### Дашборд
- Period: сегодня / неделя / месяц / квартал / год / custom range. Branch filter.
- KPI cards: **Доход** (revenue, cost of goods, delivery, profit), **Заказы** (new/in-work, completed,
  cancelled), **Клиенты** (total, new, returning, average check). Each card shows a % change vs the previous
  period of the same length.
- Revenue & profit chart and order dynamics by status. Buckets are hourly for 1 day, daily up to 3 months, and
  monthly beyond that.
- Orders by channel (Telegram / website / Instagram), traffic sources, top-10 products, top-10 clients.
- Orders map (Leaflet + OSM) with branch pins and toggles.

### Заказы
- Status tabs with live counters: Все, Новый, В процессе, Просрочен, Готов, В пути, История заказов.
- Search by order ID, client name or phone. Filters: branch, employee, payment, delivery type, platform, status,
  date range.
- Order drawer with items and totals, client, address and comment. You can assign an employee and change the
  status from there, or use the one-click "next status" button in the list.
- **Overdue detection:** New/In-progress orders older than 90 minutes become "Просрочен" automatically.
- **Экспорт:** Excel file for a date range or all time, filtered by the statuses you pick.
- **Лист сборки:** PDF or Excel for a period and set of statuses, grouped by order (with checkboxes) or by
  product (totals to pick).
- **Автоответчик:** one editable text per status × language (ru/uz/en), with placeholders `{name}`,
  `{order_id}`, `{total}`, `{branch}` and `{bonus}`. It fires on every status change in the client's language.
  Sent messages are logged and shown in the order drawer. The real Telegram delivery is stubbed.
- "Тестовый заказ" creates a random incoming order so you can demo the flow.

### Клиенты
- Table: name, username, phone, orders, bonus points, date added, last visit, platform, action. Search
  (Cyrillic, case-insensitive), platform filter and sorting.
- **Настройки баллов:** turn the bonus system on or off and set how many сум earn 1 point (1 балл = 1 сум).
  Points are credited when an order is completed and taken back if it gets reopened or cancelled.
- The Telegram platform icon links to the store's bot (`t.me/<bot>`). The "Чат" button opens that client's chat
  thread.
- Client profile drawer with stats and recent orders. Dashboard top-10 rows link here.

### Чат
- Unified inbox for Telegram, Instagram, website and Wolt. Categories: Все, Не прочитано, Instagram, Обзоры.
  There is also a platform dropdown next to "Все чаты", search, and an unread badge in the sidebar.
- Each thread shows client details and the platform icon, with messages grouped by day. You can attach files
  (images and video preview inline) and insert emoji. Enter sends, Shift+Enter makes a new line.
- **Обзоры**: product reviews open as threads. Replying there publishes the answer on the review and marks it
  "Отвечено".
- **Настройки чата**: toggles for chat in group, chat with bot and auto-reply, plus an auto-reply text per
  platform. The auto-reply fires on a client's message, at most once per 6 hours per thread.
- The Clients page "Чат" button opens that client's thread, creating it if needed.
- "Входящее сообщение (тест)" simulates a message arriving from a channel webhook. The inbox polls every
  6 seconds; the real system would push updates instead.

### Продукты
Clicking "Продукты" in the sidebar only expands the submenu, as the spec describes. Every sub-page has a
**branch selector** ("в разрезе выбранного филиала").
- **Категории**: tree list with sub-category and product counts, creation date, edit and delete (blocked while
  the category still has children or products). The form has names and descriptions in ru / uz (Latin) /
  uz (Cyrillic) with Перевести and Сгенерировать. You choose "new category" or "sub-category of…", and set an
  image, product layout (2 or 3 per row, or list), product order, and an optional banner.
- **Продукты**: list with photo, price and old price, rating, review count, created date, active toggle, edit and
  delete. It has search in any language, a category filter (includes sub-categories) and a status filter.
- **Product form**: multilingual name and description with AI, photos and videos (the first image is the cover),
  category, price, unit, old price, cost, live profit and margin, variants, extra attributes, size and weight,
  tags, and branch availability.
- **Импорт**: download the Excel template, then upload it. Rows are matched by Russian name (update, else
  create), missing categories are created, and Cyrillic is filled in from Latin. You get a per-row error report.
  A separate "Внешний источник" tab stores sync parameters (Billz, МойСклад, 1С…). The key is never sent back to
  the browser.
- **Скидка**: name, percent or fixed amount, products, start and end, optional minimum order, branches, on/off,
  and status (действует / запланирована / завершена). It rejects a fixed discount that is bigger than a product's
  price.
- **ИКПУ**: code, package code and unit code per product, with per-row "Сгенерировать" and a bulk fill for
  products missing a code. ⚠️ Codes come from a **demo reference** (`IkpuCatalog`). In production this must query
  the tasnif.soliq.uz classifier.
- **Склад**: inline editing of cost, weight, price, availability status (Безлимитный / Ограничено / Нет в
  наличии) and quantity. Changes save on edit. Margin, last update and sales velocity (units per day, last 30
  days) are shown. **История продаж** opens a side panel with totals and a 12-month chart.

### Маркетинг
Clicking "Маркетинг" only expands the submenu, the same as "Продукты".
- **Рассылка**: messages sent through the Telegram bot. The table shows total, sent, not sent, clicks, blocked
  and date/time. Creating one takes two steps, as the spec says. Step 1 picks recipients, either by segment
  (platform, language, minimum orders, activity, bonus points) or by hand, with a live count of who the bot can
  reach. Step 2 is the message: name, image, text (Telegram's length limits), an optional link button, and send
  now or schedule, with a live Telegram preview. Each recipient gets a personal tracked link (`/r/{token}`), so
  clicks are real, unique, and attributable per person. Scheduled broadcasts send when due and can be cancelled.
- **Промокод**: percent or fixed discount, with a cap for percent codes, usage limit, minimum order, validity
  period, and extra restrictions (first order only, platforms, categories). There's a code generator, usage
  bars, and statuses (действует / запланирован / истёк / лимит исчерпан). A "Проверить промокод" box applies
  the same rules checkout will use (`MarketingService.CheckPromo`). The category restriction is checked
  against the cart, so it isn't part of that quick check.
- **Источники**: a Telegram-bot or website link per ad/post/QR code (`t.me/bot?start=src_…` or
  `?utm_source=…`), plus a short tracked link `/s/{slug}` that counts clicks. The table shows clicks, new and
  existing users, orders, conversion, created date and last visit. Renaming keeps the link, so printed QR codes
  keep working.
- **СМС-рассылка**: status filter На модерации / В процессе / Подтверждённый / Отклонённый. Templates come
  first and go through moderation, with a warning. The editor shows a live character and SMS-part count
  (160 per SMS for Latin text, 70 for Cyrillic) and an automatic pre-check (no short links, not all caps, at most
  6 parts). Campaigns use only approved templates and an audience.
- **Пост для канала**: the spec's 5-step instructions with progress ticks, connecting the channel, then a post
  composer (photo, text, button) with a live preview and a list of published posts.
- **Баннер**: main slider or category banner. Separate mobile and desktop image or video. The tap target can be
  a category, a product or a URL. The form has a phone-frame preview; the list has ordering and on/off.
- **Обзоры**: review table (product, rating, status, customer, comment, date) with the Все / Новый / Отвечено
  filter, a rating filter and search. You can reply with quick templates. Replies here and in Чат → Обзоры stay
  in sync.

Simulated (no external services): Telegram delivery, with ~4% of bot users marked as having blocked the bot and
non-bot customers marked "не отправлено". SMS moderation approves a template after 30 s; a campaign is in
moderation for 20 s, sending for 20 s, then confirmed with 97% delivered. The channel check only validates the
channel name format. New/existing users and orders per source are seeded; in production the storefront would
attribute them.

## Layout

```
backend/PlumMarket.Api/
  Domain/Entities.cs          orders, customers, branches, templates, settings
  Data/                       DbContext + deterministic seeder
  Services/OrderWorkflow.cs   status changes → overdue sweep, bonus accrual, auto-replies
  Services/OrderFilter.cs     filters shared by list / export / assembly sheet
  Services/OrderDocuments.cs  Excel export, PDF/Excel assembly sheet
  Domain/Catalog.cs, Chat.cs  categories, products (JSON-localized), stock, discounts, reviews; conversations
  Services/AiContentService   Claude translate/describe + offline fallback; UzTransliterator; IkpuCatalog
  Services/ChatService.cs     incoming/outgoing messages, auto-reply, review replies
  Domain/Marketing.cs         broadcasts (+recipients), promo codes, traffic sources, SMS, channel posts, banners
  Services/MarketingService   audience segments, simulated delivery/moderation, SMS parts, promo validation
  Controllers/                Dashboard, Orders, Customers, Settings, Chat, Categories, Products (+import),
                              Discounts, Ikpu, Stock, Uploads, Ai, Broadcasts, PromoCodes, Sources, Sms,
                              Channel, Banners, Reviews, Tracking (/r/{token}, /s/{slug} redirects)
frontend/src/
  views/                      DashboardView, OrdersView (+ orders/*), CustomersView (+ customers/*),
                              ChatView (+ chat/*), catalog/* (categories, products, discounts, ikpu, stock),
                              marketing/* (broadcasts, promo codes, sources, sms, channel post, banners, reviews)
  components/                 PeriodPicker, KpiCard, OrdersMap, Modal, Pager, PlatformIcon, Icon,
                              LocalizedEditor, MediaUploader, SingleImage, BranchSwitcher, ProductThumb,
                              AudiencePicker, TelegramPreview
```

## Prototype shortcuts (not production)

- No auth or multi-tenancy: this is one merchant, "Plum Bakery". The storefront API is public; its only writes are
  visitor chat messages.
- The storefront loads the active catalog into memory per request for pricing/search. That's fine for hundreds
  of products; a real store needs a search index and cached prices.
- SQLite with `EnsureCreated` instead of migrations. The dashboard aggregates in memory, which is fine for
  thousands of orders but needs SQL/materialized aggregates at scale.
- Traffic sources are seeded, not tracked. Notifications and chat replies are stored, not delivered to
  Telegram/Instagram/Wolt. Chat uses polling instead of websockets.
- Uploads are stored on local disk (`backend/PlumMarket.Api/uploads/`). SVG is not accepted.
- ИКПУ codes come from a demo reference, not the official classifier.
