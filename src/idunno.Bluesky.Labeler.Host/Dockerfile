FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG SOURCE_REVISION_ID
ARG TARGETARCH
ARG PUBLIC_RELEASE=false
ENV DOTNET_PROCESSOR_COUNT=2
WORKDIR /src
COPY . .
RUN test -n "$SOURCE_REVISION_ID" && case "$TARGETARCH" in \
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
    -m:4 -nr:false --disable-build-servers \
    --property:UseSharedCompilation=false \
    --property:ConcurrentBuild=false \
    --property:PublicRelease="$PUBLIC_RELEASE" \
    -bl:/tmp/cli-publish.binlog \
    --property:BuildCommitId="$SOURCE_REVISION_ID" && \
    ln -s idunno.Bluesky.Labeler.Cli /cli-out/labeler-admin

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Labeler__LocalAdministrationEndpoint=/run/labeler/admin.sock \
    PATH="/opt/labeler-cli:${PATH}"
RUN mkdir -p /data /run/labeler /run/labeler-keys && chown 1654:1654 /data /run/labeler && chmod 0700 /run/labeler
COPY --from=build /cli-out /opt/labeler-cli/
USER 1654
EXPOSE 8080
HEALTHCHECK NONE
CMD ["labeler-admin", "--help"]
