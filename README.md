# FamiliaQuiz

Real-time family party quiz: one player is the King 👑 on top of the stairs, everyone else
guesses answers about them and climbs one step per correct guess. See `claude.md` for the rules.

## Run locally

Two terminals from the repo root:

```powershell
dotnet run --project src/FamiliaQuiz.Server --launch-profile http   # http://localhost:5075
dotnet run --project src/FamiliaQuiz.Client --launch-profile http   # http://localhost:5264
```

Open http://localhost:5264. To play with a second player on the same machine, use
http://127.0.0.1:5264 in another tab (different origin = separate localStorage = separate player).

Tests: `dotnet test`.

## Projects

| Project | What |
|---|---|
| `src/FamiliaQuiz.Shared` | Hub contract: method names, DTOs, enums (netstandard2.1, Unity-friendly) |
| `src/FamiliaQuiz.Server` | ASP.NET Core + SignalR hub. `Game/GameRoom.cs` holds all game rules |
| `src/FamiliaQuiz.Client` | Blazor WebAssembly standalone UI (pt-BR) |
| `tests/FamiliaQuiz.Server.Tests` | xUnit tests for the game rules |

## Deploy (free)

- **Server (Render, Docker):** Dockerfile path `src/FamiliaQuiz.Server/Dockerfile`, context = repo root.
  Set `Cors__AllowedOrigins__0` to the client's origin (e.g. `https://pejotacampos.github.io`).
- **Client (GitHub/Cloudflare Pages):** `dotnet publish src/FamiliaQuiz.Client -c Release`, host
  `bin/Release/net10.0/publish/wwwroot`. Put the server URL in `wwwroot/appsettings.json` (`ServerUrl`).
  On GitHub Pages under a sub-path, change `<base href>` in `wwwroot/index.html`, and add an empty
  `.nojekyll` file so `_framework` is served.
