# Hammer Gateway

Hammer 경매 플랫폼의 API Gateway.

## Stack

- ASP.NET (.NET 10)
- YARP (Reverse Proxy)
- Redis (Rate Limiting)

## Features

- JWT 검증 및 클레임 기반 헤더 주입
- YARP 기반 서비스 라우팅
- Rate Limiting
- Request/Response 로깅

## Services

| Service | Description |
|---------|-------------|
| [hammer-gateway](https://github.com/HoBom-s/hammer-gateway) | API Gateway |
| [hammer-user](https://github.com/HoBom-s/hammer-user) | User & Auth |
| [hammer-auction](https://github.com/HoBom-s/hammer-auction) | Auction API |
| [hammer-collector](https://github.com/HoBom-s/hammer-collector) | Data Collector |
| [hammer-logging](https://github.com/HoBom-s/hammer-logging) | Logging |

## Getting Started

```bash
dotnet restore
dotnet run --project src/Hammer.Gateway
```

## Branch Strategy

- `main` — Production
- `develop` — Development (default)
