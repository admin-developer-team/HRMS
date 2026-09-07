FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Hrms.slnx ./
COPY src/Hrms.Domain/Hrms.Domain.csproj src/Hrms.Domain/
COPY src/Hrms.Application/Hrms.Application.csproj src/Hrms.Application/
COPY src/Hrms.Infrastructure/Hrms.Infrastructure.csproj src/Hrms.Infrastructure/
COPY src/Hrms.Api/Hrms.Api.csproj src/Hrms.Api/
RUN dotnet restore src/Hrms.Api/Hrms.Api.csproj

COPY src/ src/
RUN dotnet publish src/Hrms.Api/Hrms.Api.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build --chown=app:app /app/publish .
USER app
EXPOSE 8080 10000
ENTRYPOINT ["dotnet", "Hrms.Api.dll"]
