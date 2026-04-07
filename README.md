# Cities Skylines — Claude Code MCP Advisor

AI companion for Cities: Skylines that connects [Claude Code](https://claude.ai/code) directly to your running game via [MCP](https://modelcontextprotocol.io/) (Model Context Protocol). Claude can read your city stats, demolish buildings, inject money, change taxes, and manage saves — all from the terminal.

## Architecture

```
Claude Code  <--stdio/MCP-->  Python MCP Server  <--HTTP-->  C# Game Mod (port 7828)
                                    |                              |
                               Save Parser                   Game Managers
                              (.crp files)                  (BuildingManager,
                                                            EconomyManager, etc.)
```

## MCP Tools Available

### Read Tools
| Tool | Description |
|------|-------------|
| `is_game_running` | Check if the mod is active |
| `get_city_stats` | Full city report (population, money, services, traffic) |
| `get_buildings` | List buildings with filters (type, abandoned, burned) |
| `get_traffic` | Road segments, congestion, flow % |
| `get_transport` | Bus, metro, train, tram lines |
| `get_districts` | Districts with population and happiness |
| `get_budget` | Money, weekly profit |

### Write Tools
| Tool | Description |
|------|-------------|
| `demolish_building` | Demolish a building by ID |
| `demolish_all_abandoned` | Mass-demolish all abandoned buildings |
| `set_money` | Add/remove money |
| `set_tax_rate` | Change tax rate (0-29%) for any service |
| `set_budget` | Change service budget (50-150%) |
| `pause_game` | Pause/unpause simulation |
| `set_game_speed` | Set speed (1-3) |

### Save File Tools
| Tool | Description |
|------|-------------|
| `list_saves` | List all .crp save files |
| `read_save_metadata` | Parse save file header |
| `backup_save` | Create timestamped backup |

## Quick Install

```bash
git clone https://github.com/BernardUriza/cities-skylines--cc-mcp-advisor.git
cd cities-skylines--cc-mcp-advisor

# Build and install the mod
bash mod/build.sh

# Install Python deps
pip install httpx mcp

# Register MCP server in Claude Code
claude mcp add cities-skylines -- python3 -m mcp_server.server
```

Then:
1. Restart Cities: Skylines
2. Enable **"Claude City Advisor MCP"** in Content Manager > Mods
3. Load a save
4. In Claude Code, ask: *"check my city"* or *"demolish all abandoned buildings"*

## Manual Build (Mac)

```bash
cd mod/
bash build.sh
```

This compiles all C# sources and installs the DLL to the game's mod directory.

## Test Connection

With the game running and a save loaded:

```bash
# Direct HTTP test
curl http://localhost:7828/api/v1/health

# Full connection test
python3 scripts/test_connection.py
```

## Project Structure

```
mod/                          # C# game mod (runs inside Unity)
  ClaudeAdvisorMod.cs         # Entry point (IUserMod)
  HttpCommandServer.cs        # HTTP server on port 7828
  GameActionExecutor.cs       # Write operations (demolish, money, tax)
  CityDataCollector.cs        # Read operations (stats, buildings, traffic)
  JsonHelper.cs               # JSON utilities
  build.sh                    # Compile script

mcp_server/                   # Python MCP server (stdio transport)
  server.py                   # FastMCP tool definitions
  game_client.py              # HTTP client for the mod
  save_parser.py              # .crp binary file parser

scripts/
  install.sh                  # Full install script
  test_connection.py          # Connection verification
```

## How It Works

1. **C# Mod** runs inside Cities Skylines, starts an HTTP server on `localhost:7828`
2. **Python MCP Server** translates Claude Code tool calls into HTTP requests
3. **Read operations** access game Singleton managers directly (thread-safe for reads)
4. **Write operations** use `SimulationManager.AddAction()` to safely dispatch to the simulation thread

## Requirements

- Cities: Skylines (Steam)
- macOS / Linux (Windows support planned)
- Python 3.10+
- Mono (for compiling: `brew install mono`)
- Claude Code

## License

MIT
