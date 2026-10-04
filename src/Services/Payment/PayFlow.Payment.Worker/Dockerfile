FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/Services/Payment/PayFlow.Payment.Worker/PayFlow.Payment.Worker.csproj
RUN dotnet publish src/Services/Payment/PayFlow.Payment.Worker/PayFlow.Payment.Worker.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "PayFlow.Payment.Worker.dll"]
