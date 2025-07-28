FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim
WORKDIR /app
COPY --from=build-env /app/out ./

USER root
COPY templates/entrypoint.sh ./
RUN chmod +x ./entrypoint.sh

RUN { \
    echo "module name : HttpServer"; \
    echo "module version : 6.0.2"; \
} > ./application.info

RUN useradd -ms /bin/bash moduleuser
USER moduleuser

EXPOSE 8080

LABEL org.opencontainers.image.description="HttpServer is an Azure IoT edge module that receives HTTP requests and sends them as messages."

ENTRYPOINT ["/app/entrypoint.sh"]
