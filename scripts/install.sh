#!/bin/bash
# Full install: build mod + install dependencies + configure MCP server
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "=== Cities Skylines MCP Advisor — Full Install ==="
echo ""

# 1. Build the mod
echo "--- Step 1: Building C# mod ---"
bash "$PROJECT_DIR/mod/build.sh"
if [ $? -ne 0 ]; then
    echo "FAILED: Mod build failed"
    exit 1
fi
echo ""

# 2. Install Python deps
echo "--- Step 2: Installing Python dependencies ---"
pip install httpx mcp 2>&1 | tail -5
echo ""

# 3. Configure MCP server in Claude Code
echo "--- Step 3: Configuring MCP server ---"
claude mcp add cities-skylines -- python3 -m mcp_server.server --cwd "$PROJECT_DIR" 2>/dev/null
if [ $? -eq 0 ]; then
    echo "MCP server registered as 'cities-skylines'"
else
    echo "NOTE: claude mcp add failed (may need manual config)"
    echo "Run manually: claude mcp add cities-skylines -- python3 -m mcp_server.server"
fi
echo ""

echo "=== INSTALL COMPLETE ==="
echo ""
echo "Next steps:"
echo "1. Restart Cities Skylines"
echo "2. Enable 'Claude City Advisor MCP' in Content Manager > Mods"
echo "3. Load a save"
echo "4. In Claude Code, use the cities-skylines tools"
