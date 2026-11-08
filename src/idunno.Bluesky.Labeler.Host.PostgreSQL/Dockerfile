ARG LABELER_BASE
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SOURCE_REVISION_ID
ARG PUBLIC_RELEASE=false
ENV DOTNET_PROCESSOR_COUNT=2
WORKDIR /src
COPY . .
RUN test -n "$SOURCE_REVISION_ID" && dotnet publish src/idunno.Bluesky.Labeler.Host.PostgreSQL/idunno.Bluesky.Labeler.Host.PostgreSQL.csproj \
    --configuration Release --framework net10.0 --output /out \
    -m:4 -nr:false --disable-build-servers \
    --property:UseSharedCompilation=false --property:ConcurrentBuild=false \
    --property:UseAppHost=false --property:PublicRelease="$PUBLIC_RELEASE" \
    --property:BuildCommitId="$SOURCE_REVISION_ID" -bl:/tmp/host-publish.binlog

FROM ${LABELER_BASE}
WORKDIR /app
COPY --from=build /out .
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["dotnet", "idunno.Bluesky.Labeler.Host.PostgreSQL.dll", "--health-check", "http://127.0.0.1:8080/health/labeler/ready"]
ENTRYPOINT ["dotnet", "idunno.Bluesky.Labeler.Host.PostgreSQL.dll"]
CMD []
