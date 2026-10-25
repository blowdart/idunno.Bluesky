FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish samples/Samples.LabelerHost/Samples.LabelerHost.csproj \
    --configuration Release \
    --output /out \
    --property:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Labeler__DatabaseProvider=sqlite \
    Labeler__ConnectionString="Data Source=/data/labels.db" \
    Labeler__SigningKeyDirectory=/run/labeler-keys \
    Labeler__ActiveKeyPath=/data/active-key \
    Labeler__LocalAdministrationEndpoint=/run/labeler/admin.sock
RUN mkdir -p /data /run/labeler /run/labeler-keys && chown 1654:1654 /data /run/labeler && chmod 0700 /run/labeler
COPY --from=build /out .
USER 1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "Samples.LabelerHost.dll"]
