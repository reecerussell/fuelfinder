# ---- Build the React client ----
FROM node:24-alpine AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
# vite.config.ts writes the build to ../server/wwwroot
RUN npm run build

# ---- Build the .NET API ----
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS server
WORKDIR /src/server
COPY server/FuelFinder.Api.csproj ./
RUN dotnet restore
COPY server/ ./
COPY --from=client /src/server/wwwroot ./wwwroot
RUN dotnet publish -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=server /app ./
# The aspnet image listens on 8080. Use the numeric UID of its non-root `app`
# user so Kubernetes' runAsNonRoot check can verify it.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FuelFinder.Api.dll"]
