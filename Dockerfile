FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Identity.Domain/Identity.Domain.csproj src/Identity.Domain/
COPY src/Identity.Application/Identity.Application.csproj src/Identity.Application/
COPY src/Identity.Infrastructure/Identity.Infrastructure.csproj src/Identity.Infrastructure/
COPY src/Identity.Api/Identity.Api.csproj src/Identity.Api/
RUN dotnet restore src/Identity.Api/Identity.Api.csproj
COPY src/ src/
RUN dotnet publish src/Identity.Api/Identity.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS final
ARG VCS_REF=unknown
LABEL org.opencontainers.image.title="Identity" \
      org.opencontainers.image.version="1.1.0" \
      org.opencontainers.image.revision=$VCS_REF
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["dotnet", "Identity.Api.dll", "--health-check"]
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
