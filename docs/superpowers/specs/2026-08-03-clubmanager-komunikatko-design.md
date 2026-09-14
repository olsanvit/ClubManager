# ClubManager — Komunikátko: Design Spec
**Datum:** 2026-08-03  
**Status:** Schváleno, čeká na implementaci  
**Modul:** Komunikátko (modul 1 ze 2; modul 2 = Rezervace aut)

---

## Přehled

Multi-tenant Blazor Server aplikace pro sportovní kluby a organizace. Tento dokument popisuje modul **Komunikátko** — plnohodnotný messaging systém s vlákny, přímými zprávami a notifikacemi.

**Tech stack:** .NET 10, Blazor Server (`@rendermode InteractiveServer`), PostgreSQL, ASP.NET Identity, SignalR, SMTP, ntfy.

---

## Architektura

### Typ aplikace
Jeden monolit, dva moduly (Komunikátko + Rezervace aut). Tenant = Organizace (každý klub/firma je jeden tenant).

### Struktura projektu

```
ClubManager.Web/
  Components/Pages/
    Auth/              — Login, Register, AcceptInvite, JoinByCode
    Admin/             — OrganizationsAdmin, UsersAdmin
    Clubs/             — ClubList, ClubDetail, ClubSettings, Members
    Messaging/         — ThreadList, ThreadDetail, NewThread, DirectMessages
    Profile/           — Profile, NotificationSettings
  Hubs/
    MessagingHub.cs    — SignalR hub

ClubManager.Domain/
  Entities/
  Enums/               — Role, ThreadType, MessagePriority
  Interfaces/          — IMessageService, INotificationService, IInvitationService

ClubManager.Infrastructure/
  AppDbContext.cs
  Repositories/
  Services/
    MessageService.cs
    NotificationService.cs
    InvitationService.cs
  BackgroundServices/
    NotificationDispatcher.cs
```

### Datový tok nové zprávy

1. Uživatel odešle zprávu v Blazoru → `MessageService.SendAsync()`
2. Message uložena do DB
3. `MessagingHub` pushne zprávu všem připojeným účastníkům vlákna (real-time přes SignalR `/hubs/messaging`)
4. `NotificationDispatcher` (background service, `Channel<NotificationJob>`) odešle email/ntfy asynchronně dle preferencí

---

## Datový model

```
Organization
  Id, Name, Slug, CreatedAt

Club (Oddíl)
  Id, OrganizationId, Name
  JoinCode              — unikátní kód pro vstup do oddílu

User (extends IdentityUser)
  Id, Email, PhoneNumber, FirstName, LastName, OrganizationId

Membership
  UserId, ClubId
  Role: SuperAdmin | ClubManager | Parent | Child
  IsManagedChild        — dítě bez vlastního loginu (pod rodičem)

ParentChildLink
  ParentUserId, ChildUserId

Thread
  Id, ClubId (null = DM), Title
  Type: General | Announcement | Debt | Event
  IsDirectMessage, CreatedByUserId, CreatedAt

ThreadParticipant       — účastníci DM vláken
  ThreadId, UserId

Message
  Id, ThreadId, SenderUserId, Body, CreatedAt, EditedAt

MessageRead
  MessageId, UserId, ReadAt

Invitation
  Id, Email, ClubId, Role, Token (GUID), ExpiresAt, AcceptedAt

NotificationPreference
  UserId, ClubId
  EmailEnabled, NtfyEnabled
  MinPriority: All | High | Urgent
```

---

## Role a oprávnění

| Akce | SuperAdmin | ClubManager | Parent | Child |
|---|---|---|---|---|
| Vytvořit organizaci | ✅ | ❌ | ❌ | ❌ |
| Vytvořit oddíl | ✅ | ✅ | ❌ | ❌ |
| Pozvat člena | ✅ | ✅ | ❌ | ❌ |
| Regenerovat JoinCode | ✅ | ✅ | ❌ | ❌ |
| Vytvořit vlákno (oddíl) | ✅ | ✅ | ❌ | ❌ |
| Psát do vlákna | ✅ | ✅ | ✅ | ✅ |
| Zahájit DM | ✅ | ✅ | ✅ | ✅ |
| Označit vlákno jako Debt | ✅ | ✅ | ❌ | ❌ |

---

## UI flow

### Navigace
```
Dashboard | Oddíly | Zprávy | Profil  [Admin]
```

### `/messages` — hlavní messaging stránka
- **Levý panel:** seznam vláken seskupený po oddílech + DM sekce; badge nepřečtených
- **Pravý panel (ThreadDetail):** feed zpráv, input, hlavička s účastníky

### `/clubs/{id}/members`
- Seznam členů, role, stav pozvánky
- "Pozvat emailem" → dialog (email, role, volitelně dítě pod rodičem)
- "Kód skupiny" → zobrazí JoinCode + tlačítko regenerovat

### `/accept-invite?token=xxx`
- Formulář: jméno, heslo, telefon → účet vytvořen, přiřazen do oddílu

### `/join?code=ABC123`
- Přihlásit / registrovat → zadat kód → přiřazen do oddílu

### Managed child (dítě pod rodičem)
Rodič vidí v levém panelu přepínač "Zobrazuji za: [sebe] [Marek]". Při přepnutí vidí vlákna a DM dítěte a může psát za něj.

---

## Notifikační systém

### Priority

| Thread type | Priorita | Chování |
|---|---|---|
| `Debt` | Urgent | Vždy email + ntfy, ignoruje preference |
| `Announcement` | High | Email + ntfy dle `MinPriority` preference |
| `Event` | High | Email + ntfy dle `MinPriority` preference |
| `General` / DM | Normal | Dle preference (default: jen ntfy) |

Správce může ručně povýšit vlákno na Urgent.

### ntfy integrace
Každý uživatel má ntfy topic: `clubmanager-{userId}`. Uživatel si v Profilu zadá vlastní topic nebo se použije vygenerovaný. POST na `http://192.168.60.221:8225/{topic}` (interní).

### NotificationDispatcher
```
Background hosted service, Channel<NotificationJob> queue
Pro každou novou zprávu:
  → načte účastníky vlákna
  → pro každého: zkontroluj NotificationPreference
  → pokud priority >= minPriority: pošli email + ntfy
  → loguj výsledek
```

---

## Onboarding / Registrace

- Email + telefonní číslo povinné pro všechny uživatele
- Dva způsoby vstupu do oddílu:
  1. **Pozvánka emailem** — Správce zadá email + roli → systém pošle email s tokenem → `/accept-invite?token=xxx`
  2. **Kód skupiny** — uživatel jde na `/join?code=ABC123` → přiřazen do oddílu s výchozí rolí Parent
- Pozvánka expiruje po 7 dnech
- JoinCode lze regenerovat (staré kódy přestanou fungovat)

---

## MVP scope

### MVP (implementovat jako první)
1. Auth — registrace, login, pozvánka emailem, kód skupiny
2. Organizations + Clubs — vytvoření, správa členů, role
3. Messaging — vlákna v oddílu, send/receive, real-time přes SignalR
4. Notifikace — email (SMTP) pro Urgent/High

### V2 (po MVP)
- Přímé zprávy (DM)
- Managed child (rodič vidí/píše za dítě)
- NotificationPreference UI (per-user nastavení)
- ntfy integrace (po emailu)
- Modul Rezervace aut

---

## Otevřené otázky (vyřešeny)

- Typ aplikace: monolit ✅
- Tenant model: Tenant = Organizace ✅
- Messaging direction: obousměrně (C) ✅
- Struktura zpráv: vlákna + DM ✅
- Notifikace: priority-based ✅
- Dítě jako uživatel: obojí možné, Správce rozhoduje ✅
- Onboarding: email + telefon povinné, pozvánka nebo kód ✅
