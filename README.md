# Fuel Finder

A mobile-first site that shows live UK fuel prices near you, using the GOV.UK
[Fuel Finder API](https://www.developer.fuel-finder.service.gov.uk/).

- **server/**: ASP.NET Core (.NET 11) minimal API endpoints, published with Native AOT and runtime async
- **client/**: React + TypeScript (Vite)

## How it works

The Fuel Finder API is bulk-only. It returns every forecourt in batches of 500 and
has no "search near me" endpoint. The .NET server therefore loads the full dataset
into memory at startup. It refreshes prices every 15 minutes and station details
every hour, which matches the API's caching guidance (1 concurrent request, 100 req/min).
The React app calls the server's `/api/stations` endpoint. Postcodes are converted
to coordinates in the browser using the free [postcodes.io](https://postcodes.io) API.

## Setup

Credentials are kept in .NET user-secrets, not in source:

```bash
cd server
dotnet user-secrets set "FuelFinder:ClientId" "<your client id>"
dotnet user-secrets set "FuelFinder:ClientSecret" "<your client secret>"
```

In production, set the environment variables `FuelFinder__ClientId` and `FuelFinder__ClientSecret` instead.

## Run in development

```bash
# terminal 1: API on http://localhost:5067 (takes ~30-60s to load the first data)
cd server && dotnet run --launch-profile http

# terminal 2: React dev server with hot reload; proxies /api to the server
cd client && npm install && npm run dev
```

Open the URL Vite prints (http://localhost:5173).

## Build for production

```bash
cd client && npm run build        # outputs into server/wwwroot
cd ../server && dotnet publish -c Release -o ../publish
```

The published .NET app serves both the API and the React site.

The Docker image compiles the server with Native AOT and runs it on
`runtime-deps:11.0-resolute-chiseled` (Ubuntu, distroless, no .NET runtime). It is about 50 MB.
JSON uses source generation (`Serialization/AppJsonSerializerContext.cs`), so any new
request or response type has to be registered there.

## API

- `GET /api/status`: whether data is loaded, station count, last refresh time
- `GET /api/stations?lat=..&lng=..&radiusKm=8&fuel=E10&sort=price|distance`: up to 50 stations

Fuel codes: `E10` (unleaded), `E5` (super unleaded), `B7_STANDARD` (diesel), `B7_PREMIUM` (premium diesel).
