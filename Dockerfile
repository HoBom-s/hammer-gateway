FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY Hammer.Gateway.slnx ./
COPY src/Hammer.Gateway/Hammer.Gateway.csproj src/Hammer.Gateway/
RUN dotnet restore src/Hammer.Gateway/Hammer.Gateway.csproj

COPY src/ src/
RUN dotnet publish src/Hammer.Gateway/Hammer.Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

RUN groupadd --system --gid 1001 appgroup && \
    useradd --system --uid 1001 --gid appgroup --no-create-home appuser

COPY --from=build /app .

USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Hammer.Gateway.dll"]
