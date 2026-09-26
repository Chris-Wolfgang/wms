# syntax=docker/dockerfile:1.7
# E14.1: one build, three runtime images (api, web, worker), each on the ASP.NET runtime image with no SDK
# inside. Build with `docker build --target api -t wms-api .` (or through compose.yaml). The publish is JIT,
# not trimmed: EF Core, Serilog's sinks and the OpenID Connect stack are not trim-safe, so trimming is
# reserved for the NativeAOT CLI and simulator (E13.3).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props BannedSymbols.txt .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Wolfgang.Wms.Api/Wolfgang.Wms.Api.csproj -c Release -o /out/api --no-self-contained -p:UseAppHost=false \
 && dotnet publish src/Wolfgang.Wms.Web/Wolfgang.Wms.Web.csproj -c Release -o /out/web --no-self-contained -p:UseAppHost=false \
 && dotnet publish src/Wolfgang.Wms.Worker/Wolfgang.Wms.Worker.csproj -c Release -o /out/worker --no-self-contained -p:UseAppHost=false \
 && dotnet publish src/Wolfgang.Wms.Migrate/Wolfgang.Wms.Migrate.csproj -c Release -o /out/migrate --no-self-contained -p:UseAppHost=false

# The runtime base: the `app` user (non-root), curl for the compose health checks, a writable key-ring directory.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /keys /filedrop && chown -R app:app /keys /filedrop
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0 \
    Wms__Logging__Stdout=true
WORKDIR /app
USER app

FROM runtime AS api
COPY --from=build --chown=app:app /out/api/ /app/
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wolfgang.Wms.Api.dll"]

FROM runtime AS web
COPY --from=build --chown=app:app /out/web/ /app/
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wolfgang.Wms.Web.dll"]

# E14.2: one worker image, three roles by WMS_ROLE (worker, ingest, migrate); migrate exits when done.
FROM runtime AS worker
COPY --from=build --chown=app:app /out/worker/ /app/
COPY --from=build --chown=app:app /out/migrate/ /app/migrate/
COPY --chown=app:app docker/entrypoint.sh /app/entrypoint.sh
ENV WMS_ROLE=worker
ENTRYPOINT ["/bin/bash", "/app/entrypoint.sh"]
