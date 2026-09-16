# ClubManager — archiv

Aplikace **zanikla 2026-09-15**. Všechno, co uměla, žije dál jako modul **Kluby** v ScorerAppu:

- https://github.com/olsanvit/ScorerApp — `src/ScorerApp.Web/Components/Pages/ClubModule`,
  `Domain/Models/Clubs`, `Domain/Services/Clubs`

| ClubManager | ScorerApp |
|---|---|
| `/organizations`, `/clubs`, `/members`, `/clubs/{id}/members` | `/organizations`, `/clubs` + detail organizace a oddílu |
| `/chat` (SignalR hub) | `/chat` (in-process `ClubChatBroadcaster`) |
| `/messages`, `/payments/debts` | `/circulars`, `/circulars/debts` |
| `/cars`, `/cars/reservations` | `/cars`, `/cars/reservations` |
| `/join`, `/accept-invite` | `/join`, `/accept-invite` (navíc zrušení a znovuodeslání pozvánky) |
| `ParentChildLink` / `FamilyLink` (účet ↔ účet, bez UI) | `FamilyLink` (rodič ↔ hráč soupisky) — rodič dostává oběžníky oddílu |

Kód je v historii repozitáře; poslední verze s aplikací je commit `682e8f7`.
Návrh a plán zůstávají v `docs/superpowers/`.

Data: produkční databáze `ClubManager` na QNAPu obsahovala jen seedovaný účet,
nic se nemigrovalo. Kontejner `clubmanager` a role `clubmanager_usr` se řeší zvlášť.
