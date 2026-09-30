# Crosscutting frontend performance evidence ledger

This report records application-level delivery optimizations and their scoped
local evidence across bounded contexts. Broad E2E coverage is functional
regression evidence, not performance measurement for every page. The original
comparison measured `/iam/login` and `/iam/register-owner`. The additional
[remaining-screen audit](#remaining-screen-local-lighthouse-audit--2026-09-29)
records **26 valid mobile/desktop audits across 13 route-role screens (10 distinct
paths)** on current optimized sources. Local results do not establish CI
acceptance, production performance or coverage of every application state.
The [surgical round-two entry](#surgical-round-two-fixes-and-local-convergence--2026-09-29)
adds scoped fixes and **30 paired mobile/desktop observations across 15 route-role
screens**, including new same-stack login/register-owner baselines.

## Scope matrix

| Scope | Change / coverage | Evidence limit |
|---|---|---|
| All contexts: IAM, Subscriptions, Profiles, Publishing, Devices, Analytics | Shared gzip, static cache policy and module chunks | Delivery/header and built-app regression evidence; no all-page Lighthouse claim |
| IAM | Deferred Cloudinary widget in both registration forms; existing auth hero preload on direct auth entries | Original login/register-owner comparison; new register-builder mobile/desktop observation; mocked first-action provider evidence |
| Subscriptions | Deferred Stripe client initialization | Unit/purchase regression and new Builder my-subscription + embedded plans audit; payment simulated, no real-provider acceptance |
| Profiles, Publishing, Devices, Analytics | Shared delivery changes and existing functional regression coverage | New scoped route-role Lighthouse observations below; no context-specific optimization or all-state performance claim |

## Surgical frontend performance evidence — 2026-09-29

**Outcome:** local validation passed for the scoped optimizations. CI acceptance
is pending; this entry does not promote the historical gates below or claim a
production performance result. Changes are uncommitted.

### Journey, actors and acceptance

`Anonymous entry → login/registration → authorized navigation → photo upload or
Builder checkout on first action`.

| Actor | Observable acceptance | Owning evidence |
|---|---|---|
| Anonymous | Login and both registration forms mount; no Chart/Stripe/Cloudinary SDK requests before use | `performance-delivery.spec.js` |
| Builder | Register/login, projects, profile, analytics and purchase remain usable | Existing full E2E suite |
| Assigned Owner | Register/login/logout, profile, analytics and devices remain usable | Existing full E2E suite |
| Unassigned Owner | Registration still fails closed; invitation failures cannot bypass eligibility | `iam-form-feedback.spec.js` |
| Both registration actors | First photo click loads widget once; successful callback updates preview; failed SDK opens local picker | `performance-delivery.spec.js` |
| Browser/crawler | Gzip JS/CSS, Vary, immutable hashed assets; HTML/unversioned files revalidate; missing assets return 404; API never inherits static cache; robots is plain text | `performance-delivery.spec.js`, `tests/nginx-config.mjs` |

Affected boundaries: Vue/Vite module graph and browser → outer Nginx → frontend
Nginx; existing frontend/API/MySQL contracts; Cloudinary script and Stripe SDK
initialization. Backend logic, schemas, API proxy settings and CSS/image design
are unchanged. No global `window.Chart`/`new Chart` consumers were found in the
frontend source; dashboards use npm Chart.js imports.

### Risk tiers and layer ownership

| Tier | Scenario | Cheapest trustworthy layer |
|---|---|---|
| A | Broken module initialization/circular chunks; auth and Owner authorization regressions | Built-app E2E for both roles + existing API contract tests |
| A | Stale HTML, cache leaking onto API, missing hashed assets returning SPA HTML | Real Nginx header/body E2E and parser validation |
| B | Concurrent SDK loads, SDK failure/timeout, missing global and retry | Unit tests; first-action browser convergence for both roles |
| B | URL checkout accidentally initializes Stripe; deferred client shares initialization and retries rejection | Subscription E2E + mocked pure-entry client unit tests |
| C | Auth hero loads twice or is fetched on authenticated non-auth entry | Browser network assertions; same URL as computed CSS background |
| D | New saturation/security campaigns | Skipped: no business, authorization or persistence implementation changes; existing full journeys retained |

### Commands and results

Frontend unit/build ran in a temporary whitelist snapshot of current sources,
tests and configs, on Node 22.18.0. Repository dotenv files were never opened or
copied. Only synthetic endpoint settings and dummy public provider settings were
created in that snapshot. Production Dockerfile was built from the same safe
snapshot; outer Nginx used the repository configuration.

| Command | Local result |
|---|---|
| `npm ci --ignore-scripts --prefer-offline --no-audit --no-fund` in snapshot | Passed; optional native binaries support unit/build without install scripts |
| `npm run test:unit` in snapshot | **70/70**, 9 files, final run 1.30s |
| `VITE_STRIPE_PUBLISHABLE_KEY=pk_test_local_performance_dummy VITE_CLOUDINARY_CLOUD_NAME=local-performance-dummy VITE_CLOUDINARY_UPLOAD_PRESET=local-performance-dummy npm run build` in snapshot | Passed, Vite 7.1.7, 774 modules, 7.21s |
| `npm run test:nginx` in frontend | Passed with `nginx:alpine nginx -t` |
| `dotnet test backend/tests/Contract/IoBuild.Contract.Tests.csproj --no-restore --verbosity minimal` | **20/20**, 0 skipped, about 1s test execution |
| `docker compose --env-file <temp>/perf-empty.env -p surgical-perf-20260929 -f <temp>/perf-compose.yml up -d --build` | Passed; project-owned MySQL volume/containers; port 18081 |
| `E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_SIMULATED_PAYMENTS=1 npx playwright test --workers=1 --repeat-each=2 ...` before explicit dummy-fixture flag was added | **44/44**, 22 scenarios × 2, 2.4min; 13 existing scenarios + 9 performance scenarios, no retries |
| `E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_CLOUDINARY_DUMMY=1 E2E_SIMULATED_PAYMENTS=1 npx playwright test --workers=1 --reporter=list --output="C:/Users/halli/AppData/Local/Temp/opencode/perf-playwright-final-flags"` | **22/22**, 0 skipped, 1.2min; final versioned tests with explicit fixture flag |

The final dummy-fixture flag is `E2E_CLOUDINARY_DUMMY=1`; it must be set only when
the built frontend uses `local-performance-dummy` for cloud name and upload
preset. Headers require `E2E_NGINX=1`. The extra no-SDK URL-checkout assertion
requires `E2E_SIMULATED_PAYMENTS=1`. These are explicit environment prerequisites,
not blanket CI pass claims.

### Comparable Lighthouse lab evidence

Lighthouse **12.8.2**, installed Chrome at
`C:/Program Files/Google/Chrome/Application/chrome.exe`, fresh audit storage,
default simulated mobile throttling or `--preset=desktop`, same outer Nginx
origin **http://127.0.0.1:18081**, same anonymous routes. All audits ran serially
without concurrent tests/builds. Four BEFORE audits completed before repo edits.
Earlier Vite-preview login numbers were excluded from the comparison.

Command shape:

```sh
CHROME_PATH="C:/Program Files/Google/Chrome/Application/chrome.exe" npx --yes lighthouse@12.8.2 http://127.0.0.1:18081/iam/login --chrome-flags="--headless --disable-gpu --no-sandbox" --output=json --output=html --output-path="<temp>/perf-before-login-mobile" --quiet
```

For Owner use `/iam/register-owner`; desktop adds `--preset=desktop`. AFTER uses
the same commands with `perf-after-*` output paths.

| Route/profile | P/A/BP/SEO before → after | LCP before → after | Transferred before → after |
|---|---|---|---|
| Login mobile | 50/100/79/82 → **81/100/100/100** | 15.32s → **3.91s** | 2.30MB → 0.48MB |
| Login desktop | 74/100/78/82 → **99/100/100/100** | 2.73s → **0.87s** | 2.30MB → 0.49MB |
| Register Owner mobile | 47/90/68/82 → **75/90/89/100** | 16.14s → **4.36s** | 2.33MB → 0.49MB |
| Register Owner desktop | 74/90/89/82 → **99/90/89/100** | 2.85s → **0.82s** | 2.33MB → 0.49MB |

All eight final comparison reports have empty `runWarnings`, no runtime error
and no recorded console errors. This is one observation per route/profile and
phase, not a statistical median or field CWV. External CDN/font timings and
local CPU vary; small differences must not be treated as precise causal effects.
An intermediate four-audit set without hero preload was preserved (mobile login
P75/LCP4.75s; Owner P73/LCP4.73s). Its 62–70% image discovery delay justified
preloading the exact existing image only on direct auth entries. No resize,
replacement or CSS design change was made.

Bundle findings: charts 185.68KB and Stripe 3.18KB are no longer requested on
auth. Core vendor is 194.97KB; **PrimeVue remains 914.43KB initial JS**. CSS remains
358.96KB uncompressed, now about 36.15KB gzip. Renaming/splitting vendor must not
be misreported as a reduction of all JavaScript by that same amount.

### Gate status and diagnostics

| Gate | Local scoped evidence | Delivery/CI status |
|---|---|---|
| G0 | passed — 70 unit tests | skipped — CI not run |
| G1 | passed — 20 API contracts, Nginx parser, headers and bodies | skipped — CI not run |
| G2 | passed — complete built-app/MySQL suite, both roles | skipped — CI not run |
| G3 | passed — applicable A/B/C cases above | skipped — CI and real provider campaign not run |
| G4 | passed — deterministic repeated E2E; versioned tests, artifacts | skipped — CI not run |

Failure evidence was retained outside the repo; no expectations were relaxed:

- Product/config: unquoted regex `{8,}` broke Nginx parsing, causing 502. Quoted
  the location pattern and added the versioned real-parser check.
- Product/bundling: `@primevue/core` in vendor created a cross-chunk initialization
  cycle (`Cannot read properties of undefined (reading 'extend')`). Kept
  `primevue`, `@primevue/*` and `@primeuix/*` together; unit and built-app runtime
  regressions cover this. Windows worker crashes in the first two runs occurred
  when terminal deadlines interrupted suites already blocked by these failures.
- Test contract: widget config uses `cloud_name`/`upload_preset`, not `cloudName`;
  corrected the assertion to verify existing credentials and cropping policy.
- Test fixture: anonymous unknown-route entry correctly redirected to login and
  fetched its CSS background. Used a real Builder session and `/projects` to
  prove no auth image request on a genuine non-auth page.
- Environment: host npm `ESTRICTALLOWSCRIPTS` blocked initial installation;
  `--ignore-scripts` solved setup without changing global npm policy or assertions.

### Artifacts, rollback and owned follow-up

Artifacts remain in `C:/Users/halli/AppData/Local/Temp/opencode/`:
`perf-{before,intermediate,after}-{login,owner}-{mobile,desktop}.report.{json,html}`,
and `perf-playwright-*` failure/acceptance evidence. Only the owned Compose stack,
volume and temporary override are cleaned; existing containers/volumes and prior
`lighthouse-*.json` reports are preserved.

Rollback units: (1) frontend Dockerfile/Nginx config/parser test; (2) Vite chunk
matcher plus CDN removal; (3) shared Cloudinary loader and both registration
callers; (4) Stripe client and subscription caller; (5) description/robots/auth
preload. Tests/docs stay with the behavior unit they verify. No commits/push.

| Open risk / deferred work | Why deferred | Owner | Review/resume |
|---|---|---|---|
| Run CI and deployed-origin Lighthouse/field monitoring | This session is explicitly local and uncommitted | ccarita-tech | 2026-10-06, after publication is authorized |
| Initial PrimeVue/CSS size; mobile LCP still above 2.5s | Global component/CSS registration changes exceed surgical scope | ccarita-tech | 2026-10-06 |
| Real Cloudinary upload, Stripe SDK session-ID fallback and real checkout | Dummy public settings; Cloudinary browser boundary mocked, Stripe URL payment simulated; unit SDK proof is not provider acceptance | ccarita-tech | 2026-10-06, in approved provider test environment |
| Existing Owner A90/BP89 | Preserve UI/forms; no accessibility redesign in this change | ccarita-tech | 2026-10-06 |
| Existing Docker Node20/Vitest5 engine warning and backend NU1902/EF1002 warnings | Builds/contracts pass; dependency/security changes not part of this scope | ccarita-tech | 2026-10-06 |

## Remaining-screen local Lighthouse audit — 2026-09-29

**Outcome:** 13 route-role screens, **26/26 accepted mobile/desktop reports**,
zero final blocked measurements. These are new observations of the current
optimized frontend, with no before/after claim for previously unmeasured routes.
Application code, auth, development settings and existing local changes were
not modified by this audit. Only this ledger and context-reference prose changed.

### Route catalog, actors and acceptance

CodeGraph was consulted first for routes/roles; route config files supplied
details not returned by its capped results. Canonical paths come from
`frontend/src/router.js` and each context's `*-routes.js`, not guessed names.
Each accepted report has the requested final URL, a visible screen-specific
selector and, where applicable, actual fixture content. The harness checks the
Lighthouse-rendered tab itself after auditing, not merely a preflight tab.

| Context | Actor | Canonical route / screen | Rendered evidence |
|---|---|---|---|
| IAM | Anonymous | `/iam/register-builder`, initial step | `.auth-container #email` |
| Analytics | Builder | `/analytics/dashboard` | `.builder-dashboard .stats-grid` |
| Analytics | Owner | `/analytics/dashboard` | `.owner-dashboard .stats-grid` |
| Publishing | Builder | `/projects`, populated list | `.project-grid`, fixture project name |
| Publishing | Builder | `/projects/new`, creation form | `.form-container .form-card` |
| Publishing | Builder | `/projects/1`, structured detail | `.project-details-root .structure-floors`, fixture project name |
| Publishing | Builder | `/clients`, populated list | `.p-datatable`, fixture client name |
| Publishing | Builder | `/clients/1`, assigned client detail | `.client-profile-card h1`, fixture client name |
| Profiles | Builder | `/profiles/profile` | `.profile-name`, Builder profile name |
| Profiles | Owner | `/profiles/profile` | `.profile-name`, Owner profile name |
| Devices | Builder | `/devices/device-management` | `.device-management-view .p-datatable`, fixture device name |
| Devices | Owner | `/devices/device-management` | `.unit-devices-table`, fixture device name |
| Subscriptions | Builder | `/subscriptions/my-subscription`, active plan and plan catalog | `.hero-plan-title`; separate diagnostic verifies `.plans-grid` and all three plans |

Plans have **no separate route**: Starter, Professional and Enterprise are
embedded in my-subscription. The active Starter state and embedded catalog
therefore share one navigation measurement per profile, not two duplicate audits.
IDs `1` are actual IDs from this new isolated database.

Additional catalog entries are documented but not assigned Lighthouse scores:
`/projects/:id/edit` (edit state), `/analytics/owner/devices` (Owner-only
dashboard component), device alias `/devices/devices`, plus login/register-owner
already measured historically.
No claim is made for dialogs, second registration steps, checkout transitions,
empty/error states, all tenants or all pages accessible by manual URL entry.
Projects/clients/subscriptions navigation is Builder-oriented; their route
configs do not declare a Builder-only frontend role gate. This audit does not
prove Owner access/denial for those routes.

Observed redirect diagnostics for **both roles**:

- Authenticated `/iam/login` lands on `/analytics/dashboard`, showing that role's
  own dashboard; `/home` is the configured redirect to that dashboard.
- `/profiles` lands on `/profiles/profile`; `/devices` lands on
  `/devices/device-management`.
- **Existing route defects:** `/analytics` lands on `/dashboard`, and
  `/subscriptions` lands on `/my-subscription`; both miss their configured
  nested canonical paths and show no dashboard. These diagnostic destinations
  receive no performance score. Canonical measured paths render correctly.
- `/analytics/owner/devices` renders the Owner dashboard for Owner; Builder is
  redirected to `/analytics/dashboard`. Builder subscription checks remain
  active and use its real active subscription; no guard was bypassed.

### Isolated dataset and measurement policy

Stack: new project `perf-private-20260929`, outer Nginx on
`http://127.0.0.1:18081`, production frontend Dockerfile + current frontend
Nginx config, current backend source and MySQL **8.0**. Source snapshots use an
explicit frontend whitelist and backend code/project-file extensions; no
checkout dotenv or backend appsettings was read/copied. Snapshot backend
`appsettings.json` is synthesized as `{}`; configuration comes from local
fixture environment settings. Nothing touches `appsettings.Development.json`,
`.atl/` or `.codegraph/`. Historical images/reports were not used as baselines.

Fixture API flow: users → sessions → profiles → simulated checkout/confirmation
→ project → units → clients/invitation → Owner registration/session/profile →
Owner custom device. Sessions are genuinely issued by the unchanged IAM API,
kept in memory and injected into same-origin `token`/`currentUser` localStorage.
There are no fabricated JWTs or authorization interceptions. Dataset:

- 1 Builder + 1 assigned Owner, each with a profile, no profile photo.
- 1 active Starter subscription confirmed through real APIs in simulated mode;
  3 seeded plans, no real Stripe/Cloudinary calls.
- 1 project with no hero image, 3 units over 2 floors; 1 assigned and 2 free.
- 2 clients: one assigned active client and one unassigned stand-by client.
- 1 Owner-created SmartLight linked to the assigned unit. No MQTT simulator or
  Influx samples: persisted/empty live-telemetry states only, not live IoT load.

Lighthouse **12.8.2**, Node **22.18.0**, Playwright **1.63.0**, Chrome
**154.0.0.0** at `C:/Program Files/Google/Chrome/Application/chrome.exe`;
Docker **29.5.3**, Compose **5.1.4**. Audits run serially, mobile then desktop,
without concurrent builds/tests. Mobile uses Lighthouse's default simulated
throttling; desktop uses its `desktopConfig`. One accepted observation per
screen/profile, not a median, field CWV, INP measurement or production result.

**Cold authenticated navigation policy:** `disableStorageReset: true` preserves
auth localStorage. It does not establish a cold network cache by itself. Before
each audit the harness navigates the preflight tab to `about:blank`, explicitly
sends CDP `Network.clearBrowserCache`, and audits a dedicated Puppeteer page.
Thus browser HTTP cache is cold while auth storage remains intact. No login
network/typing cost is included. Host DNS/TLS/OS/server caches may remain warm;
Google Fonts/CDN timings and local CPU vary. Repeat visits with immutable assets
cached are not represented. The dedicated page is blanked between audits.

### Measurements

P/A/BP/SEO are 0–100. FCP/LCP are seconds, TBT milliseconds, CLS unitless;
bytes are Lighthouse `total-byte-weight` (transferred resources, not bundle
source size). All final reports have **null runtimeError, empty runWarnings and
no observed failed API/HTTP requests**. The registration diagnostic exception
below remains part of the evidence, despite a valid rendered screen.

| Context | Role | Route | Profile | P/A/BP/SEO | FCP s | LCP s | TBT ms | CLS | Bytes |
|---|---|---|---|---|---:|---:|---:|---:|---:|
| IAM | Anonymous | `/iam/register-builder` | mobile | 72/90/89/100 | 3.37 | 4.16 | 302 | 0.0000 | 488420 |
| IAM | Anonymous | `/iam/register-builder` | desktop | 99/90/89/100 | 0.69 | 0.88 | 14 | 0.0014 | 493138 |
| Analytics | Builder | `/analytics/dashboard` | mobile | 63/90/100/100 | 3.52 | 5.20 | 396 | 0.0001 | 518467 |
| Analytics | Builder | `/analytics/dashboard` | desktop | 96/90/100/100 | 0.85 | 1.26 | 5 | 0.0002 | 518452 |
| Analytics | Owner | `/analytics/dashboard` | mobile | 72/90/100/100 | 3.71 | 4.84 | 162 | 0.0000 | 515256 |
| Analytics | Owner | `/analytics/dashboard` | desktop | 98/90/100/100 | 0.80 | 1.05 | 5 | 0.0002 | 515259 |
| Publishing | Builder | `/projects` | mobile | 76/88/100/100 | 3.65 | 4.42 | 104 | 0.0005 | 443938 |
| Publishing | Builder | `/projects` | desktop | 98/88/100/100 | 0.77 | 1.03 | 5 | 0.0038 | 443947 |
| Publishing | Builder | `/projects/new` | mobile | 72/90/100/100 | 3.67 | 5.04 | 126 | 0.0114 | 462000 |
| Publishing | Builder | `/projects/new` | desktop | 95/90/100/100 | 0.81 | 1.38 | 4 | 0.0055 | 461978 |
| Publishing | Builder | `/projects/1` | mobile | 72/91/100/100 | 3.43 | 4.68 | 224 | 0.0000 | 453556 |
| Publishing | Builder | `/projects/1` | desktop | 99/91/100/100 | 0.78 | 0.86 | 3 | 0.0002 | 453533 |
| Publishing | Builder | `/clients` | mobile | 80/91/100/100 | 3.40 | 4.07 | 53 | 0.0262 | 448943 |
| Publishing | Builder | `/clients` | desktop | 99/91/100/100 | 0.79 | 0.88 | 14 | 0.0089 | 448950 |
| Publishing | Builder | `/clients/1` | mobile | 73/88/100/100 | 3.67 | 4.66 | 160 | 0.0000 | 450173 |
| Publishing | Builder | `/clients/1` | desktop | 99/88/100/100 | 0.79 | 0.88 | 0 | 0.0012 | 450142 |
| Profiles | Builder | `/profiles/profile` | mobile | 79/83/100/100 | 3.36 | 3.97 | 137 | 0.0000 | 438057 |
| Profiles | Builder | `/profiles/profile` | desktop | 99/83/100/100 | 0.76 | 0.81 | 19 | 0.0002 | 438034 |
| Profiles | Owner | `/profiles/profile` | mobile | 81/83/100/100 | 3.41 | 3.56 | 180 | 0.0000 | 436986 |
| Profiles | Owner | `/profiles/profile` | desktop | 99/83/100/100 | 0.74 | 0.78 | 0 | 0.0002 | 437002 |
| Devices | Builder | `/devices/device-management` | mobile | 65/91/100/100 | 3.37 | 4.00 | 555 | 0.0645 | 440176 |
| Devices | Builder | `/devices/device-management` | desktop | 97/91/100/100 | 0.68 | 1.01 | 96 | 0.0047 | 440208 |
| Devices | Owner | `/devices/device-management` | mobile | 69/88/100/100 | 3.51 | 3.67 | 500 | 0.0002 | 439921 |
| Devices | Owner | `/devices/device-management` | desktop | 99/88/100/100 | 0.69 | 0.75 | 12 | 0.0003 | 439905 |
| Subscriptions | Builder | `/subscriptions/my-subscription` | mobile | 74/91/100/100 | 3.72 | 4.49 | 78 | 0.0171 | 452127 |
| Subscriptions | Builder | `/subscriptions/my-subscription` | desktop | 99/91/100/100 | 0.76 | 0.93 | 20 | 0.0149 | 452150 |

### Opportunities and accessibility by screen

Mobile estimates below are diagnostic, non-additive and not promised savings.
Every screen flags roughly **36 KiB unused CSS** in `vendor-DYoCBEyJ.css`.
The per-profile full JSON/HTML retains item-level nodes/resources and additional
insights (font/cache/image delivery, forced reflow and dependency trees).
Accessibility failures are automated findings, not a full accessibility audit.

| Artifact screen stem | Unused JS KiB | Render-blocking estimate ms | Failed accessibility audit IDs (mobile and desktop) |
|---|---:|---:|---|
| register-builder | 134 | 600 | `aria-required-children`, `color-contrast` |
| analytics-builder | 153 | 600 | `button-name`, `color-contrast` |
| analytics-owner | 186 | 600 | `button-name`, `color-contrast` |
| projects-list | 130 | 600 | `button-name`, `color-contrast`, `heading-order` |
| projects-create | 130 | 600 | `button-name`, `color-contrast` |
| projects-detail | 127 | 600 | `button-name`, `color-contrast` |
| clients-list | 112 | 750 | `button-name`, `color-contrast` |
| clients-detail | 127 | 600 | `button-name`, `color-contrast`, `heading-order` |
| profile-builder | 129 | 750 | `button-name`, `color-contrast`, `label`, `select-name` |
| profile-owner | 129 | 600 | `button-name`, `color-contrast`, `label`, `select-name` |
| devices-builder | 116 | 600 | `button-name`, `color-contrast` |
| devices-owner | 119 | 750 | `button-name`, `color-contrast`, `label` |
| my-subscription | 130 | 300 | `button-name`, `color-contrast` |

Priorities, based on measured evidence rather than an implemented fix:

1. **Shared delivery/main-thread cost:** mobile FCP 3.36–3.72s and LCP
   3.56–5.20s. Lighthouse identifies PrimeVue + core vendor unused JS; Analytics
   also includes unused Chart.js bytes. Builder analytics lists PrimeVue
   193098 transferred bytes / 104067 unused bytes, vendor 26774 unused and
   charts 25854 unused. Shared CSS is ~99.7–99.9% unused in the inspected
   navigation coverage; Google Fonts + shared CSS are render-blocking. This
   supports investigation of initial component/CSS delivery, not blanket removal
   of apparently unused styles needed by later interactions.
2. **Mobile Devices blocking:** TBT Builder 555ms / Owner 500ms. Builder Devices
   main-thread breakdown shows ~932ms script evaluation; Analytics Builder
   ~1208ms. A single lab observation does not isolate one component as the cause.
3. **Accessibility:** Profiles score 83 for both roles (labels/select names,
   unnamed buttons and contrast); projects/client detail add heading-order
   findings. Desktop P95–99 does not resolve these issues.
4. **Routing and registration diagnostics:** unresolved base-route redirects
   above, and `TypeError: Cannot read properties of undefined (reading 'getData')`
   at PrimeVue `Proxy.onPaste` during both register-builder Lighthouse runs.
   The form still renders; this exception is observed by Playwright even though
   Lighthouse `errors-in-console` is empty. It is associated with Lighthouse's
   paste diagnostic, not evidence of failure of a real user's registration.

No failed API requests were observed during the accepted audits. Approximate
2 KiB text-compression opportunities on Builder analytics/devices include small
API data; this is not evidence that the optimized static gzip policy failed.

### Commands, rejected harness run and artifacts

All scripts/artifacts are local in:
`C:/Users/halli/AppData/Local/Temp/opencode/perf-private-20260929/` (`T` below).
Setup script is one level above, `perf-private-setup.mjs`.

```sh
node "C:/Users/halli/AppData/Local/Temp/opencode/perf-private-setup.mjs"
docker compose --env-file "$T/empty.env" -p perf-private-20260929 -f "$T/compose.yml" up -d --build
npm install --prefix "$T" --ignore-scripts --no-audit --no-fund lighthouse@12.8.2 playwright@1.63.0
node "$T/audit.mjs"
node "$T/audit.mjs" --resume
node "$T/summarize.mjs"
node "$T/diagnostics.mjs"
node "$T/verify-docs.mjs"
docker compose --env-file "$T/empty.env" -p perf-private-20260929 -f "$T/compose.yml" down --volumes --rmi local
git diff --check
```

API call shape is `lighthouse(url, {port:19229, disableStorageReset:true,
output:['json','html'], onlyCategories:['performance','accessibility',
'best-practices','seo']}, desktopConfigOrUndefined, dedicatedPuppeteerPage)`.
Context7's authenticated Lighthouse recipe was consulted; installed 12.8.2's
Node API confirms explicit page support.

The first harness attempt rejected **24 audits** because Lighthouse closed its
own automatically-created tab before post-run selector validation. Builder
Devices preflight also rejected the wrong fixture text (project name instead of
device name). Classification: **harness**, not product render failure. Evidence
is retained in `first-harness-failure.json`; no score from that attempt is
accepted. The corrected harness passes an explicit dedicated Puppeteer page and
checks the actual device fixture in the populated table. A serial rerun produced
all 26 valid reports without altering application behavior or auth.

Artifacts:

- `perf-private-<screen-stem>-{mobile,desktop}.report.{json,html}`: **52 sanitized
  files**; screen stems are enumerated in the opportunities table.
- `summary.json`: all scores/metrics, rendered URL/selector, per-screen
  opportunities/a11y, runtime errors/warnings, console and HTTP diagnostics.
- `seed-summary.json`, `catalog.json`, `route-diagnostics.json`,
  `perf-private-subscriptions-plans.png`, `sanitization-check.json`.
- `audit.mjs`, `diagnostics.mjs`, `summarize.mjs`, `compose.yml`, safe source
snapshots and `first-harness-failure.json` for reproduction/diagnosis.

`verify-docs.mjs` validated **13 local links/anchors**, historical YAML equality
against HEAD in all six context ledgers, and **322 snapshot application files**
matching the current checkout byte-for-byte. `git diff --check` passed; existing
LF/CRLF conversion notices are not whitespace errors. Cleanup checks found no
remaining containers or volumes labeled with this audit's Compose project.

Reports are inspected for JWT/bearer values, sensitive URL parameters and
sensitive-key dumps; scan of all 52 JSON/HTML files found no secrets. Tokens and
session responses are never written to evidence, repo or console; raw request
headers are not saved. The owned Chrome profile containing localStorage was
deleted after diagnostics. Only this audit's four containers, network, MySQL
volume and two image tags were removed; historical reports/images remain.

### Delivery limits and owned follow-up

| Gate / evidence | Result | Limit |
|---|---|---|
| Local Lighthouse navigation | 26/26 valid observations | One cold-navigation lab observation each; no statistical or field acceptance |
| URL/role/render and report sanitization checks | Passed locally | Diagnostic harness lives in Temp, not a versioned CI acceptance test |
| G0/G1/G2/G3 product regression gates | Skipped in this audit | No behavior change; prior versioned evidence remains separate; browser diagnostics do not replace it |
| G4 / CI / deterministic multi-run performance campaign | Skipped | Local audit only, not repeated distribution or CI delivery acceptance |
| Documentation diff/link checks and owned cleanup | Passed locally | Historical dated performance section and context YAML preserved |

| Open risk / follow-up | Why deferred | Owner | Review/resume |
|---|---|---|---|
| Shared JS/CSS/Fonts and mobile Devices TBT investigation | Audit-only instruction; no optimization authorized | ccarita-tech | 2026-10-06 |
| Profiles/other screen accessibility and registration paste exception | Audit-only instruction; requires separate behavior/design evidence | ccarita-tech | 2026-10-06 |
| `/analytics` and `/subscriptions` base redirects | Canonical paths measured; fixing router behavior outside this audit | ccarita-tech | 2026-10-06 |
| Large tenants, real photos/live telemetry, transitions, deployed-origin and field INP | Small representative fixture, simulated providers and navigation-only scope | ccarita-tech | 2026-10-06, approved environment |

## Surgical round-two fixes and local convergence — 2026-09-29

**Outcome:** scoped routing/accessibility patches pass local functional
convergence: **72 unit tests, 20 backend contracts, Nginx parser, and the entire
28-scenario built-app E2E suite twice (56/56, no retries)**. CI is **skipped**.
Profiles for Builder/Owner, Owner Devices and both registration forms now have
Lighthouse **Accessibility 100** in the measured states. No general performance
speedup, production acceptance or all-state accessibility claim is made.
Previous dated evidence above and all context YAML histories remain unchanged.

### Journeys, actors and risk ownership

Journeys: `Anonymous registration → validated Next → native clipboard input →
Back/Next with retained values`; `Builder/Owner session → base URL or real menu
→ correct dashboard → labeled profile → save → full reload → locale change`;
`assigned Owner → named device controls → command → persisted status / power
lock`. Backend authorization, subscription gates and telemetry freshness remain
authoritative and unchanged.

| Actor / boundary | Observable acceptance | Owning evidence |
|---|---|---|
| Builder + Owner, frontend router | `/analytics` resolves to `/analytics/dashboard`; no catch-all page; actual menu profile/home links work | `canonical-routes.test.js` G0; both-role `performance-round2.spec.js` G2 convergence |
| Builder, subscription gate/API | `/subscriptions` resolves to `/subscriptions/my-subscription`; active plan/catalog remain visible and real menu reaches them | Route unit + real API/MySQL E2E; existing purchase scenario |
| Builder + Owner, Profiles/API/persistence | Five fields have connected labels, native language selector has a name; saving survives reload; Spanish labels still name controls | Both-role round-two E2E + existing profile journeys |
| Anonymous Builder + invited Owner, IAM | Header-only tablist, selected state on tabs, no panel inside tablist; genuine clipboard data sets age; step data retained; eligibility unchanged | Round-two registration E2E for both roles + existing IAM fail-closed tests |
| Owner, Devices/API | Numeric/dropdown controls have device/attribute names; command, power lock/unlock and confirmed status still work | Extended `devices-control.spec.js` full journey |
| Shared header/theme/Fonts | Named menu/notifications/language control; measured language/logout/profile-action contrast ≥4.5; preconnects present without lazy CSS | Browser computed-color assertions + head assertions; Lighthouse diagnostic evidence |

Affected boundaries: Vue Router, Vue templates and PrimeVue pass-through/theme
tokens; browser Fonts connection setup; unchanged browser → outer Nginx →
frontend Nginx/API → MySQL. No backend/schema/provider implementation changes.

| Tier | What-if / regression | Cheapest trustworthy layer |
|---|---|---|
| A | Relative redirect escapes parent; paid Builder navigation or Owner dashboard breaks | Memory-router G0 + direct navigation and real menu G2 for both roles |
| A | Profile save/locale, Owner commands, auth eligibility or prior lazy SDK delivery regress | Full existing real-stack E2E suite, including all previous performance tests |
| B | Labels name wrong field; header/selected tab semantics incorrect; palette shade still fails contrast | Actual rendered label/name/selected-state/color assertions + scoped Lighthouse |
| B | Clipboard/panel transition loses data or throws on representative paste | DataTransfer-backed ClipboardEvent regression for both registrations |
| C | Font scheduling changes cause styling flash or extra SDK requests | Keep existing blocking stylesheet/font family/weights and `display=swap`; only add preconnect; prior no-SDK tests retained |
| D | Large tenants, real provider/telemetry campaign, field INP | Skipped: small fixture and local scope; owned follow-up below |

### Changes and diagnostic verdicts

- `analytics-routes.js`, `subscriptions-routes.js`: relative redirects were
  resolved outside their parent. Absolute canonical paths fix direct entry;
  route unit tests and real menu tests preserve names/guards.
- Both Profiles views: `label for` + native input IDs, named language select and
  camera action. Shared layout names menu/notifications; shared language control
  uses the existing translated language label. Owner device range/dropdown gets
  a device-and-attribute accessible name. Widgets and commands are retained.
- `theme.js` + `main.js`, existing CSS: keep Aura/emerald palette, use emerald
  700/800/900 for primary action text/fill states; local profile green/red buttons
  and shared language/logout shades meet measured contrast. No layout, images,
  font-family or component registry redesign.
- Both registration forms: move `tablist` from the Stepper (which contains panels)
  to StepList; wrapper becomes `group`. PrimeVue's Step also placed
  `aria-current` on a `role=presentation` wrapper, invalidating presentation
  semantics for axe. Pass-through clears that root state and supplies
  `aria-selected` on the real tab header. CSS/data attributes, validation,
  disabled progression and form state remain intact. Four final registration
  reports have no `aria-required-children` failure.
- `index.html`: Google Fonts API/static-origin preconnect, crossorigin on the
  font origin; existing synchronous stylesheet, weights and swap policy remain.
  No claim of reduced CSS/vendor bytes: CSS still **358.96KB / 36.15KB gzip**,
  PrimeVue **914.50KB / 191.13KB gzip** (before 914.43KB / 191.11KB). Removing
  global components, rewriting chunk boundaries or lazy-loading essential CSS
  was not judged low-risk given the prior cross-chunk initialization issue.

**Paste verdict — dependency/diagnostic input, not an own-app fix:** before any
patch, `clipboard-diagnostic.mjs` reproduced `getData` in PrimeVue InputNumber
on the mounted hidden age input. A ClipboardEvent with real DataTransfer text
`30` succeeds with no error; Lighthouse 12.8.2's Inputs gatherer instead sends
`new ClipboardEvent('paste', {cancelable:true})` with null clipboardData.
InputNumber prevents default for numeric parsing then dereferences clipboardData.
No generic error catcher, event suppression, control replacement or dependency
upgrade was added. The synthetic exception and BP89 remain in both registration
reports; genuine paste is covered by versioned passing tests for both roles.

**Failure evidence:** five prepatch regressions failed as expected (both-role
wrong analytics destination, both-role missing profile labels, invalid tablist).
First full postpatch run was 50/54: all four failures were a test-contract error,
requesting a menu `href=/profiles` while the rendered menu and `ROUTES.PROFILES`
already use `/profiles/profile`. Fixed that locator after inspecting the saved
DOM/screenshot; canonical URL and rendered-name assertions were not weakened.
The first semantic patch exposed a remaining Step wrapper ARIA problem; the
second scoped PT correction removed it. Final full repeat is 56/56.

### Comparable Lighthouse and noise evidence

Fresh prepatch snapshot/image from **this round**, actual same current optimized
sources before these patches; not the older remaining-screen numbers. Baseline
frontend image ID begins `3fc3ba90b37c`; baseline noise rerun reuses that exact
frozen image. Stack: current production Dockerfiles/Nginx, same origin
`http://127.0.0.1:18081`, API and fresh owned MySQL8 databases re-seeded with
identical small fixtures before each comparison campaign. Real API sessions
match each role/origin; auth storage preserved via `disableStorageReset:true`
while CDP `Network.clearBrowserCache` explicitly resets HTTP cache before every
serial audit. Dedicated Puppeteer page permits actual post-audit URL/selector
checks. No simultaneous tests/builds during Lighthouse.

Same fixture as the prior audit: 2 accounts/profiles; active Starter via simulated
real checkout confirmation; 1 project, 3 units/2 floors, 2 clients/1 assigned
Owner, no photos/live telemetry. **1 explicit custom SmartLight is not the total
registry**: unit provisioning/default seeding also creates devices; GET devices
reports **13 registry rows** in the fresh fixture. Existing defaults are preserved.
Functional E2E creates separate actors after the main measurement campaign.
Final registration-only remeasure is anonymous, after the last semantic patch.

Tools: Lighthouse **12.8.2**, Chrome **154.0.0.0** at the installed path, Node
**22.18.0**, Playwright **1.63.0**, Vite **7.1.7**, Docker **29.5.3**, Compose
**5.1.4**. Default simulated mobile and Lighthouse desktopConfig. One comparable
observation per profile plus baseline noise samples; not statistical medians,
field CWV or INP. External Fonts/CDN, host CPU and OS/server caches vary.

Table: P and A are scores; LCP seconds; TBT milliseconds. Full JSON retains
FCP/CLS/bytes, BP/SEO, opportunities and runtime diagnostics. Login/Owner baseline
rows come from the frozen-image noise campaign, not historical measurements.

| Screen stem | Profile | P before → after | A before → after | LCP s before → after | TBT ms before → after |
|---|---|---|---|---|---|
| register-builder | mobile | 77 → 75 | 90 → 100 | 4.22 → 4.13 | 184 → 237 |
| register-builder | desktop | 98 → 98 | 90 → 100 | 0.98 → 0.96 | 14 → 25 |
| analytics-builder | mobile | 71 → 67 | 90 → 96 | 5.02 → 5.22 | 208 → 312 |
| analytics-builder | desktop | 92 → 93 | 90 → 96 | 1.37 → 1.31 | 12 → 0 |
| analytics-owner | mobile | 76 → 74 | 90 → 96 | 4.39 → 4.50 | 111 → 159 |
| analytics-owner | desktop | 98 → 97 | 90 → 96 | 1.05 → 1.00 | 6 → 0 |
| projects-list | mobile | 76 → 77 | 88 → 94 | 4.12 → 4.10 | 186 → 132 |
| projects-list | desktop | 98 → 98 | 88 → 94 | 0.99 → 1.01 | 8 → 5 |
| projects-create | mobile | 81 → 76 | 90 → 96 | 3.85 → 4.31 | 18 → 101 |
| projects-create | desktop | 95 → 97 | 90 → 96 | 1.41 → 1.21 | 10 → 4 |
| projects-detail | mobile | 81 → 76 | 91 → 91 | 4.07 → 4.23 | 51 → 183 |
| projects-detail | desktop | 99 → 99 | 91 → 91 | 0.86 → 0.88 | 3 → 8 |
| clients-list | mobile | 74 → 70 | 91 → 91 | 4.50 → 4.02 | 158 → 376 |
| clients-list | desktop | 99 → 98 | 91 → 91 | 0.87 → 0.90 | 0 → 0 |
| clients-detail | mobile | 78 → 77 | 88 → 98 | 4.37 → 4.29 | 29 → 150 |
| clients-detail | desktop | 89 → 99 | 88 → 98 | 1.45 → 0.88 | 0 → 1 |
| profile-builder | mobile | 79 → 76 | 83 → 100 | 3.97 → 4.08 | 135 → 251 |
| profile-builder | desktop | 99 → 99 | 83 → 100 | 0.79 → 0.82 | 0 → 5 |
| profile-owner | mobile | 86 → 86 | 83 → 100 | 3.32 → 3.33 | 0 → 0 |
| profile-owner | desktop | 99 → 99 | 83 → 100 | 0.77 → 0.78 | 0 → 29 |
| devices-builder | mobile | 75 → 69 | 91 → 91 | 3.70 → 3.96 | 317 → 453 |
| devices-builder | desktop | 98 → 99 | 91 → 91 | 0.99 → 0.81 | 22 → 3 |
| devices-owner | mobile | 80 → 77 | 88 → 100 | 3.53 → 3.57 | 203 → 307 |
| devices-owner | desktop | 99 → 90 | 88 → 100 | 0.77 → 1.46 | 6 → 1 |
| my-subscription | mobile | 76 → 75 | 91 → 96 | 4.24 → 4.26 | 161 → 204 |
| my-subscription | desktop | 99 → 99 | 91 → 96 | 0.88 → 0.83 | 0 → 0 |
| login | mobile | 78 → 77 | 100 → 100 | 4.14 → 4.16 | 144 → 155 |
| login | desktop | 99 → 98 | 100 → 100 | 0.91 → 0.97 | 4 → 6 |
| register-owner | mobile | 66 → 77 | 90 → 100 | 4.49 → 3.92 | 453 → 225 |
| register-owner | desktop | 98 → 99 | 90 → 100 | 0.96 → 0.90 | 22 → 15 |

**Do not infer a performance improvement from this table.** Some timings/scores
are worse. Identical baseline-image repeats demonstrate substantial noise:
Builder Devices mobile P75→59 / TBT317→710ms / LCP3.70→4.41s; Builder Analytics
P71→66 / TBT208→334ms; Builder Profile P79→79 / TBT135→152ms. This does not prove
every difference is noise; it prevents attributing one run to the small patches.
The preconnect benefit is unproven in these observations. No polling, retries,
shadow freshness or dashboard fetch behavior was weakened for a higher score.
Main-thread/vendor optimization remains owned follow-up.

### Commands, gates and artifacts

Run safe snapshot/tools from the checkout; run npm/build/E2E from its Temp
frontend. `T=C:/Users/halli/AppData/Local/Temp/opencode/perf-surgical-round2-20260929`.
Snapshots whitelist source/tests/config names, synthesize public dummy settings
and backend `{}` config, exclude checkout dotenv/backend appsettings, bin/obj
and caches. Existing local settings, `.atl/`, `.codegraph/`, previous uncommitted
optimizations and Git branches/remotes were not changed. No commit/push.

```sh
node scripts/performance-snapshot.mjs "$T"
docker compose --env-file "$T/empty.env" -p perf-surgical-round2-20260929 -f "$T/compose.yml" up -d --build
npm install --prefix "$T" --ignore-scripts --no-audit --no-fund lighthouse@12.8.2 playwright@1.63.0
node frontend/tests/performance/lighthouse-round2.mjs "$T" before
node frontend/tests/performance/lighthouse-round2.mjs "$T" before --resume
node frontend/tests/performance/clipboard-diagnostic.mjs "$T"
# Frozen prepatch image override + fresh owned DB for noise / extra auth baseline:
PERF_RUN_LABEL=noise PERF_SCREENS=profile-builder,devices-builder,analytics-builder,login,register-owner node frontend/tests/performance/lighthouse-round2.mjs "$T" before --seed
# Fresh owned DB + current final frontend:
PERF_RUN_LABEL=final node frontend/tests/performance/lighthouse-round2.mjs "$T" after --seed
PERF_RUN_LABEL=stepper PERF_SCREENS=register-builder,register-owner node frontend/tests/performance/lighthouse-round2.mjs "$T" after
node frontend/tests/performance/report-round2.mjs "$T"
node frontend/tests/performance/verify-round2.mjs "$T"
```

| Command / gate | Final local result | CI / limit |
|---|---|---|
| Snapshot `npm ci --ignore-scripts --prefer-offline --no-audit --no-fund` | Passed | Checkout dotenv never loaded |
| `npm run test:unit` — G0 | **72/72**, 10 files, final 1.33s | skipped — CI not run |
| `VITE_STRIPE_PUBLISHABLE_KEY=pk_test_local_performance_dummy VITE_CLOUDINARY_CLOUD_NAME=local-performance-dummy VITE_CLOUDINARY_UPLOAD_PRESET=local-performance-dummy npm run build` | Passed; final production Docker build also passed, 776 modules | No dependency upgrades |
| `npm run test:nginx` + existing delivery E2E — G1 | Parser passed; gzip/cache/body/API boundaries retained | skipped — CI not run |
| `dotnet test backend/tests/Contract/IoBuild.Contract.Tests.csproj --no-restore --verbosity minimal` — G1 | **20/20**, 0 skipped, ~1s | skipped — CI not run; existing NU1902 warning retained |
| `E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_CLOUDINARY_DUMMY=1 E2E_SIMULATED_PAYMENTS=1 npx playwright test --workers=1 --repeat-each=2 --reporter=list --output="$T/full-e2e-accepted"` — G2/G3/G4 functional | **56/56**, 28 scenarios ×2, 0 skipped/retries, **3.2min** on final code | skipped — CI / real providers not run |
| Versioned serial Lighthouse + rendered selector checks | **30 valid final route/profile results**, 15 screens/12 paths; no login/error-shell score accepted | Lab diagnostic evidence; not deterministic performance acceptance |
| Repeated performance/large-data/real-provider campaigns | skipped beyond the six baseline noise samples | Local CPU attribution unresolved; no field INP or provider acceptance |
| Diff/link/history/snapshot and sanitized-artifact checks | Passed locally | No CI promotion of historical gates |

The harness initially stopped when a table existed before its fixture row
arrived; changed validation to wait for actual fixture text and resumed only
already valid measurements. A fresh-stack user seed also received one startup
502 before API readiness; added `/health` migration readiness polling **before**
any fixture mutation, without retrying business writes. These are recorded
harness/environment failures, not hidden retries in final E2E acceptance.

Artifacts under `T`: `perf-surgical-round2-{before,before-noise,after,
after-final,after-stepper}-<screen>-{mobile,desktop}.report.{json,html}`;
`summary-*.json`, `comparison.json`, `clipboard-diagnostic.json`, sanitized
`seed-summary*.json`, `catalog.json`, and `prepatch-e2e`, `full-e2e-first`,
`full-e2e-final`, `full-e2e-accepted`. `comparison.json` uses final full reports,
overriding only the four registration results with the last semantic remeasure.
Intermediate reports remain historical diagnostics, not selected final scores.
All tools are versioned in `frontend/tests/performance/` and
`scripts/performance-snapshot.mjs`; report outputs remain outside the repo.
JWT/bearer values, session responses and raw network headers are not recorded;
simulated checkout IDs in seed logs are redacted. Chrome profiles are removed.
Only the owned stack/volume/network/image resources are cleaned; older artifacts
and pre-existing images remain.
Final checks validated **14 local links/anchors**, unchanged historical YAML in
all six context ledgers, **265 application source/config files** matching the
final snapshot and `git diff --check`. **192 sanitized JSON/HTML reports** retain
all campaigns, including intermediates and baseline repeats. Cleanup removed the
four owned containers, network, DB volume, current image tags and three owned
untagged campaign images; project-label checks found no remaining containers or
volumes.

| Open risk / deferred work | Why deferred | Owner | Review/resume |
|---|---|---|---|
| Global PrimeVue/CSS/vendor reduction and mobile Analytics/Devices CPU | No demonstrably low-risk local reduction; global registration/chunk changes exceed this patch and prior initialization-cycle risk; repeated timings noisy | ccarita-tech | 2026-10-06, dedicated profiling campaign |
| Remaining screen-specific contrast/heading/unnamed controls | Shared controls fixed; e.g. Analytics muted text and project/client local styles need separate scoped UI tests | ccarita-tech | 2026-10-06 |
| PrimeVue numeric paste diagnostic/BP89 | Null-data Lighthouse event is reproducible dependency behavior; real paste passes; no generic suppression or widget replacement | ccarita-tech | 2026-10-06, evaluate upstream focused fix |
| CI, deployed-origin/field monitoring, large tenants and real telemetry/providers | Local safe dummy stack and navigation measurements only | ccarita-tech | 2026-10-06, approved environment |

Rollback units: absolute redirects + their unit/E2E evidence; profile/header/device
names and palette + role E2E; stepper semantics + both registration tests;
font preconnect + head evidence; safe performance tooling + this dated ledger.
Existing gzip/cache/chunk/lazy-SDK/auth-hero work is preserved independently.

## Related bounded-context evidence

The dated section above is preserved verbatim from the IAM ledger. Its reference
to "historical gates below" refers to the historical IAM YAML retained in the
[IAM evidence ledger](../bounded-contexts/iam/evidence-ledger.md), including
Owner registration evidence. Other context histories remain in their ledgers:
[Subscriptions](../bounded-contexts/subscriptions/evidence-ledger.md),
[Profiles](../bounded-contexts/profiles/evidence-ledger.md),
[Publishing](../bounded-contexts/publishing/evidence-ledger.md),
[Devices](../bounded-contexts/devices/evidence-ledger.md) and
[Analytics](../bounded-contexts/analytics/evidence-ledger.md).
