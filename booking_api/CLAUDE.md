# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech Stack

- **Framework:** ASP.NET Core 10.0 with Minimal APIs (no controllers, no Startup.cs)
- **Language:** C# with nullable reference types enabled
- **ORM:** Entity Framework Core with Npgsql (PostgreSQL provider)
- **Database:** PostgreSQL
- **Auth:** ASP.NET Identity + JWT Bearer (HS256)
- **IDs:** UUID v7 via `Guid.CreateVersion7()` on all entities

## Commands

All commands run from the solution root (`booking_api/` containing `booking_api.sln`).

```bash
dotnet build                              # Build the project
dotnet run --project booking_api          # Run the API
dotnet ef migrations add <Name> --project booking_api --startup-project booking_api  # Create migration
dotnet ef database update --project booking_api --startup-project booking_api        # Apply migrations
docker build -t booking_api booking_api   # Docker build
```

## Architecture

### Project Structure (inside `booking_api/booking_api/`)

- `Program.cs` — Entry point, middleware pipeline, service registration
- `Models/` — EF Core entities. All inherit from `BaseEntity`
- `Data/AppDbContext.cs` — DbContext with global soft-delete query filter and automatic audit timestamps
- `Data/DataSeeder.cs` — Seeds default admin user on startup (`admin@sportshub.com` / `Admin123!`)
- `DTOs/` — Request/response objects decoupled from entities
- `Endpoints/` — Minimal API route definitions (one file per resource)
- `Services/` — Business logic layer
- `Extensions/` — DI registration extension methods (keeps Program.cs clean)

### BaseEntity Pattern

Every entity inherits `BaseEntity` which provides:
- `Id` (Guid, UUID v7) — auto-generated
- `CreatorUserId`, `CreationTime` — set on creation
- `LastModifiedByUserId`, `LastModificationTime` — set on update
- `DeletedByUserId`, `DeletionTime`, `IsDeleted` — soft delete

`AppDbContext` automatically:
- Applies a global query filter excluding soft-deleted records (`IsDeleted == true`)
- Sets audit fields on `SaveChanges` using the current user's JWT claims

### User Model

`User` extends `IdentityUser<Guid>` (not `BaseEntity`), but manually includes the same audit and soft-delete fields. It also adds `FirstName`, `LastName`, `Role` (enum: Player/Admin), and ban tracking fields (`IsBanned`, `BannedByUserId`, `BannedAt`).

### Authentication & Authorization

- JWT tokens issued by `AuthService` with claims: `sub` (user ID), `email`, `role`, `jti`
- Configured in `appsettings.json` under `Jwt` section (Key, Issuer, Audience, ExpirationMinutes)
- Identity configured with relaxed password rules (min 6 chars, no special char requirements)
- Authorization policy: `RequireRole("Admin")` used on admin endpoints

### API Routes

- `/api/auth/register` (POST) — public, returns JWT + user
- `/api/auth/login` (POST) — public, checks ban status
- `/api/auth/me` (GET) — requires auth
- `/api/admin/users` (GET) — admin only
- `/api/admin/users/{userId}/ban` (POST) — admin only
- `/api/admin/users/{userId}/unban` (POST) — admin only

### Ports

- HTTP: `localhost:5198`, HTTPS: `localhost:7077`
- Docker: ports 8080/8081
- CORS: allows `http://localhost:3000` (frontend)
- OpenAPI schema: `/openapi/v1.json` (development only)

### Connection String

Configured in `appsettings.json` under `ConnectionStrings:DefaultConnection`. Default points to `localhost` PostgreSQL with database `booking_db`.
