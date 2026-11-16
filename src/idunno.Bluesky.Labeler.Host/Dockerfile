ARG TARGETARCH

FROM scratch AS cli-amd64
COPY cli/linux-x64/ /

FROM scratch AS cli-arm64
COPY cli/linux-arm64/ /

FROM cli-${TARGETARCH} AS cli

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG IMAGE_SOURCE=https://github.com/blowdart/idunno.Bluesky
ARG IMAGE_REVISION
ARG IMAGE_VERSION
ARG IMAGE_CREATED
LABEL org.opencontainers.image.source="${IMAGE_SOURCE}" \
      org.opencontainers.image.description="Base image for idunno.Bluesky labeler hosts. Contains the labeler administration CLI (labeler-admin) and the signed CLI downloads. Database specific images build on it." \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.revision="${IMAGE_REVISION}" \
      org.opencontainers.image.version="${IMAGE_VERSION}" \
      org.opencontainers.image.created="${IMAGE_CREATED}"
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Labeler__LocalAdministrationEndpoint=/run/labeler/admin.sock \
    PATH="/opt/labeler-cli:${PATH}"
RUN mkdir -p /data /run/labeler /run/labeler-keys && chown 1654:1654 /data /run/labeler && chmod 0700 /run/labeler
COPY --from=cli --chmod=755 / /opt/labeler-cli/
RUN ln -s idunno.Bluesky.Labeler.Cli /opt/labeler-cli/labeler-admin
COPY downloads/ /opt/labeler-downloads/
USER 1654
EXPOSE 8080
HEALTHCHECK NONE
CMD ["labeler-admin", "--help"]
