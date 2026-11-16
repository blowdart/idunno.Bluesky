ARG LABELER_BASE
FROM ${LABELER_BASE}
ARG IMAGE_SOURCE=https://github.com/blowdart/idunno.Bluesky
ARG IMAGE_REVISION
ARG IMAGE_VERSION
ARG IMAGE_CREATED
LABEL org.opencontainers.image.source="${IMAGE_SOURCE}" \
      org.opencontainers.image.description="idunno.Bluesky labeler host using MySQL storage. Includes the labeler administration CLI (labeler-admin) and the signed CLI downloads." \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.revision="${IMAGE_REVISION}" \
      org.opencontainers.image.version="${IMAGE_VERSION}" \
      org.opencontainers.image.created="${IMAGE_CREATED}"
WORKDIR /app
COPY hosts/mysql/ .
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["dotnet", "idunno.Bluesky.Labeler.Host.MySQL.dll", "--health-check", "http://127.0.0.1:8080/health/labeler/ready"]
ENTRYPOINT ["dotnet", "idunno.Bluesky.Labeler.Host.MySQL.dll"]
CMD []
