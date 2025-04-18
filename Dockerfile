
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /app


COPY src/FeatureFusion/FeatureFusion.csproj ./src/FeatureFusion/
COPY src/EventBusRabbitMQ/EventBus.csproj ./src/EventBusRabbitMQ/
COPY src/FeatureFusion.AppHost.ServiceDefaults/FeatureFusion.AppHost.ServiceDefaults.csproj ./src/FeatureFusion.AppHost.ServiceDefaults/
RUN dotnet restore src/FeatureFusion/FeatureFusion.csproj

# Copy the full source
COPY src/FeatureFusion/ ./src/FeatureFusion/
COPY src/EventBusRabbitMQ/ ./src/EventBusRabbitMQ/
COPY src/FeatureFusion.AppHost.ServiceDefaults/ ./src/FeatureFusion.AppHost.ServiceDefaults/

# Publish the application
RUN dotnet publish src/FeatureFusion/FeatureFusion.csproj -c Release -o /app/out

# Stage 2 - Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app


RUN apk add --no-cache bash curl && \
    curl -sSL https://github.com/vishnubob/wait-for-it/releases/download/v2.8.0/wait-for-it.sh -o /wait-for-it.sh && \
    chmod +x /wait-for-it.sh


COPY --from=build /app/out ./

ENV DB_HOST=postgres
ENV DB_PORT=5432
ENV DB_USERNAME=username
ENV DB_PASSWORD=password
ENV DB_DATABASE=eventstore

EXPOSE 5004

CMD /wait-for-it.sh postgres:5432 --timeout=60s && \
    /wait-for-it.sh redis:6379 --timeout=60s && \
    /wait-for-it.sh rabbitmq:5672 --timeout=60s && \
    /wait-for-it.sh memcached:11211 --timeout=60s && \
    dotnet FeatureFusion.dll
