"""
Cities Skylines MCP Server — AI Companion for Claude Code

Connects Claude Code to a running Cities Skylines game via the ClaudeAdvisor mod's
HTTP API on localhost:7828. Also provides offline save file tools.
"""

import logging
import os
import sys

from mcp.server.fastmcp import FastMCP
from mcp_server.game_client import (
    GameClient,
    GameConnectionError,
    GameTimeoutError,
    GameAPIError,
)
from mcp_server import save_parser

# ─── Logging Setup ────────────────────────────────────────
# Structured logging to stderr (MCP uses stdio for protocol, stderr for logs)

logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%H:%M:%S",
    stream=sys.stderr,
)
log = logging.getLogger("cities.mcp")


# ─── MCP Server ──────────────────────────────────────────

mcp = FastMCP(
    "cities-skylines",
    instructions=(
        "You are an AI companion for Cities: Skylines. "
        "Use these tools to read city stats, execute game actions (demolish, money, taxes), "
        "and manage save files. Always check is_game_running first before using game tools."
    ),
)

client = GameClient(os.environ.get("GAME_HTTP_URL", "http://localhost:7828"))


def _handle_game_error(tool_name: str, e: Exception) -> dict:
    """Classify game errors into user-friendly responses with tracing info."""
    if isinstance(e, GameConnectionError):
        log.warning("[%s] Game not connected: %s", tool_name, e)
        return {
            "error": "game_not_connected",
            "message": str(e),
            "suggestion": "Start Cities Skylines and load a save with the ClaudeAdvisor mod enabled.",
        }
    elif isinstance(e, GameTimeoutError):
        log.warning("[%s] Game timeout: %s", tool_name, e)
        return {
            "error": "game_timeout",
            "message": str(e),
            "suggestion": "The game might be loading or frozen. Try again in a moment.",
        }
    elif isinstance(e, GameAPIError):
        log.error("[%s] Game API error: %s", tool_name, e)
        return {
            "error": "game_api_error",
            "status_code": e.status_code,
            "message": str(e),
            "correlation_id": e.correlation_id,
        }
    else:
        log.error("[%s] Unexpected error: %s", tool_name, e, exc_info=True)
        return {
            "error": "unexpected_error",
            "type": type(e).__name__,
            "message": str(e),
        }


# ─── Health ───────────────────────────────────────────────

@mcp.tool()
async def is_game_running() -> dict:
    """Check if Cities Skylines is running with the ClaudeAdvisor mod active.
    Always call this before using other game tools."""
    log.info("Tool: is_game_running")
    connected = await client.is_connected()
    log.info("Game connected: %s", connected)
    return {"connected": connected, "url": client.base_url}


# ─── Read Tools ───────────────────────────────────────────

@mcp.tool()
async def get_city_stats() -> dict:
    """Get comprehensive city statistics: population, money, services,
    buildings, traffic, transport. This is the main diagnostic tool."""
    log.info("Tool: get_city_stats")
    try:
        return await client.get("/api/v1/stats")
    except Exception as e:
        return _handle_game_error("get_city_stats", e)


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
    log.info("Tool: get_buildings type=%s flags=%s limit=%d", type, flags, limit)
    try:
        params = {}
        if type:
            params["type"] = type
        if flags:
            params["flags"] = flags
        params["limit"] = str(limit)
        return await client.get("/api/v1/buildings", params=params)
    except Exception as e:
        return _handle_game_error("get_buildings", e)


@mcp.tool()
async def get_traffic() -> dict:
    """Get traffic data: road segments, average density, flow %, congested roads."""
    log.info("Tool: get_traffic")
    try:
        return await client.get("/api/v1/traffic")
    except Exception as e:
        return _handle_game_error("get_traffic", e)


@mcp.tool()
async def get_traffic_graph(
    limit: int = 10000,
    min_density: int = 0,
) -> dict:
    """Get the road network as a graph for analysis/ML: nodes (intersections with
    XYZ positions, connection count) and edges (road segments with traffic density,
    lane count, road type, length, directionality).

    Returns a complete graph structure suitable for GNN/PyTorch processing.
    Use min_density to filter out low-traffic segments and reduce payload size.

    Args:
        limit: Max road segments to return (default 10000)
        min_density: Only include segments with density >= this value (0-255, default 0 = all)
    """
    log.info("Tool: get_traffic_graph limit=%d min_density=%d", limit, min_density)
    try:
        params = {"limit": str(limit), "minDensity": str(min_density)}
        return await client.get("/api/v1/traffic/graph", params=params)
    except Exception as e:
        return _handle_game_error("get_traffic_graph", e)


@mcp.tool()
async def get_transport() -> dict:
    """Get transport lines: bus, metro, train, tram counts."""
    log.info("Tool: get_transport")
    try:
        return await client.get("/api/v1/transport")
    except Exception as e:
        return _handle_game_error("get_transport", e)


@mcp.tool()
async def get_districts() -> dict:
    """Get all districts with population and happiness."""
    log.info("Tool: get_districts")
    try:
        return await client.get("/api/v1/districts")
    except Exception as e:
        return _handle_game_error("get_districts", e)


@mcp.tool()
async def get_budget() -> dict:
    """Get detailed budget: money, weekly profit, plus income and expenses
    broken down by service (residential, commercial, police, fire, etc.).
    Shows exactly where money is coming from and going to."""
    log.info("Tool: get_budget")
    try:
        return await client.get("/api/v1/budget")
    except Exception as e:
        return _handle_game_error("get_budget", e)


@mcp.tool()
async def get_problems() -> dict:
    """Get all active problems in the city: buildings without electricity, water,
    sewage, on fire, crime, death, no workers, no customers, road disconnected, etc.
    Returns problem counts by type and a list of up to 50 affected buildings.
    Use this to proactively monitor city health."""
    log.info("Tool: get_problems")
    try:
        return await client.get("/api/v1/problems")
    except Exception as e:
        return _handle_game_error("get_problems", e)


@mcp.tool()
async def get_changes() -> dict:
    """Detect what changed since the last time this tool was called.
    Tracks: population, happiness, crime, money, demand, abandoned/burned counts,
    traffic density, and buildings with problems.
    First call captures a baseline. Subsequent calls return deltas.
    Use this to monitor the city over time and react to trends."""
    log.info("Tool: get_changes")
    try:
        return await client.get("/api/v1/changes")
    except Exception as e:
        return _handle_game_error("get_changes", e)


# ─── Write Tools ──────────────────────────────────────────

@mcp.tool()
async def demolish_building(building_id: int) -> dict:
    """Demolish a single building by its ID.
    Use get_buildings first to find building IDs."""
    log.info("Tool: demolish_building id=%d", building_id)
    try:
        return await client.post("/api/v1/actions/demolish", {"buildingId": building_id})
    except Exception as e:
        return _handle_game_error("demolish_building", e)


@mcp.tool()
async def demolish_all_abandoned() -> dict:
    """Demolish ALL abandoned buildings in the city at once.
    No more clicking 329 buildings one by one."""
    log.info("Tool: demolish_all_abandoned")
    try:
        return await client.post("/api/v1/actions/demolish-abandoned", {})
    except Exception as e:
        return _handle_game_error("demolish_all_abandoned", e)


@mcp.tool()
async def set_money(amount: int) -> dict:
    """Add or remove money from the city treasury.

    Args:
        amount: Positive to add money, negative to remove. In dollars (e.g. 50000 = $50,000).
    """
    log.info("Tool: set_money amount=%d", amount)
    try:
        return await client.post("/api/v1/actions/money", {"amount": amount})
    except Exception as e:
        return _handle_game_error("set_money", e)


@mcp.tool()
async def set_tax_rate(service: str, rate: int) -> dict:
    """Change tax rate for a service type.

    Args:
        service: residential, commercial, industrial, office
        rate: Tax rate 0-29 (default is usually 9)
    """
    log.info("Tool: set_tax_rate service=%s rate=%d", service, rate)
    try:
        return await client.post("/api/v1/actions/tax", {"service": service, "rate": rate})
    except Exception as e:
        return _handle_game_error("set_tax_rate", e)


@mcp.tool()
async def set_budget(service: str, budget: int) -> dict:
    """Change budget for a city service.

    Args:
        service: healthcare, fire, police, education, road, electricity, water, garbage, parks
        budget: Budget percentage 50-150 (100 is default)
    """
    log.info("Tool: set_budget service=%s budget=%d", service, budget)
    try:
        return await client.post("/api/v1/actions/budget", {"service": service, "budget": budget})
    except Exception as e:
        return _handle_game_error("set_budget", e)


@mcp.tool()
async def pause_game(paused: bool = True) -> dict:
    """Pause or unpause the game simulation.

    Args:
        paused: True to pause, False to unpause
    """
    log.info("Tool: pause_game paused=%s", paused)
    try:
        return await client.post("/api/v1/actions/pause", {"paused": paused})
    except Exception as e:
        return _handle_game_error("pause_game", e)


@mcp.tool()
async def set_game_speed(speed: int) -> dict:
    """Change game simulation speed.

    Args:
        speed: 1 (normal), 2 (fast), 3 (fastest)
    """
    log.info("Tool: set_game_speed speed=%d", speed)
    try:
        return await client.post("/api/v1/actions/speed", {"speed": speed})
    except Exception as e:
        return _handle_game_error("set_game_speed", e)


@mcp.tool()
async def send_chirp(message: str) -> dict:
    """Send a message to the in-game Chirper feed (the Twitter-like notification system).
    The message appears as from 'Claude Advisor' directly inside the game.
    Use this to communicate advice, warnings, or observations to the player in-game.

    Args:
        message: The text to display in the Chirper (keep it short, like a tweet)
    """
    log.info("Tool: send_chirp length=%d", len(message))
    try:
        return await client.post("/api/v1/actions/chirp", {"message": message})
    except Exception as e:
        return _handle_game_error("send_chirp", e)


# ─── Visual Tools ─────────────────────────────────────────

@mcp.tool()
async def take_screenshot() -> dict:
    """Take a screenshot of the current game view. Returns the file path
    to the PNG image. Use this to visually inspect the city layout,
    check building placement, traffic patterns, or anything visual."""
    log.info("Tool: take_screenshot")
    try:
        result = await client.get("/api/v1/screenshot")
        if result.get("success") and result.get("data", {}).get("path"):
            return {
                "success": True,
                "message": "Screenshot captured. Read the image file to see the city.",
                "path": result["data"]["path"],
                "size_kb": result["data"].get("size_kb", 0),
            }
        return result
    except Exception as e:
        return _handle_game_error("take_screenshot", e)


# ─── Save File Tools ──────────────────────────────────────

@mcp.tool()
async def list_saves() -> list[dict]:
    """List all Cities Skylines save files with metadata (name, size, date)."""
    log.info("Tool: list_saves")
    try:
        return save_parser.list_saves()
    except Exception as e:
        log.error("list_saves failed: %s", e, exc_info=True)
        return [{"error": str(e)}]


@mcp.tool()
async def read_save_metadata(filename: str) -> dict:
    """Read the header metadata of a .crp save file (city name, version, assets).

    Args:
        filename: The .crp filename (e.g. 'claudecode.crp')
    """
    log.info("Tool: read_save_metadata file=%s", filename)
    saves_dir = save_parser.SAVES_DIR
    path = os.path.join(saves_dir, filename)
    if not os.path.exists(path):
        log.warning("Save not found: %s", path)
        return {"error": f"Save not found: {filename}"}
    try:
        return save_parser.read_crp_header(path)
    except Exception as e:
        log.error("read_save_metadata failed: %s", e, exc_info=True)
        return {"error": f"Failed to read save: {e}"}


@mcp.tool()
async def backup_save(filename: str) -> dict:
    """Create a timestamped backup of a save file before modifying it.

    Args:
        filename: The .crp filename to backup (e.g. 'claudecode.crp')
    """
    log.info("Tool: backup_save file=%s", filename)
    try:
        return save_parser.backup_save(filename)
    except Exception as e:
        log.error("backup_save failed: %s", e, exc_info=True)
        return {"error": f"Backup failed: {e}"}


# ─── Entry point ──────────────────────────────────────────

if __name__ == "__main__":
    log.info("Starting Cities Skylines MCP server (stdio transport)")
    mcp.run(transport="stdio")
