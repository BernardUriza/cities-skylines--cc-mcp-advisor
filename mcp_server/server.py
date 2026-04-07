"""
Cities Skylines MCP Server — AI Companion for Claude Code

Connects Claude Code to a running Cities Skylines game via the ClaudeAdvisor mod's
HTTP API on localhost:7828. Also provides offline save file tools.
"""

import os
from mcp.server.fastmcp import FastMCP
from mcp_server.game_client import GameClient
from mcp_server import save_parser

mcp = FastMCP(
    "cities-skylines",
    instructions=(
        "You are an AI companion for Cities: Skylines. "
        "Use these tools to read city stats, execute game actions (demolish, money, taxes), "
        "and manage save files. Always check is_game_running first before using game tools."
    ),
)

client = GameClient(os.environ.get("GAME_HTTP_URL", "http://localhost:7828"))


# ─── Health ───────────────────────────────────────────────

@mcp.tool()
async def is_game_running() -> dict:
    """Check if Cities Skylines is running with the ClaudeAdvisor mod active.
    Always call this before using other game tools."""
    connected = await client.is_connected()
    return {"connected": connected, "url": client.base_url}


# ─── Read Tools ───────────────────────────────────────────

@mcp.tool()
async def get_city_stats() -> dict:
    """Get comprehensive city statistics: population, money, services,
    buildings, traffic, transport. This is the main diagnostic tool."""
    return await client.get("/api/v1/stats")


@mcp.tool()
async def get_buildings(
    type: str = "",
    flags: str = "",
    limit: int = 50,
) -> dict:
    """List buildings with optional filters.

    Args:
        type: Filter by service type (residential, commercial, industrial, office)
        flags: Filter by flag (abandoned, burned)
        limit: Max results (default 50)
    """
    params = {}
    if type:
        params["type"] = type
    if flags:
        params["flags"] = flags
    params["limit"] = str(limit)
    return await client.get("/api/v1/buildings", params=params)


@mcp.tool()
async def get_traffic() -> dict:
    """Get traffic data: road segments, average density, flow %, congested roads."""
    return await client.get("/api/v1/traffic")


@mcp.tool()
async def get_transport() -> dict:
    """Get transport lines: bus, metro, train, tram counts."""
    return await client.get("/api/v1/transport")


@mcp.tool()
async def get_districts() -> dict:
    """Get all districts with population and happiness."""
    return await client.get("/api/v1/districts")


@mcp.tool()
async def get_budget() -> dict:
    """Get budget info: money, formatted amount, weekly profit."""
    return await client.get("/api/v1/budget")


# ─── Write Tools ──────────────────────────────────────────

@mcp.tool()
async def demolish_building(building_id: int) -> dict:
    """Demolish a single building by its ID.
    Use get_buildings first to find building IDs."""
    return await client.post("/api/v1/actions/demolish", {"buildingId": building_id})


@mcp.tool()
async def demolish_all_abandoned() -> dict:
    """Demolish ALL abandoned buildings in the city at once.
    No more clicking 329 buildings one by one."""
    return await client.post("/api/v1/actions/demolish-abandoned", {})


@mcp.tool()
async def set_money(amount: int) -> dict:
    """Add or remove money from the city treasury.

    Args:
        amount: Positive to add money, negative to remove. In dollars (e.g. 50000 = $50,000).
    """
    return await client.post("/api/v1/actions/money", {"amount": amount})


@mcp.tool()
async def set_tax_rate(service: str, rate: int) -> dict:
    """Change tax rate for a service type.

    Args:
        service: residential, commercial, industrial, office
        rate: Tax rate 0-29 (default is usually 9)
    """
    return await client.post("/api/v1/actions/tax", {"service": service, "rate": rate})


@mcp.tool()
async def set_budget(service: str, budget: int) -> dict:
    """Change budget for a city service.

    Args:
        service: healthcare, fire, police, education, road, electricity, water, garbage, parks
        budget: Budget percentage 50-150 (100 is default)
    """
    return await client.post("/api/v1/actions/budget", {"service": service, "budget": budget})


@mcp.tool()
async def pause_game(paused: bool = True) -> dict:
    """Pause or unpause the game simulation.

    Args:
        paused: True to pause, False to unpause
    """
    return await client.post("/api/v1/actions/pause", {"paused": paused})


@mcp.tool()
async def set_game_speed(speed: int) -> dict:
    """Change game simulation speed.

    Args:
        speed: 1 (normal), 2 (fast), 3 (fastest)
    """
    return await client.post("/api/v1/actions/speed", {"speed": speed})


# ─── Visual Tools ─────────────────────────────────────────

@mcp.tool()
async def take_screenshot() -> dict:
    """Take a screenshot of the current game view. Returns the file path
    to the PNG image. Use this to visually inspect the city layout,
    check building placement, traffic patterns, or anything visual."""
    result = await client.get("/api/v1/screenshot")
    if result.get("success") and result.get("data", {}).get("path"):
        return {
            "success": True,
            "message": "Screenshot captured. Read the image file to see the city.",
            "path": result["data"]["path"],
            "size_kb": result["data"].get("size_kb", 0),
        }
    return result


# ─── Save File Tools ──────────────────────────────────────

@mcp.tool()
async def list_saves() -> list[dict]:
    """List all Cities Skylines save files with metadata (name, size, date)."""
    return save_parser.list_saves()


@mcp.tool()
async def read_save_metadata(filename: str) -> dict:
    """Read the header metadata of a .crp save file (city name, version, assets).

    Args:
        filename: The .crp filename (e.g. 'claudecode.crp')
    """
    saves_dir = save_parser.SAVES_DIR
    path = os.path.join(saves_dir, filename)
    if not os.path.exists(path):
        return {"error": f"Save not found: {filename}"}
    return save_parser.read_crp_header(path)


@mcp.tool()
async def backup_save(filename: str) -> dict:
    """Create a timestamped backup of a save file before modifying it.

    Args:
        filename: The .crp filename to backup (e.g. 'claudecode.crp')
    """
    return save_parser.backup_save(filename)


# ─── Entry point ──────────────────────────────────────────

if __name__ == "__main__":
    mcp.run(transport="stdio")
