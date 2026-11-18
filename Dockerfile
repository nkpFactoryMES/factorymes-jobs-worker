FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false
COPY global.json Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY .packages/ ./.packages/
COPY services/jobs-worker/*.csproj services/jobs-worker/packages.lock.json ./services/jobs-worker/
RUN dotnet restore services/jobs-worker/FactoryMES.Jobs.csproj --locked-mode
COPY services/jobs-worker/ ./services/jobs-worker/
RUN dotnet publish services/jobs-worker/FactoryMES.Jobs.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15
WORKDIR /app
RUN mkdir -p /var/factorymes/keys /var/factorymes/chat-captures \
    && chown -R app:app /var/factorymes
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_CLI_TELEMETRY_OPTOUT=1
USER app
ENTRYPOINT ["dotnet", "FactoryMES.Jobs.dll"]
