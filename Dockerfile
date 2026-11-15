FROM node:24-trixie-slim AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
# vite.config.ts writes the build to ../server/wwwroot
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS server
WORKDIR /src/server
COPY server/FuelFinder.Api.csproj ./
RUN dotnet restore
COPY server/ ./
COPY --from=client /src/server/wwwroot ./wwwroot
RUN dotnet publish -c Release -o /app --no-restore

# Ubuntu chiseled (distroless) with the ASP.NET framework precompiled as a composite image.
# Native AOT isn't an option because MVC controllers aren't AOT-compatible.
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-composite
WORKDIR /app
COPY --from=server /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FuelFinder.Api.dll"]
