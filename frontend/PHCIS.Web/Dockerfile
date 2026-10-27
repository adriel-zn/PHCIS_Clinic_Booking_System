FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY frontend/PHCIS.Web/ frontend/PHCIS.Web/
RUN printf '%s' '{"Api":{"BaseUrl":"https://localhost:8443/","ClinicUtcOffsetMinutes":120,"UseGraphQL":true,"ShowDevelopmentLogin":true}}' > frontend/PHCIS.Web/wwwroot/appsettings.json
RUN dotnet publish frontend/PHCIS.Web/PHCIS.Web.csproj -c Release -o /out

FROM caddy:2-alpine
COPY --from=build /out/wwwroot /srv
COPY docker/Caddyfile /etc/caddy/Caddyfile
EXPOSE 8443
