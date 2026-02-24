# ato-copilot Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-21

## Active Technologies
- C# 13 / .NET 9.0 + Azure.Identity 1.13, Azure.ResourceManager 1.13, Microsoft.EntityFrameworkCore 9.0, Serilog 4.2, xUnit 2.9, FluentAssertions 7.0, Moq 4.20 (002-remediation-kanban)
- SQLite (dev) / SQL Server (prod) via EF Core — `AtoCopilotContext` extended with 4 new DbSets; new migration (002-remediation-kanban)
- C# 13 / .NET 9.0 + Microsoft.Identity.Client (MSAL.NET) 4.68+, Microsoft.Identity.Web 3.5+, Microsoft.Graph 5.70+, Microsoft.AspNetCore.Authentication.JwtBearer 9.0.0 (existing), Azure.ResourceManager.SecurityCenter 1.2.0-beta.6 (existing for JIT VM), EF Core 9.0 (existing) (003-cac-auth-pim)
- SQLite (dev) / SQL Server (prod) — extending existing AtoCopilotContext with CacSession, JitRequestEntity, CertificateRoleMapping entities (003-cac-auth-pim)
- C# 13 / .NET 9.0 + Microsoft.SemanticKernel 1.34, Microsoft.Identity.Web 3.5, Microsoft.Graph 5.70, Azure.ResourceManager.SecurityCenter, EF Core 9.0, Serilog, xUnit 2.9.3, FluentAssertions 7.0.0, Moq 4.20.72 (003-cac-auth-pim)
- SQLite (dev) / SQL Server (prod) via EF Core; Azure Key Vault (prod secrets) (003-cac-auth-pim)
- C# 13 / .NET 9.0 + Microsoft.Extensions.AI 9.4.0-preview, Microsoft.Identity.Web 3.5.0, Serilog, Entity Framework Core 9.0.0 (004-kanban-user-context)
- SQLite (development), SQL Server (production) via EF Core (004-kanban-user-context)
- C# 13 / .NET 9.0 + EF Core 9.0, Azure.ResourceManager, Serilog, Microsoft.Extensions.Hosting, System.Threading.Channels (005-compliance-watch)
- SQLite (development) / SQL Server (production) via `AtoCopilotContext`; existing patterns — `IDbContextFactory<AtoCopilotContext>` for Singleton services (005-compliance-watch)
- C# 13 / .NET 9.0 (backend), TypeScript 4.9 / React 18.2 (frontend) + ASP.NET Core SignalR, EF Core 9.0, Serilog, SPA Services (backend); React, react-markdown, @microsoft/signalr, Tailwind CSS, Axios (frontend) (006-chat-app)
- SQLite (local dev) / SQL Server (Docker/production) — dual-provider, auto-detected from connection string (006-chat-app)
- [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION] + [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION] (007-nist-controls)
- [if applicable, e.g., PostgreSQL, CoreData, files or N/A] (007-nist-controls)

- C# 13 / .NET 9.0 + Azure.Identity 1.13, Azure.ResourceManager 1.13, Microsoft.Extensions.AI 9.4-preview, Microsoft.EntityFrameworkCore 9.0, Serilog 4.2, xUnit 2.9, FluentAssertions 7.0, Moq 4.20 (001-core-compliance)

## Project Structure

```text
src/
tests/
```

## Commands

dotnet build Ato.Copilot.sln [ONLY COMMANDS FOR ACTIVE TECHNOLOGIES][ONLY COMMANDS FOR ACTIVE TECHNOLOGIES] dotnet test

## Code Style

C# .NET 9: Follow standard conventions

## Recent Changes
- 007-nist-controls: Added [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION] + [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]
- 006-chat-app: Added C# 13 / .NET 9.0 (backend), TypeScript 4.9 / React 18.2 (frontend) + ASP.NET Core SignalR, EF Core 9.0, Serilog, SPA Services (backend); React, react-markdown, @microsoft/signalr, Tailwind CSS, Axios (frontend)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
