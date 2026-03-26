# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech Stack

- **Framework:** ASP.NET Core 10.0 with Minimal APIs (no controllers, no Startup.cs)
- **Language:** C# with nullable reference types enabled
- **ORM:** Entity Framework Core with Npgsql (PostgreSQL provider)
- **Database:** PostgreSQL
- **IDs:** UUID v7 via `Guid.CreateVersion7()` on all entities

## Commands

```bash
dotnet build                              # Build the project
dotnet run --project booking_api          # Run the API
dotnet ef migrations add <Name> --project booking_api  # Create migration
dotnet ef database update --project booking_api        # Apply migrations
docker build -t booking_api booking_api   # Docker build
```

All commands run from the solution root (`booking_api/` containing `booking_api.sln`).

## Architecture

### Project Structure (inside `booking_api/booking_api/`)

- `Program.cs` — Entry point, middleware pipeline, service registration
- `Models/` — EF Core entities. All inherit from `BaseEntity`
- `Data/AppDbContext.cs` — DbContext with global soft-delete query filter and automatic audit timestamps
- `DTOs/` — Request/response objects decoupled from entities
- `Endpoints/` — Minimal API route definitions (one file per resource)
- `Services/` — Business logic layer
- `Extensions/` — DI registration extension methods (keeps Program.cs clean)
- `Middleware/` — Cross-cutting concerns

### BaseEntity Pattern

Every entity inherits `BaseEntity` which provides:
- `Id` (Guid, UUID v7) — auto-generated
- `CreatorUserId`, `CreationTime` — set on creation
- `LastModifiedByUserId`, `LastModificationTime` — set on update
- `DeletedByUserId`, `DeletionTime`, `IsDeleted` — soft delete

`AppDbContext` automatically:
- Applies a global query filter excluding soft-deleted records (`IsDeleted == true`)
- Sets `CreationTime` and `LastModificationTime` audit fields on `SaveChanges`

### Ports

- HTTP: `localhost:5198`, HTTPS: `localhost:7077`
- Docker: ports 8080/8081
- OpenAPI schema: `/openapi/v1.json` (development only)

### Connection String

Configured in `appsettings.json` under `ConnectionStrings:DefaultConnection`. Default points to `localhost` PostgreSQL with database `booking_db`.
