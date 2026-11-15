FROM node:24-trixie-slim AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
# vite.config.ts writes the build to ../server/wwwroot
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:11.0-resolute-aot AS server
WORKDIR /src/server
COPY server/FuelFinder.Api.csproj ./
RUN dotnet restore -r linux-x64
COPY server/ ./
COPY --from=client /src/server/wwwroot ./wwwroot
RUN dotnet publish -c Release -r linux-x64 -o /app --no-restore \
    && rm -f /app/*.dbg /app/appsettings.Development.json

# Native AOT binary on Ubuntu chiseled (distroless), with no .NET runtime in the image.
FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-resolute-chiseled
WORKDIR /app
COPY --from=server /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["./FuelFinder.Api"]
