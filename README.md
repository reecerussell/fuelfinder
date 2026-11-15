# Fuel Finder

**Live at [findfuel.live](https://findfuel.live)**

A mobile-first website that shows live UK pump prices for nearby forecourts. The data comes from the
government's [Fuel Finder scheme](https://www.developer.fuel-finder.service.gov.uk/), which requires
every UK forecourt to report its prices.

- Search by **postcode** or **current location**, within 2, 5, 10 or 20 miles
- Choose **unleaded (E10), super unleaded (E5), diesel (B7) or premium diesel**
- Sort by **cheapest** or **nearest**
- See the **cost to fill up**, based on the user's tank size, which is remembered on their device
- **Share** a search as a link
- Open a station to see all its prices, get **directions** or **call** it

| | |
|---|---|
| **server/** | ASP.NET Core on .NET 11. Minimal API endpoints, published with Native AOT, with runtime async enabled |
| **client/** | React 19, TypeScript and Vite |
| **Hosting** | Talos Kubernetes home cluster via ArgoCD ([`tiny-cluster`](https://github.com/reecerussell/tiny-cluster) repo), exposed through a Cloudflare Tunnel |
| **Image** | `ghcr.io/reecerussell/fuelfinder`, about 50 MB, Ubuntu chiseled (distroless) |

---

## Contents

- [How it works](#how-it-works)
- [Project layout](#project-layout)
- [API reference](#api-reference)
- [Front end](#front-end)
- [Configuration](#configuration)
- [Running locally](#running-locally)
- [Building](#building)
- [Deployment](#deployment)
- [Security](#security)
- [Code conventions](#code-conventions)
- [Operational notes and known issues](#operational-notes-and-known-issues)

---

## How it works

```
Browser ──HTTPS──▶ Cloudflare ──tunnel──▶ cloudflared (cluster) ──▶ fuelfinder Service :80 ──▶ pod :8080
   │                                                                             │
   └── postcode lookup ──▶ api.postcodes.io                                     └── every 15 min ──▶ GOV.UK Fuel Finder API
```

### The upstream data

The GOV.UK Fuel Finder API is **bulk-only**. It has no "search near me" endpoint, so the server
downloads the whole dataset and searches it in memory.

| Detail | Value |
|---|---|
| Base URL | `https://www.fuel-finder.service.gov.uk/api/v1/` |
| Auth | `POST oauth/generate_access_token` with a JSON body `{ "client_id", "client_secret" }`. Returns `{ "data": { "access_token", "expires_in": 3600 } }` |
| Stations | `GET pfs?batch-number=N`: details for every forecourt, 500 per batch |
| Prices | `GET pfs/fuel-prices?batch-number=N`: prices for every forecourt, 500 per batch |
| End of data | The batch after the last one returns **404**. There are about 17 batches (around 8,100 stations) |
| Limits | 100 requests per minute and **1 concurrent request** per client. Data updates within 30 minutes of a price change |
| Quirks | Requests without a `User-Agent` header get **403** from the firewall. Some boolean fields come back as `null`. Fuel codes are upper case (`E10`, `E5`, `B7_STANDARD`, `B7_PREMIUM`, `B10`, `HVO`) |

These details were worked out by testing against the live API, because the public docs don't include
the token URL or the paths.

### The server

1. **`FuelDataRefreshService`** (a hosted service) downloads station details every **hour** and
   prices every **15 minutes**, following the API's caching guidance. If the first load fails, it
   retries every minute.
2. **`FuelFinderApiClient`** pages through the batches. A semaphore ensures only one request is in
   flight, because of the API's one-concurrent-request limit.
3. **`StationFactory`** joins each station to its prices and cleans up the data:
   - skips stations that are temporarily or permanently closed, or have no coordinates
   - multiplies any price under 10 by 100, because some retailers submit pounds (1.459) instead of
     pence (145.9)
   - drops prices outside 80–400p as data errors
   - converts upper-case names and addresses to title case
4. **`FuelDataStore`** holds the cleaned list as an immutable snapshot and swaps it atomically on each
   refresh.
5. **`StationSearchService`** calculates the haversine distance to every station, filters by radius
   and fuel, sorts, and returns the top 50 along with the total count. A search takes about
   5–8 ms in the cluster.

Visitors never trigger calls to the government API. Only the refresh schedule does, so traffic
can't exhaust the upstream quota.

### Downstream HTTP

Both downstream calls use named `HttpClient`s from `IHttpClientFactory`, each with the **standard
resilience handler** (`Microsoft.Extensions.Http.Resilience`). It retries 429s, 5xx responses and
timeouts with backoff, honours `Retry-After`, and adds a circuit breaker.

| Client | Purpose | Extra handlers |
|---|---|---|
| `FuelFinderAuth` | Token requests | Default resilience settings |
| `FuelFinder` | Data batches | `FuelFinderAuthenticationHandler` attaches the bearer token, and on a 401 refreshes it and retries once. Attempt timeout 60 s, total timeout 5 min |

`FuelFinderTokenProvider` caches the access token until a minute before it expires.

---

## Project layout

```
fuelfinder/
├── Dockerfile                 # 3-stage build: Node → .NET AOT → chiseled runtime-deps
├── .github/
│   ├── workflows/build.yaml   # builds and pushes the image to GHCR on every push to master
│   └── dependabot.yml         # weekly updates for actions, NuGet and npm
├── server/
│   ├── Program.cs             # explicit Program class: services and pipeline
│   ├── Endpoints/             # minimal API endpoint groups (one static class each)
│   │   ├── StationsEndpoints.cs   GET /api/stations (rate limited)
│   │   ├── StatusEndpoints.cs     GET /api/status
│   │   └── HealthEndpoints.cs     GET /healthz (readiness)
│   ├── Dtos/                  # API request/response types, and the upstream Fuel Finder DTOs (FuelFinder*Dto)
│   ├── Models/                # domain: Station, FuelPrice, search criteria and results, sort order
│   ├── Services/              # API client, token provider, auth handler, refresh service, search, StationFactory, GeoDistance
│   ├── Stores/                # FuelDataStore: in-memory snapshot of all stations
│   ├── Middleware/            # response/security headers, rate-limit policy, client IP resolution
│   ├── Options/               # FuelFinderOptions (bound from the "FuelFinder" config section)
│   ├── Serialization/         # AppJsonSerializerContext: source-generated JSON for AOT
│   └── wwwroot/               # built React app (generated by `npm run build`, gitignored)
└── client/
    ├── index.html             # SEO, Open Graph and Twitter tags, JSON-LD, icons, manifest
    ├── vite.config.ts         # dev proxy /api → :5067; builds into ../server/wwwroot
    ├── public/                # logo, favicons, PWA icons, og-image, robots.txt, sitemap.xml, site.webmanifest
    └── src/
        ├── App.tsx            # the page: search, filters, results list, sharing
        ├── CarPanel.tsx       # tank size / current level form
        ├── api.ts             # API calls, fuel types, postcode and geolocation lookups
        ├── car.ts             # fill-up maths and the saved car settings
        ├── storage.ts         # localStorage read/write that copes with blocked storage
        ├── url.ts             # shareable URL parsing and writing
        └── index.css          # all styles (light theme, square corners, blue on cream)
```

---

## API reference

All responses are JSON in camelCase. Errors use [RFC 9457 problem details](https://www.rfc-editor.org/rfc/rfc9457)
and never include stack traces or exception messages.

### `GET /api/stations`

Searches stations around a point. **Rate limited to 60 requests per minute per client IP.**

| Query param | Required | Notes |
|---|---|---|
| `lat` | yes | −90 to 90 |
| `lng` | yes | −180 to 180 |
| `radiusKm` | no | Default 8, clamped to 1–50 |
| `fuel` | no | `E10`, `E5`, `B7_STANDARD`, `B7_PREMIUM` (any case). If set, only stations selling it are returned |
| `sort` | no | `price` (default) or `distance`, any case. `price` falls back to distance when no fuel is given |

```jsonc
// 200
{
  "lastUpdated": "2026-11-15T16:46:09.58Z",   // when the server last refreshed prices
  "total": 83,                                 // all matches; results holds at most 50
  "results": [{
    "id": "4882e3fe…", "name": "Asda Trafford Park", "brand": "Asda",
    "address": "Barton Dock Road, Trafford Park, Manchester", "postcode": "M41 7PP",
    "phone": "+44…", "latitude": 53.46, "longitude": -2.33,
    "isMotorway": false, "isSupermarket": true, "amenities": ["customer_toilets"],
    "prices": [{ "fuelType": "E10", "price": 150.7, "updatedAt": "2026-11-11T12:18:30Z" }],
    "distanceKm": 6.9
  }]
}
```

| Status | When |
|---|---|
| 400 | Validation failed (missing or out-of-range `lat`/`lng`, unknown `sort`). The body includes field errors |
| 429 | Over 60 searches in a minute from one IP. Includes a `Retry-After` header |
| 503 | Data not loaded yet (the first 20–30 seconds after start-up) |

### `GET /api/status`

`{ "loaded": true, "stations": 8098, "lastUpdated": "…" }`. Always 200. Used as the liveness probe.

### `GET /healthz`

Returns `200 "ok"` once data has loaded, and `503` before that. Used as the **readiness** probe, so a
new pod takes no traffic until it has prices, and a rollout keeps the old pod serving until then.

Any other path serves the React app (`index.html`), or a static file from `wwwroot`.

---

## Front end

A single page with no router. It works at phone width first, and every layout was checked at
360–390 px.

### Search and results
- **Use my location** uses the browser's geolocation API, which needs HTTPS. **Postcode** search
  sends the postcode from the browser to [postcodes.io](https://postcodes.io), a free service. Full
  postcodes and outcodes (such as `M1`) both work.
- The list shows the price for the chosen fuel (the cheapest is highlighted), distance in miles, and
  how long ago the price was updated. Tapping a station shows every fuel, plus Directions (Google
  Maps) and Call.

### Fill-up cost (`car.ts`, `CarPanel.tsx`)
- Users enter their **tank size** (10–150 L) and how full it is now (**empty, ¼, ½, ¾**).
- Each station shows `tank × (1 − level) × price`, for example *£61.42 to fill*. The details view
  shows the cost for every fuel.
- Settings are stored in `localStorage` under `car`, on that device only. Nothing is sent to the
  server.
- A registration lookup isn't possible: the DVLA Vehicle Enquiry API returns make, fuel type and
  engine size, but **not** the tank size or model. Getting tank sizes would need a paid
  vehicle-data provider.

### Shareable links (`url.ts`)
The address bar always reflects the current search (using `history.replaceState`, so filter taps
don't add history entries). The **Share** button uses the phone's share sheet, or copies the link on
desktop.

| Param | Example | Notes |
|---|---|---|
| `postcode` | `SW1A1AA` | Used when the search came from a postcode |
| `lat`, `lng` | `53.48`, `-2.24` | Used for "my location". **Rounded to 2 decimal places (about 1 km)** so a shared link doesn't reveal someone's home |
| `fuel` | `unleaded`, `super-unleaded`, `diesel`, `premium-diesel` | |
| `radius` | `2`, `5`, `10`, `20` | Miles |
| `sort` | `distance` | Left out when it's the default (`price`) |

Opening a shared link applies its settings **for that visit only**. The recipient's saved fuel and
radius preferences (`localStorage` keys `fuel`, `radius`) change only when they pick something
themselves. Invalid parameters are ignored, and invalid or corrupted saved values fall back to the
defaults.

### Design
Light theme only: a warm cream background (`#f5f1e8`), blue as the main colour (`#1f5fbf`), square
corners, hard borders, Arial, and no emojis. All colours are CSS variables at the top of `index.css`.

### Logo, SEO and PWA
- **Logo** (`public/logo.svg`): a white fuel drop with a location dot on a square blue tile. Used in
  the header, the favicons, the PWA icons (including a maskable one with a safe zone) and the
  1200×630 `og-image.png`.
- **`index.html`** includes the title and description, a canonical link to `https://findfuel.live/`,
  Open Graph and Twitter card tags, `WebApplication` JSON-LD, the theme colour and a `<noscript>`
  fallback.
- **`robots.txt`** allows everything except `/api/` and points to **`sitemap.xml`**.
  **`site.webmanifest`** makes the site installable.
- To regenerate the PNG and ICO icons after changing `logo.svg`, render the SVG at 16, 32, 48, 180,
  192 and 512 px with headless Chrome, then bundle 16/32/48 into `favicon.ico` with Pillow.

---

## Configuration

| Setting | Env var | Notes |
|---|---|---|
| `FuelFinder:ClientId` | `FuelFinder__ClientId` | **Required.** The app won't start without it |
| `FuelFinder:ClientSecret` | `FuelFinder__ClientSecret` | **Required** |
| `FuelFinder:BaseUrl` | `FuelFinder__BaseUrl` | Defaults to `https://www.fuel-finder.service.gov.uk/api/v1/` |
| Listen URL | `ASPNETCORE_URLS` / `ASPNETCORE_HTTP_PORTS` | The container listens on **8080** |

Credentials come from the [Fuel Finder developer portal](https://www.developer.fuel-finder.service.gov.uk/)
(sign in with GOV.UK One Login and register as an information recipient). **Never commit them.**
- **Locally:** .NET user-secrets (see below).
- **In the cluster:** a SOPS/age-encrypted Secret. See [Deployment](#deployment).

---

## Running locally

**Requirements:** .NET 11 SDK and Node 24. Native AOT publishing also needs `clang` or `gcc` and
`zlib`, but plain `dotnet run` and the Docker build don't.

```bash
# one-off: store the API credentials
cd server
dotnet user-secrets set "FuelFinder:ClientId" "<client id>"
dotnet user-secrets set "FuelFinder:ClientSecret" "<client secret>"

# terminal 1: API on http://localhost:5067 (JIT; data takes ~20-30 s to load)
cd server && dotnet run --launch-profile http

# terminal 2: React dev server with hot reload, proxying /api to :5067
cd client && npm install && npm run dev        # open http://localhost:5173
```

To test on a phone on the same network, run the API on all interfaces with
`dotnet run --no-launch-profile --urls "http://0.0.0.0:5067"` after `npm run build`, then open
`http://<machine-ip>:5067`. **Use my location only works over HTTPS**, so use postcode search there,
or test on `findfuel.live`.

Useful checks:
```bash
cd client && npx tsc -b && npm run lint            # type check and lint
curl localhost:5067/api/status
curl "localhost:5067/api/stations?lat=51.5074&lng=-0.1278&fuel=E10"
```

---

## Building

### Docker image

```bash
docker build -t fuelfinder .
docker run --rm -p 8080:8080 --read-only --cap-drop ALL \
  -e FuelFinder__ClientId=… -e FuelFinder__ClientSecret=… fuelfinder
```

The Dockerfile has three stages:

| Stage | Image | Does |
|---|---|---|
| `client` | `node:24-trixie-slim` (Debian) | `npm ci && npm run build`, writing to `server/wwwroot` |
| `server` | `mcr.microsoft.com/dotnet/sdk:11.0-resolute-aot` (Ubuntu 26.04 + native toolchain) | `dotnet publish -r linux-x64` with Native AOT, then deletes `.dbg` symbols and `appsettings.Development.json` |
| final | `mcr.microsoft.com/dotnet/runtime-deps:11.0-resolute-chiseled` | Ubuntu chiseled: no shell, no package manager, no .NET runtime. Runs `./FuelFinder.Api` as UID 1654 |

Debian and Ubuntu images are used deliberately rather than Alpine. The image is **about 50 MB**,
compared with 190 MB for a JIT build on `aspnet:11.0-resolute-chiseled-composite` and 393 MB for the
standard `aspnet` image. Memory use is about 75 MB with the full dataset loaded.

### Build settings (`FuelFinder.Api.csproj`)

| Setting | Why |
|---|---|
| `PublishAot=true` | Native AOT gives a small image and fast start-up. It's the reason the API uses **minimal APIs instead of controllers**, because MVC isn't AOT-compatible. It also enables the minimal API request delegate and configuration-binding source generators |
| `InvariantGlobalization=true` | Chiseled images have no ICU. Title casing uses `CultureInfo.InvariantCulture` |
| `<Features>$(Features);runtime-async=on</Features>` | .NET 11 **runtime async**: async methods are compiled without state machines. It must be set in the project file. Passing `-p:Features=a;runtime-async=on` on the command line silently fails, because MSBuild splits on `;` |
| `Microsoft.Extensions.Http.Resilience` | The standard resilience handler for downstream calls |

**AOT rules:**
- **JSON:** all JSON goes through `Serialization/AppJsonSerializerContext.cs`. When adding a
  request or response type, add a `[JsonSerializable(typeof(...))]` entry there, and use the
  `JsonTypeInfo` overloads with `HttpClient`.
- **Warnings:** keep `dotnet publish -r linux-x64` free of `IL2xxx`/`IL3xxx` warnings.
- **Validation:** request validation uses `AddValidation()` with DataAnnotations on the
  `[AsParameters]` request type.
- **Enums:** minimal API enum binding is **case-sensitive**, which is why `sort` is bound as a string
  and parsed in the endpoint.

---

## Deployment

### Pipeline

```
push to master ──▶ GitHub Actions (build.yaml) ──▶ ghcr.io/reecerussell/fuelfinder:{latest, <git sha>}
                                                        │
tiny-cluster repo: infra/fuelfinder/kustomization.yaml pins the image digest
                                                        │
ArgoCD (auto-sync, prune, self-heal) ──▶ namespace fuelfinder
```

- **CI** (`.github/workflows/build.yaml`) runs on pushes to `master` and manual dispatch. It uses
  `GITHUB_TOKEN` with `packages: write`. Actions are pinned to commit SHAs, and Dependabot keeps them
  current.
- The **GHCR package is public**, so the cluster pulls without credentials. Its visibility is set
  separately from the repo's, under the package settings on GitHub.

### Cluster resources (`tiny-cluster` repo)

| Path | What |
|---|---|
| `bootstrap/argocd/apps/fuelfinder.yaml` | ArgoCD Application (sync wave 1, auto-sync, `CreateNamespace`), with Image Updater annotations (`digest` strategy, git write-back to `kustomization.yaml`) |
| `bootstrap/argocd/apps/fuelfinder-image-updater.yaml` | `ImageUpdater` CR. It has to live in the `argocd` namespace |
| `infra/fuelfinder/app.yaml` | Deployment and ClusterIP Service (port 80 → 8080) |
| `infra/fuelfinder/fuelfinder-api-credentials.sops.yaml` | Secret holding `FuelFinder__ClientId` and `FuelFinder__ClientSecret`, SOPS/age-encrypted and decrypted by ksops in ArgoCD |
| `infra/fuelfinder/secret-generator.yaml` | ksops generator for the secret above |
| `infra/fuelfinder/kustomization.yaml` | Namespace, resources, generator, and the **pinned image digest** |
| `charts/gateway/values.yaml` | An envoy route for `fuel.reece-russell.co.uk`, behind Cloudflare Access. Optional and not used by the public site |

**Deployment settings:**
- **Exactly 1 replica**, on purpose. The dataset lives in memory, and the upstream API allows only
  one concurrent request per client.
- **Probes:** readiness on `/healthz`, liveness on `/api/status`.
- **Resources:** requests of 50m CPU and 128Mi memory, and a 384Mi memory limit.
- **Security context:** `runAsNonRoot` (UID/GID 1654), `readOnlyRootFilesystem`,
  `allowPrivilegeEscalation: false`, all capabilities dropped, `RuntimeDefault` seccomp.

### Public exposure

`findfuel.live` is a Cloudflare zone. The cluster's Cloudflare Tunnel (`cloudflared`, remotely
managed) has a public hostname route **`findfuel.live` → `http://fuelfinder.fuelfinder:80`**. This
goes directly to the service, not through the envoy gateway, because the gateway requires a
Cloudflare Access login. Cloudflare terminates TLS, so geolocation works.

### Releasing a change

1. Push to `master` in this repo and wait for the **Build** workflow.
2. Normally **Argo CD Image Updater** notices the new `:latest` digest and commits it to
   `infra/fuelfinder/kustomization.yaml`.
3. **While the Image Updater token is expired** (see below), pin the digest by hand:
   ```bash
   TOKEN=$(curl -s "https://ghcr.io/token?scope=repository:reecerussell/fuelfinder:pull" | jq -r .token)
   curl -sI -H "Authorization: Bearer $TOKEN" \
     -H "Accept: application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.v2+json" \
     https://ghcr.io/v2/reecerussell/fuelfinder/manifests/<git sha> | grep -i docker-content-digest
   # put the digest in tiny-cluster/infra/fuelfinder/kustomization.yaml, commit, push
   kubectl -n argocd annotate application fuelfinder argocd.argoproj.io/refresh=hard --overwrite
   ```
4. Watch it with `kubectl -n fuelfinder rollout status deploy/fuelfinder`. Readiness waits for the data
   to load, about 20 seconds.

### Rotating the API credentials

```bash
cd tiny-cluster
export SOPS_AGE_KEY_FILE=$PWD/.config/sops/age/keys.txt
sops infra/fuelfinder/fuelfinder-api-credentials.sops.yaml   # edit and save; it re-encrypts
git commit -am "fuelfinder: rotate API credentials" && git push
kubectl -n fuelfinder rollout restart deploy/fuelfinder       # env vars are read at start-up
```

---

## Security

| Area | Measure |
|---|---|
| Response headers | Kestrel's `Server` header is turned off and replaced with `Server: FindFuel`. Every response (API, static files, errors) gets `Content-Security-Policy`, `Strict-Transport-Security` (1 year, subdomains), `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin` and `Permissions-Policy` (geolocation for the site itself only). See `Middleware/ResponseHeadersMiddleware.cs` |
| CSP | `default-src 'self'`, with `connect-src` adding `https://api.postcodes.io`, `img-src` adding `data:`, `object-src 'none'` and `frame-ancestors 'none'`. **If the front end starts calling a new origin, add it here** |
| Errors | `UseExceptionHandler()` and `UseStatusCodePages()` return generic problem details. No exception messages or stack traces reach clients |
| Rate limiting | `/api/stations` allows 60 requests per minute per client IP (fixed window, no queue) and returns 429 with `Retry-After`. The client IP comes from `CF-Connecting-IP`, which Cloudflare overwrites, because behind the tunnel every request's remote address is the cloudflared pod. See `Middleware/RateLimitPolicies.cs` and `ClientIpAddress.cs` |
| Input | DataAnnotations validation on `lat`/`lng`, a clamped radius, and a parsed and checked `sort`. Upstream text is rendered by React as text only, with no raw HTML anywhere |
| Secrets | Never in git. User-secrets locally and SOPS/age in the cluster |
| Container | Distroless chiseled image with no shell. Non-root, read-only root filesystem, no privilege escalation, all capabilities dropped |
| Supply chain | Actions pinned to SHAs. Dependabot for actions, NuGet and npm |
| Privacy | Postcodes go from the browser straight to postcodes.io. Tank settings and preferences stay in `localStorage`. Shared "my location" links are rounded to about 1 km |

Cloudflare replaces the `Server` header with `server: cloudflare` on the public site. The other
headers come through unchanged.

---

## Code conventions

Backend rules for this repo:

- **One top-level type per file** (class, record, interface or enum). Private nested types inside a
  class are the only exception.
- **No comments** unless the code is unusual or not self-explanatory (for example the price
  normalisation, the upstream API quirks, or the Cloudflare IP header).
- An **explicit `Program` class** with `Main`, not top-level statements.
- **No primary constructors.** Positional records count, so use explicit constructors or `init`
  properties.
- **Downstream HTTP goes through `IHttpClientFactory`** with a resilience (retry) handler.
- **Folders:** `Dtos` (request, response and other DTOs), `Models` (domain), `Services`, `Stores`,
  plus `Endpoints`, `Middleware`, `Options` and `Serialization`.
- **Never shorten `CancellationToken` parameters to `ct`.** Use `cancellationToken`, or
  `stoppingToken` in hosted services.
- Prefer **Debian or Ubuntu** base images over Alpine, and the smallest image that works.

---

## Operational notes and known issues

- **Image Updater can't push to git.** The `argocd/git-creds` GitHub token in the cluster repo
  (`infra/argocd/git-creds.sops.yaml`) has expired, which also affects quizzie. Until it's replaced,
  pin digests by hand as described in [Releasing a change](#releasing-a-change).
- **The client secret should be rotated.** It was shared during development. See
  [Rotating the API credentials](#rotating-the-api-credentials).
- **Start-up:** the pod is unready for about 20–30 seconds while it downloads around 17 batches of
  stations and prices.
- **Stale prices:** some forecourts haven't updated in weeks. The "updated X ago" label is shown on
  purpose so users can judge.
- **Naming:** the site's name, "Fuel Finder", is the same as the government scheme's name. A rename
  to match the domain (for example "FindFuel") has been suggested so the site isn't mistaken for the
  official service.
- **Local Docker on snap:** `--security-opt no-new-privileges` stops the binary from starting under
  snap-packaged Docker because of its AppArmor profile. Kubernetes' `allowPrivilegeEscalation: false`
  works fine.
- **Performance:** about 2–8 ms inside the cluster. Through Cloudflare it's about 30 ms on a reused
  connection and 75–85 ms on a new one, which is almost all the tunnel and TLS.
