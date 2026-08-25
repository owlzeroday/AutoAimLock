# AUTOAIMLOCK — Premium SaaS Plan (Research Summary)

*Research date: 2026-08-25. Sources: licensing-platform docs (Keyforge, Dodo Payments, Traffic Orchestrator), academic study of the cheat market (Univ. of Birmingham "Checkmate" paper), payment-processor policy analyses (WIRED, TechSpot, SeamlessChex).*

---

## 1. Market reality (what the data says)

| Tier | Typical price | Notes |
|---|---|---|
| Public/free cheats | $0 | Detection-prone, crowded |
| Public paid | $10–40/mo | Standard aimbot/ESP subs |
| **Private/slotted** | **$50–200/mo** | Limited users, lower detection, "premium" positioning |
| Exclusive/VIP | $100–300/mo | Invite-only, longest lifespan |

- Subscriptions (1-day trial → 3-month) are the **standard model**; trials convert well.
- Buyers pay for **uptime** ("is it detected?") and **support**. Status pages are table stakes.
- Top vendors make $350k+/mo; a single Destiny-2 seller did 5,848 sales in 19 months through Stripe alone.
- **Price correlates with anti-cheat strength (r≈0.95)** — harder games justify higher prices.

## 2. ⚠️ The #1 business risk: payments & legal

This is the part that kills most solo sellers — not the code.

- **Stripe/PayPal explicitly prohibit cheat software.** PUBG Corp got PayPal to ban cheat sellers and freeze assets for 180 days. A "gaming tool" description triggers manual-review rejection or later termination.
- **Copyright "derivative work" lawsuits** target sellers (Blizzard v. Bossland — $8M; Bungie v. Lavi Cheats — $6.7M). Selling is not *criminal* in most countries (exceptions: South Korea, China), but civil liability and processor bans are real.
- **Mitigation options:**
  1. **Position as an accessibility/assistive tool** for offline/single-player/bot-matches (like our current About text) — genuinely different risk profile, smaller market.
  2. **Merchant of Record (MoR)** — Paddle / LemonSqueezy / Dodo Payments handle payments + global tax and absorb some merchant risk (still can ban you).
  3. **High-risk processors** (Nuvei, Worldpay) or **crypto** — what the industry actually uses; higher fees, no recourse.
- **Practical recommendation:** start MoR (LemonSqueezy/Dodo — built-in license keys, ~5% + fees), keep marketing language clean, never name games in ads, add a clear ToS ("single-player/allowed servers only"), and have a crypto fallback rail ready.

## 3. Licensing architecture (what to build)

Consensus best practices (2026):

1. **Server-side key generation only** — never ship a generator in the client.
2. **Hash keys at rest** (SHA-256); show the full key once; mask in dashboards.
3. **Bind keys to devices** — cap 3–5 activations per key; hardware fingerprint (hashed disk serial + machine GUID), with self-service slot reset.
4. **Signed tokens, verified locally** — server issues a short-lived JWT/Ed25519-signed token at activation; client verifies with the **public key only** (no secrets in the binary).
5. **Short token lifetime** (24h–7d) + **grace period** (48–72h offline tolerance) — this is what makes revocation actually work and avoids 1-star "locked out" reviews.
6. **Webhook automation** — payment → issue key; refund/chargeback → revoke; upgrade → retier.
7. **Self-service portal** — device list, deactivate, rotate key. Cuts support load and key-sharing.
8. **Rate-limit validation endpoints**; log activations (key, IP, device) to detect leaks.

**Build vs buy:** in-house = 2–4 months of work + $50–200/mo infra; licensing APIs/MoR with built-in keys = **$0–29/mo start, integrate in hours**. For a solo launch, **Dodo Payments or Lemon Squeezy** (license keys + checkout + tax included) is the fastest safe path. Keyforge if you want a dedicated licensing API.

## 3. Data storage: what goes where (decided)

**Client side — NO SQLite. Keep JSON.**
- Local settings (`%AppData%\AUTOAIMLOCK\settings.json`) are a handful of scalars — JSON is the correct format. SQLite adds a file DB, migrations, and corruption handling for zero benefit at this scale.
- Later, the client adds exactly two more local artifacts, both still JSON: a **cached license token** (the signed JWT from activation, DPAPI-protected) and an **offline grace timestamp**. No local database, ever.
- Rule of thumb: SQLite earns its place only when data is relational/large (thousands of rows, queries). Settings and a token cache are neither.

**Server side — YES, a real DB (Phase 2 of the launch sequence).**
| Data | Store | Notes |
|---|---|---|
| Users, password hashes (argon2) | Postgres / Cloudflare D1 | Never plaintext |
| License keys | SHA-256 hashes | Show once at creation; mask in portal |
| Device activations | Table keyed by license | Cap 3–5; self-service reset |
| Cloud color profiles | JSON blobs keyed by game+version | The retention moat — patch-day updates |
| Subscription state | Synced from MoR webhooks | Source of truth = payment provider |
| Telemetry (opt-in) | Append-only events | Uptime claims, crash stats |

- Cheapest viable stack: **Cloudflare Workers + D1 (SQLite-at-edge) + KV cache** ≈ $5/mo, or Supabase (Postgres + auth) free tier.
- The client never talks to the DB directly — only to the licensing API (activate/validate/heartbeat endpoints).

## 4. Premium feature ladder (what justifies the price)

**Free tier (current app):**
- Full local aim engine, 1 game profile, manual updates.

**Pro ($9.99/mo or $59/yr):**
- Cloud color-profile library (per-game, updated when games patch — this is the killer retention feature since patches break HSV bands)
- Config cloud sync across devices
- All game presets + custom profile slots
- Auto-updates + in-app status page (undetected/patching)
- Overlay HUD customization

**Elite ($19.99/mo):**
- Priority profile updates (same-day after game patches)
- Private/slotted builds
- Discord priority support + direct channel
- Early-access features

## 5. UI/UX gaps to close before charging (current app vs premium bar)

| Area | Now | Premium bar |
|---|---|---|
| Onboarding | Splash + main window | Login/activation screen, trial nagger, "get key" CTA |
| Account | None | Login window, plan badge (FREE→PRO), device management link |
| Updates | Manual reinstall | In-app updater + "what's new" dialog |
| Status | None | Status page (working/patching/detected) — buyers check this first |
| Trust | About text | ToS, refund policy, changelog page, support links |
| Polish | Dark terminal theme | Consistent, but needs: icon branding, window animations, empty states, tooltips |
| Telemetry | None | Anonymous crash + usage stats (opt-in) for uptime claims |

## 6. Recommended launch sequence

1. **Phase 1 (now):** polish UI (done in v1.3.0), add login/activation window skeleton + "FREE PLAN" badge logic, ToS dialog.
2. **Phase 2:** stand up Lemon Squeezy/Dodo product with license keys; implement activate/validate/deactivate against their public endpoints; 72h offline grace; device cap 3.
3. **Phase 3:** cloud profile service (simple API + DB), in-app updater, status page.
4. **Phase 4:** Pro/Elite tiers, Discord community, referral system.
5. **Ongoing:** monitor chargebacks, keep a crypto payment fallback, never name games in marketing.

## 7. Key numbers to plan around

- MoR fees: ~5% + $0.50/txn (vs Stripe 2.9% — the premium buys you survival)
- Licensing API: $0–29/mo at start
- Infra (API + DB + status page): $10–40/mo (Cloudflare Workers + D1/KV can be ~$5)
- Break-even: ~10 Pro subs covers fixed costs
- Churn driver #1: game patches breaking profiles → same-day profile updates are your retention moat
