FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build

COPY src/FeatureFusion/*.csproj ./src/FeatureFusion/
COPY src/EventBusRabbitMQ/*.csproj ./src/EventBusRabbitMQ/
COPY src/FeatureFusion.AppHost.ServiceDefaults/*.csproj ./src/FeatureFusion.AppHost.ServiceDefaults/
COPY tests/EventBus.Test/*.csproj ./tests/EventBus.Test/

# Integration/Functional tests on ci/pipelines
RUN dotnet restore tests/EventBus.Test/EventBus.Test.csproj 

COPY ../ .

WORKDIR /build/src/FeatureFusion
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out ./
EXPOSE 5004
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost:5004/health || exit 1
ENTRYPOINT ["dotnet", "FeatureFusion.dll"]