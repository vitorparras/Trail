FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
ARG TARGETARCH
WORKDIR /source
COPY Trail.slnx Directory.Build.props Directory.Packages.props global.json nuget.config ./
COPY eng/ eng/
COPY src/Trail.Domain/Trail.Domain.csproj src/Trail.Domain/packages.lock.json src/Trail.Domain/
COPY src/Trail.Application/Trail.Application.csproj src/Trail.Application/packages.lock.json src/Trail.Application/
COPY src/Trail.Infrastructure/Trail.Infrastructure.csproj src/Trail.Infrastructure/packages.lock.json src/Trail.Infrastructure/
COPY src/Trail.Web/Trail.Web.csproj src/Trail.Web/packages.lock.json src/Trail.Web/
RUN dotnet restore src/Trail.Web/Trail.Web.csproj --locked-mode -a $TARGETARCH
COPY src/ src/
RUN dotnet publish src/Trail.Web/Trail.Web.csproj --no-restore -c Release -a $TARGETARCH -p:ContinuousIntegrationBuild=true -p:UseAppHost=false -o /app/publish
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:3ed6558056b54d5c9dfefb7ff473728e022ff2bf1d976cbf8ee07b05ec594374 AS final
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /data /data
COPY --from=build /app/publish .
USER $APP_UID
HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=3 CMD ["dotnet", "/app/Trail.Web.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "/app/Trail.Web.dll"]
