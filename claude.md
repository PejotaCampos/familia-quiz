# CLAUDE.md — FamiliaQuiz

## What this is

FamiliaQuiz ("The You Quiz") is a real-time party quiz for one family: the developer lives in
the Netherlands, the rest of the family is in Brazil. The goal is not to make a commercial
game but to bring the family closer and help everyone learn more about each other.

The developer is an experienced .NET developer using this project to learn Blazor
(and later Unity as an alternative client). Explain Blazor-specific concepts briefly
when introducing them; no need to explain general C#/.NET.

## Game rules

- One player is **"on top of the stairs"**: the **King**. Every question in the round is about them.
  (Use "King" in code and docs; the pt-BR UI can use "Rei"/"Rainha" or the player's name.)
- The King picks a question from the question bank **or writes a brand-new one**.
  They can also edit the options of an existing question before using it.
- The King **secretly chooses the correct answer, live**, right before the others guess.
  Correct answers are never stored ahead of time as the source of truth (people's answers change).
- All other players guess on their own device (multiple choice).
- Reveal: show the correct answer, who got it right, and update stair positions.
- **The stairs are the score track**: everyone else starts at the bottom; each correct guess
  moves a player up one step. The first player to reach the top becomes the next King.
- **Number of steps is configurable per room, between 5 and 10** (default 5), set when
  creating the room.
- **Tie-break:** if several players reach the top on the same question, the current King
  chooses which of them becomes the next King.
- The King earns nothing during their reign (no points for stumping others). Keep scoring
  logic simple so this can change later.
- The King can always **skip** a question (nobody should be put on the spot).

### Open design decisions (ask before assuming)
- When does the whole game end (a fixed number of reigns, a time limit, or only when the host
  ends it)? Default for v1: the host ends it.
- When a new King is crowned, does everyone (including the old King) restart at the bottom,
  or keep their positions? Default for v1: everyone restarts at the bottom.

## Players and UX constraints

- Players range from kids to grandparents, **mainly in a browser on a PC or laptop**
  (Chrome/Edge most likely). Phones are a secondary target: the layout must still work on a
  phone, but desktop is the primary design.
- **UI language: Brazilian Portuguese (pt-BR).** Code, comments and commits in English.
- Players are usually on a video call (WhatsApp, Meet, Discord) at the same time, often in
  another window. **The game must work well in a half-screen window** (about 700px wide)
  as well as full screen. No in-game voice/chat needed.
- Readable, large fonts and clear buttons for older relatives; mouse-first, with
  **keyboard shortcuts** for answering (keys 1–4 for options, Enter to confirm).
- Typing is easy on a keyboard, so writing new questions and options is a normal part of
  play, not an edge case.
- The larger screen can show the full stairs with every player's position at all times.
- Join a room via a short room code or a link shared in WhatsApp. No accounts, no install.
- PWA support is optional (desktop browsers can install it, but it is not a priority).
- Connections can still drop (Wi-Fi, laptop sleep, browsers throttling background tabs while
  the player is in the call window). Reconnecting must be seamless: the client stores a
  player ID (localStorage) and rejoins the same room as the same player, receiving the
  current room state. Do not rely on client-side timers staying accurate in background tabs;
  the server owns any timing.
- Time zone gap NL–BR is 4–5 hours; live games mostly happen on weekends.

## Architecture

```
Blazor WebAssembly (browsers)  ──SignalR──►  ASP.NET Core server (game hub)  ──►  question storage
```

- **Client: Blazor WebAssembly standalone** (not Blazor Server, not Interactive Server).
  Reasons: it can be hosted as free static files; UI events stay local instead of
  round-tripping to a European server from Brazil; and the game API stays in an explicit
  SignalR hub that a Unity client can reuse later.
- **Server: ASP.NET Core with a SignalR hub.** The server is **authoritative**: it owns rooms,
  turns, the secret answer, scoring and stair positions. Clients only send intents
  (join, choose question, submit answer, guess, skip) and render state pushed by the server.
- **Never send the correct answer to guessing clients before the reveal.**
- **The SignalR hub is the game's public API.** A Unity client will be added later and must be
  able to use the same hub, so keep the hub contract UI-agnostic: no Blazor types, no HTML,
  no UI strings in hub messages. Send data; let each client localize and present it.
- **Shared project** holds DTOs, hub method names and enums used by both client and server.
  Target **netstandard2.1** so it can later be referenced from Unity. Keep it free of
  ASP.NET/Blazor dependencies.

### Solution layout

```
FamiliaQuiz.sln
src/
  FamiliaQuiz.Client/   Blazor WebAssembly
  FamiliaQuiz.Server/   ASP.NET Core + SignalR hub
  FamiliaQuiz.Shared/   DTOs, contracts (netstandard2.1)
tests/                  (add when logic exists: xUnit tests for game rules)
```

Game rules (turn flow, scoring, stairs, rotation) should live in plain, unit-testable C#
classes, not inside the hub. The hub is a thin layer that maps connections to players and
forwards intents to the game logic.

## Hosting (must stay free)

- **Client:** static hosting (GitHub Pages or Cloudflare Pages). Mind the base path if
  hosted under a sub-path on GitHub Pages.
- **Server:** Docker container on **Render free tier**. Constraints:
  - Spins down after ~15 min idle; cold start takes about a minute. Acceptable.
  - **Ephemeral filesystem**: no local SQLite or files that must survive restarts.
  - Keep rooms **in memory**. Losing active rooms on restart is acceptable.
- **Question bank:** start with a JSON file in the repo (seed questions in pt-BR).
  Later, move to a free hosted Postgres (Neon or Supabase). Avoid Render's free Postgres
  (expires after 90 days).
- CORS: the server must allow the client's static-site origin.
- Configure SignalR automatic reconnect on the client.

## Version 1 scope (keep it tiny)

1. Create a room (choosing 5–10 stair steps) / join with a room code and a display name.
2. Lobby showing connected players; host starts the game.
3. Choose the first King.
4. The King picks a question (from seed JSON), edits it or writes a new one, then secretly picks the answer.
5. Others guess; reveal; move correct guessers up the stairs.
6. First to the top becomes the next King (on a tie, the King chooses). Repeat.
7. Reconnect support (player ID in localStorage).

### Later (not v1)
- Persisting questions created during games, and an archive of past answers ("family memory").
- Fibbage-style mode: guessers write fake answers, mixed with the real one.
- Async mode for weekdays (the King answers ahead of time; others guess anytime).
- Photos, avatars, sounds, animations on the stairs.
- "Crazy rules" (random modifiers per round), designed as pluggable rule classes.
- Unity client using the same hub.

## Conventions

- Use the .NET SDK installed on the machine (prefer the current LTS). Nullable reference
  types enabled. Keep dependencies minimal.
- Small, focused commits with clear English messages.
- Run `dotnet build` (and `dotnet test` once tests exist) before committing.
- When a change touches the hub contract, update both client and server in the same change.

## Git / repository notes

- This is a **personal** project on a work laptop. The repo lives in
  `C:\Users\pjced\source\repos\personal\FamiliaQuiz` and uses a personal Git identity via
  `includeIf` (noreply email `9043321+PejotaCampos@users.noreply.github.com`).
- The remote uses an SSH host alias: `git@github-personal:PejotaCampos/familia-quiz.git`.
  Do not change the remote URL or the Git identity.
- Never add anything from the developer's employer (code, packages, configs, secrets).