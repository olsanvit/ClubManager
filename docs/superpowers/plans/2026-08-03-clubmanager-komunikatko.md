# ClubManager — Komunikátko: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Vybudovat nový Blazor Server .NET 10 projekt ClubManager s modulem Komunikátko — multi-tenant messaging systém pro sportovní kluby s vlákny, SignalR real-time updaty a email notifikacemi.

**Architecture:** Multi-tenant monolit (Tenant = Organizace). Tři projekty v solution: `ClubManager.Web` (Blazor Server), `ClubManager.Domain` (entity, rozhraní), `ClubManager.Tests` (xUnit). Messaging funguje přes `MessageService` → DB → SignalR hub → připojení klientů; notifikace putují asynchronně přes `Channel<NotificationJob>` do `NotificationDispatcher` hosted service.

**Tech Stack:** .NET 10, Blazor Server (`@rendermode InteractiveServer`), PostgreSQL (Npgsql EF), ASP.NET Identity, SignalR, Serilog, xUnit, Bootstrap 5.

---

## Stav implementace (sjednoceno s kódem 2026-09-14)

> **Zdroj pravdy je kód.** Úryvky kódu v taskách níže jsou původní návrh a v řadě míst neodpovídají implementaci (Guid → int ID, jiné názvy entit a služeb). Checkboxy odrážejí skutečný stav k commitu `acf7343`; poznámky za kroky popisují odchylky.

### Přehled tasků

| Task | Stav | Poznámka |
|---|---|---|
| 1 Scaffold | ✅ | `ClubManager.slnx`, bez projektu `ClubManager.Domain`, testy v `src/ClubManager.Tests` (není ve slnx) |
| 2 Domain | ✅ jinak | entity v `ClubManager.Web/Models`, int ID, bez rozhraní |
| 3 DbContext + migrace | ✅ | `InitialCreate` + `AddKomunikatko` (tabulky Komunikátka, `Clubs.JoinCode`) |
| 4 Program.cs | ✅ | `AddMabDbContext` / `AddMabAuth` ze SharedServices, `/health` |
| 5 Auth stránky | ✅ | `/login`, `/register` ze SharedServices; `AcceptInvite`, `JoinByCode` vlastní |
| 6 InvitationService | ⚠️ bez testů | relativní odkaz v emailu → P1 #8 |
| 7 MessageService | ⚠️ bez testů | implementováno jako `ChatService` |
| 8 SignalR hub | ⚠️ | hub + broadcast hotové; chybí kontrola členství → P1 #5 |
| 9 Notifikace | ⚠️ částečně | `ChatNotificationDispatcher` jen email, `MinPriority` ignorováno, bez testů |
| 10 Messaging UI | ⚠️ částečně | `/chat` místo `/messages`; nejde vytvořit vlákno, bez nepřečtených |
| 11 Clubs + Members | ✅ | chybí kontrola rolí → P1 #5 |
| 12 Deploy | ❌ | blokováno P0 #2–3 a výpadkem QNAP (2026-09-14) |

### Plán → skutečný kód

| Plán | Kód |
|---|---|
| `ClubManager.Domain/Entities`, `Enums` | `ClubManager.Web/Models` |
| `AppDbContext` + `AppDbContextDesignTimeFactory` | `Data/AppDbContextClubManager.cs` (bez design-time factory) |
| `AppUser` ve Web (FirstName, LastName, NtfyTopic) | `MercenariesAndBeasts.Infrastructure.AppUser` ze SharedServices (IsAdmin, IsWhitelisted, …) |
| `Membership` + `ClubRole { SuperAdmin, ClubManager, Parent, Child }` | `OrganizationMember` (`OrgRole { Member, ClubManager, OrgAdmin }`) + `ClubMember` |
| `ParentChildLink` | `FamilyLink` |
| `ClubMessage` / `MessageRead` | `ChatMessage` / `ChatMessageRead` |
| `MessagePriority` | neexistuje — priorita se odvozuje z `ThreadType` |
| `IMessageService`, `IInvitationService`, `INotificationService` | žádná rozhraní, konkrétní třídy |
| `MessageService` (vlákna) | `ChatService` — název `MessageService` patří modulu Oběžníky |
| `NotificationService` + `BackgroundServices/NotificationDispatcher` | `ClubNotificationService` (`Services/NotificationService.cs`) + `Services/ChatNotificationDispatcher.cs` |
| `Messaging/Messages.razor` + `ThreadDetail.razor` (`/messages`) | `Chat/ChatPage.razor` (`/chat`) |
| `Clubs/ClubMembers.razor` | `Members/ClubMembers.razor` (`/clubs/{ClubId}/members`) |
| `ConnectionStrings:ClubManagerDb`, pgbouncer `:5433`, `PathBase` | `DefaultConnection` → `pg16:5432` přímo, bez `PathBase` |
| `Organization.Slug`, `ClubThread.IsDirectMessage` | chybí (DM je V2) |

### Hotové mimo plán

- **Oběžníky** — `Message`, `MessageRecipient`, `MessageService`; `/messages`, `/messages/send`, `/messages/{id}`
- **Nedoplatky** — `/payments/debts`
- **Organizace a členové** — `/organizations`, `/organizations/{id}/admin`, `/members`
- **Rezervace aut** (ve spec V2) — `Car`, `CarReservation`, `CarReservationService`; `/cars`, `/cars/reservations`
- CI (GitHub Actions) + integrační smoke testy (`/`, `/health`), CS/EN lokalizace, Google OAuth

### Backlog

**P0 — blokery**
1. ✅ **Migrace Komunikátka** (2026-09-14) — `20260914160303_AddKomunikatko`: 6 tabulek, `Clubs.JoinCode` + unikátní index, backfill kódů pro existující kluby. Potlačení `PendingModelChangesWarning` zrušeno, chyba migrace se loguje jako Error. Ověřeno na čisté PostgreSQL (Up se 2 existujícími kluby, Down, žádné pending změny). **Na produkci zatím neaplikováno** — proběhne při dalším deployi (`MigrateAsync` při startu).
2. **Heslo admin účtu v kódu.** Seed v `Program.cs` má heslo natvrdo — přesunout do konfigurace/env a heslo účtu změnit.
3. **Rotace hesla DB role `clubmanager_usr`** (bylo v gitu do `acf7343`) — provádí uživatel.

**P1 — funkčnost a bezpečnost**

4. Chybí UI pro vytvoření vlákna (`ChatService.CreateThreadAsync` nikdo nevolá) → chat je bez vláken nepoužitelný. Dle spec smí jen ClubManager/SuperAdmin.
5. Oprávnění: `MessagingHub.JoinThread` a `ChatService.SendMessageAsync` nekontrolují členství v oddílu; `ClubMembers` (pozvánky, regenerace JoinCode) nekontroluje roli — zvládne kdokoli přihlášený.
6. `ChatPage` vytváří `HubConnection` ze serveru bez auth cookie → `[Authorize]` hub pravděpodobně vrátí 401 (neověřeno — ověřit po P0 #1). Zvážit in-process notifikaci místo HubConnection.
7. Email notifikace vkládá `Body` a názvy do HTML bez escapování.
8. Odkaz v pozvánce je relativní (`/accept-invite?token=…`) → v emailu nefunkční; přidat `BaseUrl` do konfigurace.

**P2 — dluh**

9. Unit testy z plánu (`InvitationServiceTests`, `ChatServiceTests`, `NotificationDispatcherTests`) chybí; `ClubManager.Tests` přidat do `ClubManager.slnx`.
10. `MarkReadAsync` / `GetUnreadCountAsync` nejsou v UI použity — žádné nepřečtené.
11. `JoinByCode` přiřazuje roli `Member`, spec říká `Parent` — rozhodnout po sjednocení rolí.
12. Dispatcher ignoruje `NotificationPreference.MinPriority`; ruční povýšení vlákna na Urgent chybí.
13. `ChatPage` a `ClubMembers` injektují scoped `DbContext` v InteractiveServer (žije celý circuit) → přejít na `IDbContextFactory`.
14. Spec vyžaduje povinný telefon při registraci — ve SharedServices `Register` neověřeno.

**V2 (dle spec):** DM vlákna, managed child, UI pro `NotificationPreference`, ntfy pro chat (`ClubNotificationService.SendNtfyAsync` už existuje).

---

## File Map

> ⚠️ **Zastaralé** — původní návrh struktury. Skutečnost viz *Stav implementace → Plán → skutečný kód*.

```
~/Projects/ClubManager/
  ClubManager.sln
  Dockerfile
  .gitignore
  src/
    ClubManager.Web/
      ClubManager.Web.csproj
      Program.cs
      appsettings.json
      appsettings.Production.json
      AppDbContext.cs
      AppDbContextDesignTimeFactory.cs
      AppUser.cs
      Components/
        App.razor
        Routes.razor
        _Imports.razor
        Layout/
          MainLayout.razor
          NavMenu.razor
        Pages/
          Auth/
            Login.razor
            Register.razor
            AcceptInvite.razor      (/accept-invite?token=xxx)
            JoinByCode.razor        (/join?code=ABC123)
          Admin/
            OrganizationsAdmin.razor
          Clubs/
            ClubList.razor          (/clubs)
            ClubDetail.razor        (/clubs/{id})
            ClubMembers.razor       (/clubs/{id}/members)
          Messaging/
            Messages.razor          (/messages — levý+pravý panel)
            ThreadDetail.razor      — komponenta pravého panelu
          Profile/
            Profile.razor
      Hubs/
        MessagingHub.cs
      Services/
        InvitationService.cs
        MessageService.cs
        NotificationService.cs      — email + ntfy dispatch
      BackgroundServices/
        NotificationDispatcher.cs
    ClubManager.Domain/
      ClubManager.Domain.csproj
      Entities/
        Organization.cs
        Club.cs
        Membership.cs
        ParentChildLink.cs
        Thread.cs
        ThreadParticipant.cs
        Message.cs
        MessageRead.cs
        Invitation.cs
        NotificationPreference.cs
      Enums/
        ClubRole.cs
        ThreadType.cs
        MessagePriority.cs
      Interfaces/
        IMessageService.cs
        INotificationService.cs
        IInvitationService.cs
  tests/
    ClubManager.Tests/
      ClubManager.Tests.csproj
      MessageServiceTests.cs
      InvitationServiceTests.cs
      NotificationDispatcherTests.cs
```

---

## Task 1: Project Scaffold

> **Stav:** ✅ hotovo (`f4a827b`). Odchylky: `ClubManager.slnx`, žádný `ClubManager.Domain`, testy v `src/ClubManager.Tests` (přidány v `5a054a7`).

**Files:**
- Create: `~/Projects/ClubManager/` (celý scaffold)

- [x] **Krok 1: Vytvoř solution a projekty** — _`ClubManager.slnx`, bez `ClubManager.Domain`_

```bash
cd ~/Projects
dotnet new sln -n ClubManager -o ClubManager
cd ClubManager
dotnet new blazorserver -n ClubManager.Web -o src/ClubManager.Web --no-restore
dotnet new classlib -n ClubManager.Domain -o src/ClubManager.Domain --no-restore
dotnet new xunit -n ClubManager.Tests -o tests/ClubManager.Tests --no-restore
dotnet sln add src/ClubManager.Web/ClubManager.Web.csproj
dotnet sln add src/ClubManager.Domain/ClubManager.Domain.csproj
dotnet sln add tests/ClubManager.Tests/ClubManager.Tests.csproj
```

- [x] **Krok 2: Přidej NuGet balíčky do ClubManager.Web**

```bash
cd ~/Projects/ClubManager
dotnet add src/ClubManager.Web package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add src/ClubManager.Web package Microsoft.EntityFrameworkCore
dotnet add src/ClubManager.Web package Microsoft.EntityFrameworkCore.Design
dotnet add src/ClubManager.Web package Microsoft.EntityFrameworkCore.Tools
dotnet add src/ClubManager.Web package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/ClubManager.Web package Microsoft.AspNetCore.SignalR
dotnet add src/ClubManager.Web package Serilog.AspNetCore
dotnet add src/ClubManager.Web package Serilog.Enrichers.Environment
dotnet add src/ClubManager.Web package Serilog.Enrichers.Process
dotnet add src/ClubManager.Web package Serilog.Enrichers.Thread
dotnet add src/ClubManager.Web package Serilog.Sinks.PostgreSQL.ColumnWriters
dotnet add src/ClubManager.Web package MailKit
dotnet add src/ClubManager.Web package Microsoft.Extensions.Http
```

- [x] **Krok 3: Přidej project reference**

```bash
dotnet add src/ClubManager.Web reference src/ClubManager.Domain/ClubManager.Domain.csproj
dotnet add tests/ClubManager.Tests reference src/ClubManager.Web/ClubManager.Web.csproj
dotnet add tests/ClubManager.Tests reference src/ClubManager.Domain/ClubManager.Domain.csproj
dotnet add tests/ClubManager.Tests package Moq
dotnet add tests/ClubManager.Tests package Microsoft.EntityFrameworkCore.InMemory
```

- [x] **Krok 4: Vytvoř Dockerfile**

Soubor `~/Projects/ClubManager/Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/ClubManager.Web/ClubManager.Web.csproj"
RUN dotnet publish "src/ClubManager.Web/ClubManager.Web.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClubManager.Web.dll"]
```

- [x] **Krok 5: Vytvoř .gitignore a inicializuj git**

```bash
cd ~/Projects/ClubManager
dotnet new gitignore
git init
git add .
git commit -m "chore: initial project scaffold — ClubManager Blazor Server"
```

Pak vytvoř GitHub repo `olsanvit/ClubManager` a pushni:
```bash
git remote add origin https://github.com/olsanvit/ClubManager.git
git push -u origin main
```

---

## Task 2: Domain — Entity třídy a Enums

> **Stav:** ✅ hotovo jinou formou — entity v `ClubManager.Web/Models`, int ID, role přes `OrgRole`, bez rozhraní. Viz tabulka *Plán → skutečný kód*.

**Files:**
- Create: `src/ClubManager.Domain/Entities/*.cs`
- Create: `src/ClubManager.Domain/Enums/*.cs`
- Create: `src/ClubManager.Domain/Interfaces/*.cs`

- [x] **Krok 1: Enums** — _enumy `ThreadType`, `OrgRole`, `NotifyMinPriority` v `Models`; `ClubRole` ani `MessagePriority` nejsou_

`src/ClubManager.Domain/Enums/ClubRole.cs`:
```csharp
namespace ClubManager.Domain.Enums;

public enum ClubRole
{
    SuperAdmin,
    ClubManager,
    Parent,
    Child
}
```

`src/ClubManager.Domain/Enums/ThreadType.cs`:
```csharp
namespace ClubManager.Domain.Enums;

public enum ThreadType
{
    General,
    Announcement,
    Debt,
    Event
}
```

`src/ClubManager.Domain/Enums/MessagePriority.cs`:
```csharp
namespace ClubManager.Domain.Enums;

public enum MessagePriority
{
    Normal,
    High,
    Urgent
}
```

- [x] **Krok 2: Entity třídy** — _`Models/*.cs`, int ID_

`src/ClubManager.Domain/Entities/Organization.cs`:
```csharp
namespace ClubManager.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Club> Clubs { get; set; } = [];
}
```

`src/ClubManager.Domain/Entities/Club.cs`:
```csharp
namespace ClubManager.Domain.Entities;

public class Club
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Name { get; set; } = "";
    public string JoinCode { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public ICollection<Membership> Memberships { get; set; } = [];
    public ICollection<ClubThread> Threads { get; set; } = [];
}
```

`src/ClubManager.Domain/Entities/Membership.cs`:
```csharp
using ClubManager.Domain.Enums;

namespace ClubManager.Domain.Entities;

public class Membership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public ClubRole Role { get; set; }
    public bool IsManagedChild { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
```

`src/ClubManager.Domain/Entities/ParentChildLink.cs`:
```csharp
namespace ClubManager.Domain.Entities;

public class ParentChildLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ParentUserId { get; set; } = "";
    public string ChildUserId { get; set; } = "";
}
```

`src/ClubManager.Domain/Entities/ClubThread.cs`:
```csharp
using ClubManager.Domain.Enums;

namespace ClubManager.Domain.Entities;

public class ClubThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ClubId { get; set; }
    public Club? Club { get; set; }
    public string Title { get; set; } = "";
    public ThreadType Type { get; set; } = ThreadType.General;
    public bool IsDirectMessage { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ClubMessage> Messages { get; set; } = [];
    public ICollection<ThreadParticipant> Participants { get; set; } = [];
}
```

`src/ClubManager.Domain/Entities/ThreadParticipant.cs`:
```csharp
namespace ClubManager.Domain.Entities;

public class ThreadParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public ClubThread Thread { get; set; } = null!;
    public string UserId { get; set; } = "";
}
```

`src/ClubManager.Domain/Entities/ClubMessage.cs`:
```csharp
namespace ClubManager.Domain.Entities;

public class ClubMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public ClubThread Thread { get; set; } = null!;
    public string SenderUserId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public ICollection<MessageRead> Reads { get; set; } = [];
}
```

`src/ClubManager.Domain/Entities/MessageRead.cs`:
```csharp
namespace ClubManager.Domain.Entities;

public class MessageRead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public ClubMessage Message { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
```

`src/ClubManager.Domain/Entities/Invitation.cs`:
```csharp
using ClubManager.Domain.Enums;

namespace ClubManager.Domain.Entities;

public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public Guid ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public ClubRole Role { get; set; } = ClubRole.Parent;
    public string InvitedByUserId { get; set; } = "";
    public string Token { get; set; } = Guid.NewGuid().ToString();
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? AcceptedAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsAccepted => AcceptedAt.HasValue;
}
```

`src/ClubManager.Domain/Entities/NotificationPreference.cs`:
```csharp
using ClubManager.Domain.Enums;

namespace ClubManager.Domain.Entities;

public class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid ClubId { get; set; }
    public bool EmailEnabled { get; set; } = true;
    public bool NtfyEnabled { get; set; } = false;
    public MessagePriority MinPriority { get; set; } = MessagePriority.High;
}
```

- [ ] **Krok 3: Interfaces** — _zrušeno — služby jsou bez rozhraní_

`src/ClubManager.Domain/Interfaces/IMessageService.cs`:
```csharp
using ClubManager.Domain.Entities;

namespace ClubManager.Domain.Interfaces;

public interface IMessageService
{
    Task<ClubThread> CreateThreadAsync(Guid clubId, string title, ThreadType type, string createdByUserId);
    Task<ClubMessage> SendMessageAsync(Guid threadId, string senderUserId, string body);
    Task<List<ClubThread>> GetClubThreadsAsync(Guid clubId);
    Task<List<ClubMessage>> GetThreadMessagesAsync(Guid threadId, int skip = 0, int take = 50);
    Task MarkReadAsync(Guid messageId, string userId);
    Task<int> GetUnreadCountAsync(Guid clubId, string userId);
}
```

`src/ClubManager.Domain/Interfaces/IInvitationService.cs`:
```csharp
using ClubManager.Domain.Entities;
using ClubManager.Domain.Enums;

namespace ClubManager.Domain.Interfaces;

public interface IInvitationService
{
    Task<Invitation> CreateInvitationAsync(string email, Guid clubId, ClubRole role, string invitedByUserId);
    Task<Invitation?> GetByTokenAsync(string token);
    Task<bool> AcceptAsync(string token, string userId);
    Task<bool> JoinByCodeAsync(string joinCode, string userId);
}
```

`src/ClubManager.Domain/Interfaces/INotificationService.cs`:
```csharp
using ClubManager.Domain.Entities;
using ClubManager.Domain.Enums;

namespace ClubManager.Domain.Interfaces;

public interface INotificationService
{
    Task EnqueueAsync(Guid threadId, Guid messageId, MessagePriority priority);
}
```

- [x] **Krok 4: Zbuild a commit**

```bash
cd ~/Projects/ClubManager
dotnet build
git add .
git commit -m "feat: domain entities, enums, interfaces"
git push origin main
```

Expected: Build succeeded, 0 errors.

---

## Task 3: AppUser + AppDbContext + EF Migration

> **Stav:** ✅ hotovo — `AppDbContextClubManager` + `InitialCreate` (`0c3ca7f`) + `AddKomunikatko` (2026-09-14, doplňuje tabulky Komunikátka).

**Files:**
- Create: `src/ClubManager.Web/AppUser.cs`
- Create: `src/ClubManager.Web/AppDbContext.cs`
- Create: `src/ClubManager.Web/AppDbContextDesignTimeFactory.cs`

- [x] **Krok 1: AppUser** — _`AppUser` ze SharedServices_

`src/ClubManager.Web/AppUser.cs`:
```csharp
using Microsoft.AspNetCore.Identity;

namespace ClubManager.Web;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public Guid? OrganizationId { get; set; }
    public string? NtfyTopic { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
}
```

- [x] **Krok 2: AppDbContext** — _`Data/AppDbContextClubManager.cs`_

`src/ClubManager.Web/AppDbContext.cs`:
```csharp
using ClubManager.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Web;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<ParentChildLink> ParentChildLinks => Set<ParentChildLink>();
    public DbSet<ClubThread> Threads => Set<ClubThread>();
    public DbSet<ThreadParticipant> ThreadParticipants => Set<ThreadParticipant>();
    public DbSet<ClubMessage> Messages => Set<ClubMessage>();
    public DbSet<MessageRead> MessageReads => Set<MessageRead>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Membership>()
            .HasIndex(m => new { m.UserId, m.ClubId }).IsUnique();

        builder.Entity<MessageRead>()
            .HasIndex(r => new { r.MessageId, r.UserId }).IsUnique();

        builder.Entity<NotificationPreference>()
            .HasIndex(n => new { n.UserId, n.ClubId }).IsUnique();

        builder.Entity<Club>()
            .HasIndex(c => c.JoinCode).IsUnique();

        builder.Entity<Organization>()
            .HasIndex(o => o.Slug).IsUnique();
    }
}
```

- [ ] **Krok 3: DesignTimeFactory** — _nevytvořeno — EF tools používají host z `Program.cs`_

`src/ClubManager.Web/AppDbContextDesignTimeFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace ClubManager.Web;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = "Host=localhost;Port=5432;Database=ClubManager;Username=postgres;Password=postgres";
        var dsb = new NpgsqlDataSourceBuilder(cs);
        var dataSource = dsb.Build();
        var opts = new DbContextOptionsBuilder<AppDbContext>();
        opts.UseNpgsql(dataSource);
        return new AppDbContext(opts.Options);
    }
}
```

- [x] **Krok 4: Vytvoř první migraci** — _`InitialCreate` + `AddKomunikatko` (tabulky Komunikátka)_

Potřebuješ lokální PostgreSQL nebo Docker:
```bash
docker run -d --name cm-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16-alpine
```

Pak:
```bash
cd ~/Projects/ClubManager/src/ClubManager.Web
dotnet ef migrations add InitialCreate --output-dir Migrations
dotnet ef database update
```

Expected: Migration created, database updated.

- [x] **Krok 5: Commit**

```bash
cd ~/Projects/ClubManager
git add .
git commit -m "feat: AppUser, AppDbContext, initial EF migration"
git push origin main
```

---

## Task 4: Program.cs — DI, Identity, SignalR, Serilog

> **Stav:** ✅ hotovo — DI přes `AddMabDbContext` / `AddMabAuth` ze SharedServices, Serilog i do PostgreSQL, `/health`, Google OAuth. Seed s heslem v kódu → P0 #2.

**Files:**
- Modify: `src/ClubManager.Web/Program.cs`
- Create: `src/ClubManager.Web/appsettings.json`
- Create: `src/ClubManager.Web/appsettings.Production.json`

- [x] **Krok 1: appsettings.json** — _`DefaultConnection`, `Smtp`, `Ntfy`_

`src/ClubManager.Web/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "ClubManagerDb": "Host=localhost;Port=5432;Database=ClubManager;Username=postgres;Password=postgres;Pooling=true;Timeout=15;Command Timeout=30;Ssl Mode=Disable"
  },
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "User": "",
    "Password": "",
    "From": "clubmanager@example.com"
  },
  "Ntfy": {
    "BaseUrl": "http://192.168.60.221:8225"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

`src/ClubManager.Web/appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "ClubManagerDb": "Host=192.168.60.221;Port=5433;Database=ClubManager;Username=clubmanager_usr;Password=CHANGE_ME;Pooling=true;Timeout=15;Command Timeout=30;Ssl Mode=Disable"
  },
  "PathBase": "/clubmanager",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [x] **Krok 2: Program.cs**

`src/ClubManager.Web/Program.cs`:
```csharp
using ClubManager.Domain.Interfaces;
using ClubManager.Web;
using ClubManager.Web.Hubs;
using ClubManager.Web.Services;
using ClubManager.Web.BackgroundServices;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using Serilog.Exceptions;

Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Logs"));
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.FromLogContext()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Database
var cs = builder.Configuration.GetConnectionString("ClubManagerDb")!;
var dsb = new NpgsqlDataSourceBuilder(cs);
var dataSource = dsb.Build();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(dataSource));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 8;
    o.Password.RequireNonAlphanumeric = false;
    o.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/login";
    o.LogoutPath = "/logout";
    o.AccessDeniedPath = "/access-denied";
});

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// SignalR
builder.Services.AddSignalR();

// App Services
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<NotificationDispatcher>());
builder.Services.AddHttpClient();

// Forwarded headers (reverse proxy na QNAP)
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Auto migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<ClubManager.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapHub<MessagingHub>("/hubs/messaging");

app.Run();
```

- [x] **Krok 3: Build test**

```bash
cd ~/Projects/ClubManager
dotnet build
```

Expected: Build succeeded.

- [x] **Krok 4: Commit**

```bash
git add .
git commit -m "feat: Program.cs — DI, Identity, SignalR, Serilog"
git push origin main
```

---

## Task 5: Auth pages — Login, Register, AcceptInvite, JoinByCode

> **Stav:** ✅ hotovo — Login/Register poskytuje SharedServices, `AcceptInvite` a `JoinByCode` jsou v `Components/Pages/Auth`.

**Files:**
- Create: `src/ClubManager.Web/Components/Pages/Auth/Login.razor`
- Create: `src/ClubManager.Web/Components/Pages/Auth/Register.razor`
- Create: `src/ClubManager.Web/Components/Pages/Auth/AcceptInvite.razor`
- Create: `src/ClubManager.Web/Components/Pages/Auth/JoinByCode.razor`

- [x] **Krok 1: Login.razor** — _SharedServices `/login`_

`src/ClubManager.Web/Components/Pages/Auth/Login.razor`:
```razor
@page "/login"
@using Microsoft.AspNetCore.Identity
@inject SignInManager<AppUser> SignIn
@inject NavigationManager Nav

<h2>Přihlášení</h2>

<EditForm Model="@_model" OnValidSubmit="DoLogin">
    <DataAnnotationsValidator />
    <div class="mb-3">
        <label class="form-label">Email</label>
        <InputText @bind-Value="_model.Email" class="form-control" />
    </div>
    <div class="mb-3">
        <label class="form-label">Heslo</label>
        <InputText type="password" @bind-Value="_model.Password" class="form-control" />
    </div>
    @if (_error is not null)
    {
        <div class="alert alert-danger">@_error</div>
    }
    <button type="submit" class="btn btn-primary">Přihlásit</button>
    <a href="/join" class="ms-3">Vstoupit kódem</a>
</EditForm>

@code {
    private LoginModel _model = new();
    private string? _error;

    private async Task DoLogin()
    {
        var result = await SignIn.PasswordSignInAsync(_model.Email, _model.Password, false, false);
        if (result.Succeeded) Nav.NavigateTo("/");
        else _error = "Nesprávný email nebo heslo.";
    }

    class LoginModel
    {
        [Required] public string Email { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }
}
```

- [x] **Krok 2: Register.razor** — _SharedServices `/register`_

`src/ClubManager.Web/Components/Pages/Auth/Register.razor`:
```razor
@page "/register"
@using Microsoft.AspNetCore.Identity
@inject UserManager<AppUser> UserMgr
@inject SignInManager<AppUser> SignIn
@inject NavigationManager Nav

<h2>Registrace</h2>

<EditForm Model="@_model" OnValidSubmit="DoRegister">
    <DataAnnotationsValidator />
    <div class="mb-3">
        <label class="form-label">Jméno</label>
        <InputText @bind-Value="_model.FirstName" class="form-control" />
    </div>
    <div class="mb-3">
        <label class="form-label">Příjmení</label>
        <InputText @bind-Value="_model.LastName" class="form-control" />
    </div>
    <div class="mb-3">
        <label class="form-label">Email</label>
        <InputText @bind-Value="_model.Email" class="form-control" />
    </div>
    <div class="mb-3">
        <label class="form-label">Telefon</label>
        <InputText @bind-Value="_model.Phone" class="form-control" />
    </div>
    <div class="mb-3">
        <label class="form-label">Heslo</label>
        <InputText type="password" @bind-Value="_model.Password" class="form-control" />
    </div>
    @if (_error is not null)
    {
        <div class="alert alert-danger">@_error</div>
    }
    <button type="submit" class="btn btn-primary">Zaregistrovat</button>
</EditForm>

@code {
    private RegisterModel _model = new();
    private string? _error;

    private async Task DoRegister()
    {
        var user = new AppUser
        {
            FirstName = _model.FirstName,
            LastName = _model.LastName,
            Email = _model.Email,
            UserName = _model.Email,
            PhoneNumber = _model.Phone
        };
        var result = await UserMgr.CreateAsync(user, _model.Password);
        if (result.Succeeded)
        {
            await SignIn.SignInAsync(user, false);
            Nav.NavigateTo("/");
        }
        else
        {
            _error = string.Join("; ", result.Errors.Select(e => e.Description));
        }
    }

    class RegisterModel
    {
        [Required] public string FirstName { get; set; } = "";
        [Required] public string LastName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Phone { get; set; } = "";
        [Required, MinLength(8)] public string Password { get; set; } = "";
    }
}
```

- [x] **Krok 3: AcceptInvite.razor**

`src/ClubManager.Web/Components/Pages/Auth/AcceptInvite.razor`:
```razor
@page "/accept-invite"
@using Microsoft.AspNetCore.Identity
@inject IInvitationService InvSvc
@inject UserManager<AppUser> UserMgr
@inject SignInManager<AppUser> SignIn
@inject NavigationManager Nav

@if (_invitation is null)
{
    <div class="alert alert-danger">Pozvánka nenalezena nebo vypršela.</div>
}
else if (_invitation.IsAccepted)
{
    <div class="alert alert-info">Pozvánka již byla použita.</div>
}
else
{
    <h2>Přijmout pozvánku</h2>
    <p>Jste zváni do oddílu <strong>@_invitation.Club.Name</strong> jako @_invitation.Role.</p>

    <EditForm Model="@_model" OnValidSubmit="DoAccept">
        <DataAnnotationsValidator />
        <div class="mb-3">
            <label class="form-label">Jméno</label>
            <InputText @bind-Value="_model.FirstName" class="form-control" />
        </div>
        <div class="mb-3">
            <label class="form-label">Příjmení</label>
            <InputText @bind-Value="_model.LastName" class="form-control" />
        </div>
        <div class="mb-3">
            <label class="form-label">Telefon</label>
            <InputText @bind-Value="_model.Phone" class="form-control" />
        </div>
        <div class="mb-3">
            <label class="form-label">Heslo</label>
            <InputText type="password" @bind-Value="_model.Password" class="form-control" />
        </div>
        @if (_error is not null) { <div class="alert alert-danger">@_error</div> }
        <button type="submit" class="btn btn-primary">Dokončit registraci</button>
    </EditForm>
}

@code {
    [SupplyParameterFromQuery] public string? Token { get; set; }

    private Invitation? _invitation;
    private AcceptModel _model = new();
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        if (Token is not null)
            _invitation = await InvSvc.GetByTokenAsync(Token);
    }

    private async Task DoAccept()
    {
        if (_invitation is null || Token is null) return;
        var user = new AppUser
        {
            FirstName = _model.FirstName,
            LastName = _model.LastName,
            Email = _invitation.Email,
            UserName = _invitation.Email,
            PhoneNumber = _model.Phone
        };
        var result = await UserMgr.CreateAsync(user, _model.Password);
        if (!result.Succeeded)
        {
            _error = string.Join("; ", result.Errors.Select(e => e.Description));
            return;
        }
        await InvSvc.AcceptAsync(Token, user.Id);
        await SignIn.SignInAsync(user, false);
        Nav.NavigateTo("/clubs");
    }

    class AcceptModel
    {
        [Required] public string FirstName { get; set; } = "";
        [Required] public string LastName { get; set; } = "";
        [Required] public string Phone { get; set; } = "";
        [Required, MinLength(8)] public string Password { get; set; } = "";
    }
}
```

- [x] **Krok 4: JoinByCode.razor**

`src/ClubManager.Web/Components/Pages/Auth/JoinByCode.razor`:
```razor
@page "/join"
@attribute [Authorize]
@inject IInvitationService InvSvc
@inject AuthenticationStateProvider AuthState
@inject NavigationManager Nav

<h2>Vstoupit do oddílu kódem</h2>

<EditForm Model="@_model" OnValidSubmit="DoJoin">
    <DataAnnotationsValidator />
    <div class="mb-3">
        <label class="form-label">Kód skupiny</label>
        <InputText @bind-Value="_model.Code" class="form-control" placeholder="ABC12345" />
    </div>
    @if (_error is not null) { <div class="alert alert-danger">@_error</div> }
    @if (_success) { <div class="alert alert-success">Úspěšně jste vstoupili do oddílu!</div> }
    <button type="submit" class="btn btn-primary">Vstoupit</button>
</EditForm>

@code {
    private JoinModel _model = new();
    private string? _error;
    private bool _success;

    private async Task DoJoin()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        var userId = auth.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var ok = await InvSvc.JoinByCodeAsync(_model.Code.Trim().ToUpper(), userId);
        if (ok) { _success = true; await Task.Delay(1500); Nav.NavigateTo("/clubs"); }
        else _error = "Kód nenalezen nebo neplatný.";
    }

    class JoinModel
    {
        [Required] public string Code { get; set; } = "";
    }
}
```

- [x] **Krok 5: Commit**

```bash
cd ~/Projects/ClubManager
git add .
git commit -m "feat: auth pages — login, register, accept-invite, join-by-code"
git push origin main
```

---

## Task 6: InvitationService

> **Stav:** ⚠️ služba hotová (`9ec2f69`), testy chybí (P2 #9). Metoda `GetByTokenAsync` se jmenuje `GetValidByTokenAsync`.

**Files:**
- Create: `src/ClubManager.Web/Services/InvitationService.cs`
- Create: `tests/ClubManager.Tests/InvitationServiceTests.cs`

- [ ] **Krok 1: Napiš failing test** — _test chybí_

`tests/ClubManager.Tests/InvitationServiceTests.cs`:
```csharp
using ClubManager.Domain.Enums;
using ClubManager.Web;
using ClubManager.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Tests;

public class InvitationServiceTests
{
    private AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task JoinByCode_ValidCode_CreatesMembership()
    {
        await using var db = CreateDb();
        var org = new ClubManager.Domain.Entities.Organization { Name = "Test Org", Slug = "test" };
        db.Organizations.Add(org);
        var club = new ClubManager.Domain.Entities.Club { OrganizationId = org.Id, Name = "Test Club", JoinCode = "ABC12345" };
        db.Clubs.Add(club);
        await db.SaveChangesAsync();

        var svc = new InvitationService(db, null!);
        var result = await svc.JoinByCodeAsync("ABC12345", "user-123");

        Assert.True(result);
        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.UserId == "user-123" && m.ClubId == club.Id);
        Assert.NotNull(membership);
        Assert.Equal(ClubRole.Parent, membership.Role);
    }

    [Fact]
    public async Task JoinByCode_InvalidCode_ReturnsFalse()
    {
        await using var db = CreateDb();
        var svc = new InvitationService(db, null!);
        var result = await svc.JoinByCodeAsync("INVALID1", "user-123");
        Assert.False(result);
    }

    [Fact]
    public async Task GetByToken_ExpiredToken_ReturnsNull()
    {
        await using var db = CreateDb();
        var club = new ClubManager.Domain.Entities.Club { Name = "C", JoinCode = "XX" };
        db.Clubs.Add(club);
        var inv = new ClubManager.Domain.Entities.Invitation
        {
            Email = "a@b.com", ClubId = club.Id, Token = "tok",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Invitations.Add(inv);
        await db.SaveChangesAsync();

        var svc = new InvitationService(db, null!);
        var result = await svc.GetByTokenAsync("tok");
        Assert.Null(result);
    }
}
```

- [ ] **Krok 2: Spusť test — ověř že selže**

```bash
cd ~/Projects/ClubManager
dotnet test tests/ClubManager.Tests --filter "InvitationServiceTests"
```

Expected: FAIL — `InvitationService` neexistuje.

- [x] **Krok 3: Implementuj InvitationService**

`src/ClubManager.Web/Services/InvitationService.cs`:
```csharp
using ClubManager.Domain.Entities;
using ClubManager.Domain.Enums;
using ClubManager.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Web.Services;

public class InvitationService(AppDbContext db, INotificationService notifSvc) : IInvitationService
{
    public async Task<Invitation> CreateInvitationAsync(string email, Guid clubId, ClubRole role, string invitedByUserId)
    {
        var inv = new Invitation
        {
            Email = email,
            ClubId = clubId,
            Role = role,
            InvitedByUserId = invitedByUserId
        };
        db.Invitations.Add(inv);
        await db.SaveChangesAsync();
        return inv;
    }

    public async Task<Invitation?> GetByTokenAsync(string token)
    {
        return await db.Invitations
            .Include(i => i.Club)
            .FirstOrDefaultAsync(i => i.Token == token && !i.AcceptedAt.HasValue && i.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<bool> AcceptAsync(string token, string userId)
    {
        var inv = await db.Invitations
            .Include(i => i.Club)
            .FirstOrDefaultAsync(i => i.Token == token && !i.AcceptedAt.HasValue);
        if (inv is null) return false;

        inv.AcceptedAt = DateTime.UtcNow;
        var membership = new Membership { UserId = userId, ClubId = inv.ClubId, Role = inv.Role };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> JoinByCodeAsync(string joinCode, string userId)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.JoinCode == joinCode);
        if (club is null) return false;

        var existing = await db.Memberships.AnyAsync(m => m.UserId == userId && m.ClubId == club.Id);
        if (existing) return true;

        db.Memberships.Add(new Membership { UserId = userId, ClubId = club.Id, Role = ClubRole.Parent });
        await db.SaveChangesAsync();
        return true;
    }
}
```

- [ ] **Krok 4: Spusť testy — ověř pass**

```bash
dotnet test tests/ClubManager.Tests --filter "InvitationServiceTests"
```

Expected: 3 tests passed.

- [x] **Krok 5: Commit** — _v `9ec2f69`_

```bash
git add .
git commit -m "feat: InvitationService + tests"
git push origin main
```

---

## Task 7: MessageService

> **Stav:** ⚠️ implementováno jako `Services/ChatService.cs` (`9ec2f69`), testy chybí (P2 #9).

**Files:**
- Create: `src/ClubManager.Web/Services/MessageService.cs`
- Create: `tests/ClubManager.Tests/MessageServiceTests.cs`

- [ ] **Krok 1: Napiš failing testy** — _testy chybí_

`tests/ClubManager.Tests/MessageServiceTests.cs`:
```csharp
using ClubManager.Domain.Enums;
using ClubManager.Web;
using ClubManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ClubManager.Tests;

public class MessageServiceTests
{
    private AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task CreateThread_SavesThread_WithCorrectClubAndType()
    {
        await using var db = CreateDb();
        var notifMock = new Mock<ClubManager.Domain.Interfaces.INotificationService>();
        var svc = new MessageService(db, notifMock.Object);

        var clubId = Guid.NewGuid();
        var thread = await svc.CreateThreadAsync(clubId, "Test vlákno", ThreadType.General, "user-1");

        Assert.NotEqual(Guid.Empty, thread.Id);
        Assert.Equal("Test vlákno", thread.Title);
        Assert.Equal(clubId, thread.ClubId);
        Assert.Equal("user-1", thread.CreatedByUserId);
    }

    [Fact]
    public async Task SendMessage_SavesMessageAndEnqueuesNotification()
    {
        await using var db = CreateDb();
        var notifMock = new Mock<ClubManager.Domain.Interfaces.INotificationService>();
        var svc = new MessageService(db, notifMock.Object);

        var thread = new ClubManager.Domain.Entities.ClubThread
        {
            Id = Guid.NewGuid(), Title = "T", ClubId = Guid.NewGuid(),
            Type = ThreadType.Debt, CreatedByUserId = "user-1"
        };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();

        var msg = await svc.SendMessageAsync(thread.Id, "user-1", "Ahoj!");

        Assert.Equal("Ahoj!", msg.Body);
        notifMock.Verify(n => n.EnqueueAsync(thread.Id, msg.Id, MessagePriority.Urgent), Times.Once);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCorrectCount()
    {
        await using var db = CreateDb();
        var svc = new MessageService(db, Mock.Of<ClubManager.Domain.Interfaces.INotificationService>());

        var clubId = Guid.NewGuid();
        var thread = new ClubManager.Domain.Entities.ClubThread
        {
            Id = Guid.NewGuid(), ClubId = clubId, Title = "T",
            Type = ThreadType.General, CreatedByUserId = "u1"
        };
        db.Threads.Add(thread);
        var msg1 = new ClubManager.Domain.Entities.ClubMessage { ThreadId = thread.Id, SenderUserId = "u1", Body = "A" };
        var msg2 = new ClubManager.Domain.Entities.ClubMessage { ThreadId = thread.Id, SenderUserId = "u1", Body = "B" };
        db.Messages.AddRange(msg1, msg2);
        db.MessageReads.Add(new ClubManager.Domain.Entities.MessageRead { MessageId = msg1.Id, UserId = "u2" });
        await db.SaveChangesAsync();

        var count = await svc.GetUnreadCountAsync(clubId, "u2");

        Assert.Equal(1, count);
    }
}
```

- [ ] **Krok 2: Spusť test — ověř fail**

```bash
dotnet test tests/ClubManager.Tests --filter "MessageServiceTests"
```

Expected: FAIL — `MessageService` neexistuje.

- [x] **Krok 3: Implementuj MessageService** — _`Services/ChatService.cs`_

`src/ClubManager.Web/Services/MessageService.cs`:
```csharp
using ClubManager.Domain.Entities;
using ClubManager.Domain.Enums;
using ClubManager.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Web.Services;

public class MessageService(AppDbContext db, INotificationService notifSvc) : IMessageService
{
    private static MessagePriority PriorityFor(ThreadType type) => type switch
    {
        ThreadType.Debt => MessagePriority.Urgent,
        ThreadType.Announcement or ThreadType.Event => MessagePriority.High,
        _ => MessagePriority.Normal
    };

    public async Task<ClubThread> CreateThreadAsync(Guid clubId, string title, ThreadType type, string createdByUserId)
    {
        var thread = new ClubThread { ClubId = clubId, Title = title, Type = type, CreatedByUserId = createdByUserId };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();
        return thread;
    }

    public async Task<ClubMessage> SendMessageAsync(Guid threadId, string senderUserId, string body)
    {
        var thread = await db.Threads.FindAsync(threadId) ?? throw new InvalidOperationException("Thread not found");
        var msg = new ClubMessage { ThreadId = threadId, SenderUserId = senderUserId, Body = body };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();
        await notifSvc.EnqueueAsync(threadId, msg.Id, PriorityFor(thread.Type));
        return msg;
    }

    public async Task<List<ClubThread>> GetClubThreadsAsync(Guid clubId)
        => await db.Threads
            .Where(t => t.ClubId == clubId && !t.IsDirectMessage)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<List<ClubMessage>> GetThreadMessagesAsync(Guid threadId, int skip = 0, int take = 50)
        => await db.Messages
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync();

    public async Task MarkReadAsync(Guid messageId, string userId)
    {
        var exists = await db.MessageReads.AnyAsync(r => r.MessageId == messageId && r.UserId == userId);
        if (exists) return;
        db.MessageReads.Add(new MessageRead { MessageId = messageId, UserId = userId });
        await db.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid clubId, string userId)
    {
        var threadIds = await db.Threads
            .Where(t => t.ClubId == clubId && !t.IsDirectMessage)
            .Select(t => t.Id)
            .ToListAsync();

        return await db.Messages
            .Where(m => threadIds.Contains(m.ThreadId))
            .CountAsync(m => !db.MessageReads.Any(r => r.MessageId == m.Id && r.UserId == userId));
    }
}
```

- [ ] **Krok 4: Spusť testy**

```bash
dotnet test tests/ClubManager.Tests --filter "MessageServiceTests"
```

Expected: 3 tests passed.

- [x] **Krok 5: Commit** — _v `9ec2f69`_

```bash
git add .
git commit -m "feat: MessageService + tests"
git push origin main
```

---

## Task 8: SignalR MessagingHub

> **Stav:** ⚠️ hub a broadcast hotové (`ChatMessageDto`, broadcast přímo v `ChatService.SendMessageAsync`); chybí kontrola členství (P1 #5).

**Files:**
- Create: `src/ClubManager.Web/Hubs/MessagingHub.cs`

- [x] **Krok 1: Vytvoř hub** — _bez kontroly členství_

`src/ClubManager.Web/Hubs/MessagingHub.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ClubManager.Web.Hubs;

[Authorize]
public class MessagingHub : Hub
{
    public async Task JoinThread(string threadId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"thread-{threadId}");

    public async Task LeaveThread(string threadId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"thread-{threadId}");

    public static async Task BroadcastMessage(IHubContext<MessagingHub> hub, Guid threadId, MessageDto msg)
        => await hub.Clients.Group($"thread-{threadId}").SendAsync("NewMessage", msg);
}

public record MessageDto(Guid Id, Guid ThreadId, string SenderUserId, string SenderName, string Body, DateTime CreatedAt);
```

- [x] **Krok 2: Aktualizuj SendMessageAsync v MessageService — přidej broadcast** — _broadcast v `ChatService.SendMessageAsync`_

V `src/ClubManager.Web/Services/MessageService.cs` přidej `IHubContext<MessagingHub>` do konstruktoru a broadcast po uložení:

```csharp
// Přidej do using:
using ClubManager.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;

// Upravený konstruktor:
public class MessageService(
    AppDbContext db,
    INotificationService notifSvc,
    IHubContext<MessagingHub> hub,
    UserManager<AppUser> userMgr) : IMessageService

// Upravený SendMessageAsync — přidej po db.SaveChangesAsync():
    var sender = await userMgr.FindByIdAsync(senderUserId);
    var dto = new MessageDto(msg.Id, threadId, senderUserId, sender?.FullName ?? "?", body, msg.CreatedAt);
    await MessagingHub.BroadcastMessage(hub, threadId, dto);
```

- [x] **Krok 3: Aktualizuj DI v Program.cs**

`MessageService` nyní potřebuje `IHubContext<MessagingHub>` a `UserManager<AppUser>` — ty jsou automaticky registrovány v DI, takže `AddScoped<IMessageService, MessageService>()` funguje bez změny.

- [x] **Krok 4: Build test**

```bash
cd ~/Projects/ClubManager
dotnet build
```

Expected: Build succeeded.

- [x] **Krok 5: Commit** — _v `9ec2f69`_

```bash
git add .
git commit -m "feat: MessagingHub + SignalR broadcast"
git push origin main
```

---

## Task 9: NotificationService + NotificationDispatcher

> **Stav:** ⚠️ částečně — `ChatNotificationDispatcher` (Channel + BackgroundService) posílá jen email, priorita z `ThreadType`, `MinPriority` ignoruje (P2 #12); testy chybí.

**Files:**
- Create: `src/ClubManager.Web/Services/NotificationService.cs`
- Create: `src/ClubManager.Web/BackgroundServices/NotificationDispatcher.cs`
- Create: `tests/ClubManager.Tests/NotificationDispatcherTests.cs`

- [ ] **Krok 1: Napiš failing test** — _test chybí_

`tests/ClubManager.Tests/NotificationDispatcherTests.cs`:
```csharp
using ClubManager.Domain.Enums;
using ClubManager.Web;
using ClubManager.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task Enqueue_AddsJobToChannel()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(opts);

        var dispatcher = new ClubManager.Web.BackgroundServices.NotificationDispatcher(
            null!, null!, null!, db);
        var svc = new NotificationService(dispatcher);

        await svc.EnqueueAsync(Guid.NewGuid(), Guid.NewGuid(), MessagePriority.Urgent);

        Assert.Equal(1, dispatcher.QueueCount);
    }
}
```

- [ ] **Krok 2: Spusť — ověř fail**

```bash
dotnet test tests/ClubManager.Tests --filter "NotificationServiceTests"
```

Expected: FAIL — typy neexistují.

- [x] **Krok 3: Implementuj NotificationDispatcher** — _`Services/ChatNotificationDispatcher.cs` — jen email_

`src/ClubManager.Web/BackgroundServices/NotificationDispatcher.cs`:
```csharp
using ClubManager.Domain.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Net.Http;
using System.Threading.Channels;

namespace ClubManager.Web.BackgroundServices;

public record NotificationJob(Guid ThreadId, Guid MessageId, MessagePriority Priority);

public class NotificationDispatcher(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ILogger<NotificationDispatcher> logger,
    AppDbContext db) : BackgroundService
{
    private readonly Channel<NotificationJob> _channel = Channel.CreateUnbounded<NotificationJob>();
    public int QueueCount => _channel.Reader.Count;

    public void Enqueue(NotificationJob job) => _channel.Writer.TryWrite(job);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            try { await ProcessAsync(job, ct); }
            catch (Exception ex) { logger?.LogError(ex, "Notification dispatch failed for message {Id}", job.MessageId); }
        }
    }

    private async Task ProcessAsync(NotificationJob job, CancellationToken ct)
    {
        var message = await db.Messages
            .Include(m => m.Thread).ThenInclude(t => t!.Club)
            .FirstOrDefaultAsync(m => m.Id == job.MessageId, ct);
        if (message is null) return;

        var memberships = await db.Memberships
            .Where(m => m.ClubId == message.Thread.ClubId)
            .ToListAsync(ct);

        foreach (var membership in memberships)
        {
            if (membership.UserId == message.SenderUserId) continue;

            var pref = await db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == membership.UserId && p.ClubId == message.Thread.ClubId, ct);

            var effectivePriority = pref?.MinPriority ?? MessagePriority.High;
            var shouldNotify = job.Priority == MessagePriority.Urgent || job.Priority >= effectivePriority;
            if (!shouldNotify) continue;

            var user = await db.Users.FindAsync([membership.UserId], ct);
            if (user is null) continue;

            // Email
            var emailEnabled = job.Priority == MessagePriority.Urgent || (pref?.EmailEnabled ?? true);
            if (emailEnabled && !string.IsNullOrEmpty(user.Email))
                await SendEmailAsync(user.Email, message.Thread.Title, message.Body, ct);

            // ntfy
            var ntfyEnabled = pref?.NtfyEnabled ?? false;
            if ((ntfyEnabled || job.Priority == MessagePriority.Urgent) && !string.IsNullOrEmpty(user.NtfyTopic))
                await SendNtfyAsync(user.NtfyTopic, message.Thread.Title, message.Body, job.Priority, ct);
        }
    }

    private async Task SendEmailAsync(string to, string subject, string body, CancellationToken ct)
    {
        try
        {
            var smtp = config.GetSection("Smtp");
            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"] ?? "587"), SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(smtp["User"], smtp["Password"], ct);
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(smtp["From"]));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = $"[ClubManager] {subject}";
            msg.Body = new TextPart("plain") { Text = body };
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "Email send failed to {To}", to); }
    }

    private async Task SendNtfyAsync(string topic, string title, string message, MessagePriority priority, CancellationToken ct)
    {
        try
        {
            var baseUrl = config["Ntfy:BaseUrl"] ?? "http://192.168.60.221:8225";
            var http = httpFactory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{topic}");
            req.Headers.Add("Title", title);
            req.Headers.Add("Priority", priority == MessagePriority.Urgent ? "urgent" : "default");
            req.Content = new StringContent(message);
            await http.SendAsync(req, ct);
        }
        catch (Exception ex) { logger?.LogWarning(ex, "ntfy send failed to {Topic}", topic); }
    }
}
```

- [x] **Krok 4: NotificationService** — _`ClubNotificationService` (email + ntfy)_

`src/ClubManager.Web/Services/NotificationService.cs`:
```csharp
using ClubManager.Domain.Enums;
using ClubManager.Domain.Interfaces;
using ClubManager.Web.BackgroundServices;

namespace ClubManager.Web.Services;

public class NotificationService(NotificationDispatcher dispatcher) : INotificationService
{
    public Task EnqueueAsync(Guid threadId, Guid messageId, MessagePriority priority)
    {
        dispatcher.Enqueue(new NotificationJob(threadId, messageId, priority));
        return Task.CompletedTask;
    }
}
```

- [ ] **Krok 5: Spusť testy**

```bash
dotnet test tests/ClubManager.Tests
```

Expected: All tests passed.

- [x] **Krok 6: Commit** — _v `9ec2f69` + fix `7bfd37f`_

```bash
git add .
git commit -m "feat: NotificationDispatcher + NotificationService (email + ntfy)"
git push origin main
```

---

## Task 10: Messaging Blazor pages

> **Stav:** ⚠️ částečně — jediná stránka `Chat/ChatPage.razor` na `/chat` (`/messages` patří Oběžníkům). Chybí vytvoření vlákna (P1 #4) a nepřečtené (P2 #10).

**Files:**
- Create: `src/ClubManager.Web/Components/Pages/Messaging/Messages.razor`
- Create: `src/ClubManager.Web/Components/Pages/Messaging/ThreadDetail.razor`

- [x] **Krok 1: Messages.razor — levý + pravý panel** — _`Chat/ChatPage.razor` (`/chat`) — oba panely v jedné stránce_

`src/ClubManager.Web/Components/Pages/Messaging/Messages.razor`:
```razor
@page "/messages"
@attribute [Authorize]
@rendermode InteractiveServer
@inject IMessageService MsgSvc
@inject AppDbContext Db
@inject AuthenticationStateProvider AuthState

<div class="d-flex" style="height: calc(100vh - 56px);">
    <!-- Levý panel: seznam vláken -->
    <div class="border-end" style="width:320px; overflow-y:auto;">
        @foreach (var group in _threadGroups)
        {
            <div class="px-3 pt-3">
                <small class="text-muted fw-bold text-uppercase">@group.ClubName</small>
                @foreach (var t in group.Threads)
                {
                    <div class="p-2 rounded cursor-pointer @(t.Id == _selectedThreadId ? "bg-primary text-white" : "hover-bg")"
                         @onclick="() => SelectThread(t.Id)">
                        <div class="d-flex justify-content-between">
                            <span>@t.Title</span>
                            @if (t.UnreadCount > 0)
                            {
                                <span class="badge bg-danger">@t.UnreadCount</span>
                            }
                        </div>
                        <small class="opacity-75">@t.Type</small>
                    </div>
                }
            </div>
        }
    </div>

    <!-- Pravý panel: detail vlákna -->
    <div class="flex-grow-1 overflow-hidden">
        @if (_selectedThreadId.HasValue)
        {
            <ThreadDetail ThreadId="@_selectedThreadId.Value" CurrentUserId="@_userId" />
        }
        else
        {
            <div class="d-flex align-items-center justify-content-center h-100 text-muted">
                Vyberte vlákno ze seznamu
            </div>
        }
    </div>
</div>

@code {
    private string _userId = "";
    private Guid? _selectedThreadId;
    private List<ClubThreadGroup> _threadGroups = [];

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        _userId = auth.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

        var memberships = await Db.Memberships
            .Include(m => m.Club)
            .Where(m => m.UserId == _userId)
            .ToListAsync();

        foreach (var m in memberships)
        {
            var threads = await MsgSvc.GetClubThreadsAsync(m.ClubId);
            var unread = await MsgSvc.GetUnreadCountAsync(m.ClubId, _userId);
            _threadGroups.Add(new ClubThreadGroup(m.Club.Name, threads.Select(t => new ThreadListItem(t.Id, t.Title, t.Type, 0)).ToList()));
        }
    }

    private void SelectThread(Guid id) => _selectedThreadId = id;

    record ClubThreadGroup(string ClubName, List<ThreadListItem> Threads);
    record ThreadListItem(Guid Id, string Title, ClubManager.Domain.Enums.ThreadType Type, int UnreadCount);
}
```

- [x] **Krok 2: ThreadDetail.razor — real-time feed** — _real-time přes `HubConnection` — viz P1 #6_

`src/ClubManager.Web/Components/Pages/Messaging/ThreadDetail.razor`:
```razor
@using Microsoft.AspNetCore.SignalR.Client
@inject NavigationManager Nav
@inject IMessageService MsgSvc
@inject AppDbContext Db
@implements IAsyncDisposable

<div class="d-flex flex-column h-100">
    <!-- Hlavička -->
    <div class="border-bottom px-3 py-2 fw-bold">@_thread?.Title</div>

    <!-- Zprávy -->
    <div class="flex-grow-1 overflow-y-auto p-3" @ref="_messagesDiv">
        @foreach (var msg in _messages)
        {
            var isMe = msg.SenderUserId == CurrentUserId;
            <div class="mb-2 d-flex @(isMe ? "justify-content-end" : "")">
                <div class="p-2 rounded @(isMe ? "bg-primary text-white" : "bg-light")" style="max-width:70%">
                    @if (!isMe) { <small class="d-block text-muted mb-1">@msg.SenderUserId</small> }
                    <span>@msg.Body</span>
                    <small class="d-block mt-1 opacity-75">@msg.CreatedAt.ToLocalTime().ToString("HH:mm")</small>
                </div>
            </div>
        }
    </div>

    <!-- Input -->
    <div class="border-top p-3 d-flex gap-2">
        <input class="form-control" @bind="_newMessage" @bind:event="oninput"
               @onkeydown="OnKeyDown" placeholder="Napište zprávu..." />
        <button class="btn btn-primary" @onclick="Send" disabled="@string.IsNullOrWhiteSpace(_newMessage)">
            Odeslat
        </button>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public Guid ThreadId { get; set; }
    [Parameter, EditorRequired] public string CurrentUserId { get; set; } = "";

    private ClubManager.Domain.Entities.ClubThread? _thread;
    private List<ClubManager.Domain.Entities.ClubMessage> _messages = [];
    private string _newMessage = "";
    private ElementReference _messagesDiv;
    private HubConnection? _hub;

    protected override async Task OnParametersSetAsync()
    {
        _thread = await Db.Threads.FindAsync(ThreadId);
        _messages = await MsgSvc.GetThreadMessagesAsync(ThreadId);

        _hub = new HubConnectionBuilder()
            .WithUrl(Nav.ToAbsoluteUri("/hubs/messaging"))
            .WithAutomaticReconnect()
            .Build();

        _hub.On<ClubManager.Web.Hubs.MessageDto>("NewMessage", async dto =>
        {
            if (dto.ThreadId != ThreadId) return;
            var msg = new ClubManager.Domain.Entities.ClubMessage
            {
                Id = dto.Id, ThreadId = dto.ThreadId, SenderUserId = dto.SenderUserId,
                Body = dto.Body, CreatedAt = dto.CreatedAt
            };
            _messages.Add(msg);
            await InvokeAsync(StateHasChanged);
        });

        await _hub.StartAsync();
        await _hub.SendAsync("JoinThread", ThreadId.ToString());
    }

    private async Task Send()
    {
        if (string.IsNullOrWhiteSpace(_newMessage)) return;
        await MsgSvc.SendMessageAsync(ThreadId, CurrentUserId, _newMessage.Trim());
        _newMessage = "";
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey) await Send();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.SendAsync("LeaveThread", ThreadId.ToString());
            await _hub.DisposeAsync();
        }
    }
}
```

- [x] **Krok 3: Build**

```bash
cd ~/Projects/ClubManager
dotnet build
```

Expected: Build succeeded.

- [x] **Krok 4: Commit**

```bash
git add .
git commit -m "feat: messaging pages — ThreadList + ThreadDetail s SignalR"
git push origin main
```

---

## Task 11: Clubs + Members pages

> **Stav:** ✅ hotovo — `/clubs` a `/clubs/{ClubId}/members` s pozvánkami a regenerací JoinCode; chybí kontrola rolí (P1 #5).

**Files:**
- Create: `src/ClubManager.Web/Components/Pages/Clubs/ClubList.razor`
- Create: `src/ClubManager.Web/Components/Pages/Clubs/ClubMembers.razor`

- [x] **Krok 1: ClubList.razor**

`src/ClubManager.Web/Components/Pages/Clubs/ClubList.razor`:
```razor
@page "/clubs"
@attribute [Authorize]
@rendermode InteractiveServer
@inject AppDbContext Db
@inject AuthenticationStateProvider AuthState
@inject NavigationManager Nav

<h2>Moje oddíly</h2>

@if (_memberships.Count == 0)
{
    <p>Nejste členem žádného oddílu. <a href="/join">Vstoupit kódem</a></p>
}
else
{
    <div class="row g-3">
        @foreach (var m in _memberships)
        {
            <div class="col-md-4">
                <div class="card h-100">
                    <div class="card-body">
                        <h5 class="card-title">@m.Club.Name</h5>
                        <p class="text-muted">@m.Role</p>
                        <a href="/clubs/@m.ClubId/members" class="btn btn-sm btn-outline-primary">Členové</a>
                        <a href="/messages" class="btn btn-sm btn-outline-secondary ms-1">Zprávy</a>
                    </div>
                </div>
            </div>
        }
    </div>
}

@code {
    private List<ClubManager.Domain.Entities.Membership> _memberships = [];

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        var userId = auth.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        _memberships = await Db.Memberships.Include(m => m.Club).Where(m => m.UserId == userId).ToListAsync();
    }
}
```

- [x] **Krok 2: ClubMembers.razor** — _`Members/ClubMembers.razor`, bez kontroly role_

`src/ClubManager.Web/Components/Pages/Clubs/ClubMembers.razor`:
```razor
@page "/clubs/{ClubId:guid}/members"
@attribute [Authorize]
@rendermode InteractiveServer
@inject AppDbContext Db
@inject IInvitationService InvSvc
@inject AuthenticationStateProvider AuthState

<h2>Členové — @_club?.Name</h2>

<!-- Pozvat emailem -->
<div class="card mb-4">
    <div class="card-body">
        <h5>Pozvat e-mailem</h5>
        <EditForm Model="@_invModel" OnValidSubmit="SendInvite">
            <DataAnnotationsValidator />
            <div class="row g-2">
                <div class="col">
                    <InputText @bind-Value="_invModel.Email" class="form-control" placeholder="email@example.com" />
                </div>
                <div class="col-auto">
                    <InputSelect @bind-Value="_invModel.Role" class="form-select">
                        @foreach (var role in Enum.GetValues<ClubManager.Domain.Enums.ClubRole>())
                        {
                            <option value="@role">@role</option>
                        }
                    </InputSelect>
                </div>
                <div class="col-auto">
                    <button type="submit" class="btn btn-primary">Pozvat</button>
                </div>
            </div>
        </EditForm>
        @if (_invSent) { <div class="alert alert-success mt-2">Pozvánka odeslána!</div> }
    </div>
</div>

<!-- Kód skupiny -->
<div class="card mb-4">
    <div class="card-body d-flex align-items-center gap-3">
        <div>
            <strong>Kód skupiny:</strong>
            <code class="fs-5 ms-2">@_club?.JoinCode</code>
        </div>
        <button class="btn btn-sm btn-outline-secondary" @onclick="RegenerateCode">Regenerovat</button>
    </div>
</div>

<!-- Seznam členů -->
<table class="table">
    <thead><tr><th>Jméno</th><th>Email</th><th>Role</th><th>Vstoupil</th></tr></thead>
    <tbody>
        @foreach (var m in _members)
        {
            <tr>
                <td>@m.UserName</td>
                <td>@m.Email</td>
                <td>@m.Role</td>
                <td>@m.JoinedAt.ToLocalTime().ToString("d. M. yyyy")</td>
            </tr>
        }
    </tbody>
</table>

@code {
    [Parameter] public Guid ClubId { get; set; }

    private ClubManager.Domain.Entities.Club? _club;
    private List<MemberRow> _members = [];
    private InviteModel _invModel = new();
    private bool _invSent;
    private string _userId = "";

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        _userId = auth.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        _club = await Db.Clubs.FindAsync(ClubId);

        _members = await (
            from m in Db.Memberships
            join u in Db.Users on m.UserId equals u.Id
            where m.ClubId == ClubId
            select new MemberRow(u.UserName ?? "", u.Email ?? "", m.Role, m.JoinedAt)
        ).ToListAsync();
    }

    private async Task SendInvite()
    {
        await InvSvc.CreateInvitationAsync(_invModel.Email, ClubId, _invModel.Role, _userId);
        _invSent = true;
        _invModel = new();
        // TODO: email s linkem /accept-invite?token=... (viz NotificationService)
    }

    private async Task RegenerateCode()
    {
        if (_club is null) return;
        _club.JoinCode = Guid.NewGuid().ToString("N")[..8].ToUpper();
        await Db.SaveChangesAsync();
    }

    record MemberRow(string UserName, string Email, ClubManager.Domain.Enums.ClubRole Role, DateTime JoinedAt);

    class InviteModel
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        public ClubManager.Domain.Enums.ClubRole Role { get; set; } = ClubManager.Domain.Enums.ClubRole.Parent;
    }
}
```

- [x] **Krok 3: Commit**

```bash
cd ~/Projects/ClubManager
dotnet build
git add .
git commit -m "feat: ClubList + ClubMembers pages (pozvánka + kód skupiny)"
git push origin main
```

---

## Task 12: Deploy setup na QNAP

> **Stav:** ❌ neprovedeno. Realita se liší od kroků: app jde přímo na `pg16:5432` (ne pgbouncer), deploy `~/deploy-to-qnap.sh clubmanager prod` → port 5024. Blokováno P0 #2–3 a výpadkem QNAP.

**Files:**
- Verify: `~/deploy-to-qnap.sh` (klubmanager sekce již existuje)

- [ ] **Krok 1: Vytvoř DB user na QNAP** — _existence role `clubmanager_usr` neověřena (QNAP offline)_

```bash
ssh -i ~/.ssh/claude-qnap admin@192.168.60.221 "DOCKER=/share/CACHEDEV1_DATA/.qpkg/container-station/bin/docker; \$DOCKER exec pg16 psql -U roundnet -d postgres -c \"CREATE USER clubmanager_usr WITH PASSWORD 'CHANGE_ME'; CREATE DATABASE ClubManager OWNER clubmanager_usr;\""
```

Poznač heslo do Vaultwarden.

- [ ] **Krok 2: Přidej clubmanager_usr do pgbouncer** — _app nejde přes pgbouncer — pravděpodobně nepotřeba_

Hash hesla: `md5` + MD5(`CHANGE_ME` + `clubmanager_usr`):
```bash
echo -n 'CHANGE_MEclubmanager_usr' | md5sum
```

Přidej řádek do `/share/Container/pgbouncer/userlist.txt`:
```
"clubmanager_usr" "md5<výstup_z_md5sum>"
```

Přidej do `/share/Container/pgbouncer/pgbouncer.ini` pod `[databases]`:
```
ClubManager = host=192.168.60.221 port=5444 dbname=ClubManager
```

Restartuj pgbouncer:
```bash
ssh -i ~/.ssh/claude-qnap admin@192.168.60.221 "DOCKER=/share/CACHEDEV1_DATA/.qpkg/container-station/bin/docker; \$DOCKER restart pgbouncer"
```

- [ ] **Krok 3: Deploy**

```bash
~/deploy-to-qnap.sh clubmanager prod
```

Expected: Build, copy, restart — container `clubmanager` on port 5024.

- [ ] **Krok 4: Ověř** — _použít `/health`_

```bash
curl -s --max-time 10 http://192.168.60.221:5024/ | head -5
```

Expected: HTML response (login page).

- [ ] **Krok 5: Final commit**

```bash
cd ~/Projects/ClubManager
git add .
git commit -m "chore: deploy verification — ClubManager Komunikátko MVP live"
git push origin main
```

---

## Self-Review — Spec Coverage

| Spec požadavek | Task |
|---|---|
| Auth — login, register | Task 5 |
| Pozvánka emailem | Task 6 + Task 11 (ClubMembers) |
| Kód skupiny (JoinByCode) | Task 5 + Task 6 |
| Organizations + Clubs | Task 2 (entity) + Task 11 (UI) |
| Membership s rolemi | Task 2 + Task 6 |
| Vlákna v oddílu (bez DM) | Task 7 (MessageService) |
| Real-time SignalR | Task 8 |
| Email notifikace (Urgent/High) | Task 9 (NotificationDispatcher) |
| ntfy integrace | Task 9 (volitelné, dle preference) |
| NotificationPreference | Task 2 (entita), Task 9 (logika) |
| Deploy na QNAP | Task 12 |
| pgbouncer registrace | Task 12 |

**V2 (mimo MVP scope tohoto plánu):** DM vlákna, managed child, NotificationPreference UI, Rezervace aut modul.
