FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/idunno.Bluesky.Labeler.Host/idunno.Bluesky.Labeler.Host.csproj \
    --configuration Release \
    --framework net10.0 \
    --output /out \
    --property:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Labeler__DatabaseProvider=postgresql \
    Labeler__LocalAdministrationEndpoint=/run/labeler/admin.sock
RUN mkdir -p /data /run/labeler /run/labeler-keys && chown 1654:1654 /data /run/labeler && chmod 0700 /run/labeler
COPY --from=build /out .
USER 1654
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["dotnet", "idunno.Bluesky.Labeler.Host.dll", "--health-check", "http://127.0.0.1:8080/health/labeler/ready"]
ENTRYPOINT ["dotnet", "idunno.Bluesky.Labeler.Host.dll"]
