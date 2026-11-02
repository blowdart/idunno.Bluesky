FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SOURCE_REVISION_ID
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN test -n "$SOURCE_REVISION_ID" && dotnet publish src/idunno.Bluesky.Labeler.Host/idunno.Bluesky.Labeler.Host.csproj \
    --configuration Release \
    --framework net10.0 \
    --output /out \
    --property:UseAppHost=false \
    --property:BuildCommitId="$SOURCE_REVISION_ID"
RUN case "$TARGETARCH" in \
        amd64) cli_rid=linux-x64 ;; \
        arm64) cli_rid=linux-arm64 ;; \
        *) echo "Unsupported CLI architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac && \
    dotnet publish src/idunno.Bluesky.Labeler.Cli/idunno.Bluesky.Labeler.Cli.csproj \
    --configuration Release \
    --framework net10.0 \
    --runtime "$cli_rid" \
    --self-contained true \
    --output /cli-out \
    --property:BuildCommitId="$SOURCE_REVISION_ID" && \
    ln -s idunno.Bluesky.Labeler.Cli /cli-out/labeler-admin

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Labeler__DatabaseProvider=postgresql \
    Labeler__LocalAdministrationEndpoint=/run/labeler/admin.sock \
    PATH="/opt/labeler-cli:${PATH}"
RUN mkdir -p /data /run/labeler /run/labeler-keys && chown 1654:1654 /data /run/labeler && chmod 0700 /run/labeler
COPY --from=build /out .
COPY --from=build /cli-out /opt/labeler-cli/
USER 1654
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["dotnet", "idunno.Bluesky.Labeler.Host.dll", "--health-check", "http://127.0.0.1:8080/health/labeler/ready"]
ENTRYPOINT ["dotnet", "idunno.Bluesky.Labeler.Host.dll"]
