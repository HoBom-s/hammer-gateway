# Hammer Gateway

## Project Overview
Hammer 경매 플랫폼의 API Gateway. YARP 기반 리버스 프록시.

## Architecture
- **역할**: JWT 검증, 라우팅, Rate Limiting
- **기술**: ASP.NET + YARP (Yet Another Reverse Proxy)
- **인증 흐름**: JWT 검증 → 클레임에서 userId 추출 → X-User-Id 헤더 주입 → 하위 서비스 프록시
- **하위 서비스는 X-User-Id 헤더만 신뢰** (직접 JWT 검증하지 않음)

## Hammer Services
| Service | Repo | Role |
|---------|------|------|
| Gateway | hammer-gateway (this) | JWT 검증, 라우팅, Rate Limit |
| User | hammer-user | 회원, 인증, JWT 발급 |
| Auction | hammer-auction | 경매 데이터 조회 API |
| Collector | hammer-collector | 외부 API 수집/정규화 |
| Logging | hammer-logging | 로그 수집/저장 |

## Conventions
- DB: PostgreSQL (서비스별 독립)
- 캐시: Redis
- 메시징: Kafka
- Branch: main(라이브), develop(메인 개발)
- .NET 10

## Rules
- Co-Authored-By 절대 금지
- Committer: foxmon
