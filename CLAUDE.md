# Cities Skylines — Claude Code MCP Advisor

## Project Overview
AI companion for Cities: Skylines that connects Claude Code to the running game via MCP (Model Context Protocol). First-of-its-kind — no other MCP server exists for Cities Skylines.

## Architecture
- **C# Mod** (`mod/`) — runs inside Unity, HTTP server on `localhost:7828`
- **Python MCP Server** (`mcp_server/`) — FastMCP, stdio transport, registered as `cities-skylines` in `~/.claude.json`
- **Save Parser** (`mcp_server/save_parser.py`) — reads .crp binary files (CRAP format)

## Key Commands

### Build the mod
```bash
cd mod/ && bash build.sh
```
Compiles with `mcs` (Mono), installs DLL to `~/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/ClaudeAdvisor/`

### Test connection (game must be running with save loaded)
```bash
curl http://localhost:7828/api/v1/health
python3 scripts/test_connection.py
```

### Run MCP server manually
```bash
python3 -m mcp_server.server
```

## Game Paths (Mac)
- **Saves:** `~/Library/Application Support/Colossal Order/Cities_Skylines/Saves/`
- **Mods:** `~/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/`
- **Game DLLs:** `~/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed/`
- **Logs:** `~/Library/Logs/Colossal Order/Cities_Skylines/`

## C# Mod Conventions
- Target: .NET 3.5 (Mono runtime in Unity)
- No Newtonsoft/JSON.NET — use `JsonHelper.cs` (manual StringBuilder)
- No Harmony — only vanilla ICities extension points + direct Assembly-CSharp.dll access
- Write operations MUST use `SimulationManager.instance.AddAction()` for thread safety
- Read operations are thread-safe on Singleton managers

## MCP Server Conventions
- Python 3.10+, FastMCP decorators
- All game tools go through `game_client.py` → HTTP to localhost:7828
- Save tools use `save_parser.py` directly (no game needed)
- Tool docstrings are shown to Claude Code users — keep them clear and actionable

## HTTP API (mod ↔ MCP server)
- `GET /api/v1/health` — health check
- `GET /api/v1/stats` — full city stats
- `GET /api/v1/buildings?type=X&flags=X&limit=N` — building list
- `GET /api/v1/traffic` — traffic data
- `GET /api/v1/transport` — transport lines
- `GET /api/v1/districts` — districts
- `GET /api/v1/budget` — economy
- `POST /api/v1/actions/demolish` — `{"buildingId": N}`
- `POST /api/v1/actions/demolish-abandoned` — mass demolish
- `POST /api/v1/actions/money` — `{"amount": N}`
- `POST /api/v1/actions/tax` — `{"service": "X", "rate": N}`
- `POST /api/v1/actions/budget` — `{"service": "X", "budget": N}`
- `POST /api/v1/actions/speed` — `{"speed": 1-3}`
- `POST /api/v1/actions/pause` — `{"paused": true/false}`

## Repo
- **GitHub:** https://github.com/BernardUriza/cities-skylines--cc-mcp-advisor
- **Branch:** master
- **Owner:** Bernard Uriza (bernarduriza on Steam)
