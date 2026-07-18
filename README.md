# Lares

**Your home, watched over.** Lares (named after the Roman guardian spirits of the household) is a smart home
platform that manages devices from different brands in a single panel — with an AI chat assistant that
understands natural-language commands. Devices are simulated behind a connector abstraction, keeping the door
open for real-device integrations later. UI architecture is inspired by the
[Home Assistant demo](https://demo.home-assistant.io/).

## Stack

| Layer | Tech |
|---|---|
| API | ASP.NET Core Web API (.NET 10), EF Core + Npgsql, ASP.NET Identity + JWT |
| DB | PostgreSQL (Docker Compose) |
| Web | React + TypeScript + Vite, Tailwind CSS, TanStack Query, react-i18next (EN/AZ) |
| Mobile | React Native + Expo (planned) |
| AI | Anthropic Claude API (tool use) |
| Tests | xUnit + Testcontainers |

## Monorepo layout

```
backend/Lares.Api        ASP.NET Core Web API
backend/Lares.Api.Tests  Integration tests (Testcontainers)
frontend/web             React web app
mobile/                  React Native app (planned)
```

## Run locally

```bash
docker compose up -d                    # PostgreSQL
cd backend/Lares.Api
dotnet ef database update
dotnet run                              # API → http://localhost:5202/swagger

cd frontend/web
npm install
npm run dev                             # Web → http://localhost:5173
```
