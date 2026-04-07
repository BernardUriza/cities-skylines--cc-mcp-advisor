#!/bin/bash
# Build script for ClaudeAdvisor MCP mod
# Compiles all C# sources into a single DLL

MANAGED="$HOME/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed"
MODS="$HOME/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/ClaudeAdvisor"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=== ClaudeAdvisor MCP Build ==="
echo "Source: $SCRIPT_DIR"
echo "Target: $MODS"

# Check compiler
if ! command -v mcs &> /dev/null; then
    echo "ERROR: mcs (Mono C# compiler) not found. Install with: brew install mono"
    exit 1
fi

# Check game DLLs
if [ ! -f "$MANAGED/ICities.dll" ]; then
    echo "ERROR: Game DLLs not found at $MANAGED"
    exit 1
fi

# Create mod directory
mkdir -p "$MODS/Source"

# Compile
echo "Compiling..."
mcs -target:library \
    -out:"$MODS/ClaudeAdvisor.dll" \
    -r:"$MANAGED/ICities.dll" \
    -r:"$MANAGED/Assembly-CSharp.dll" \
    -r:"$MANAGED/ColossalManaged.dll" \
    -r:"$MANAGED/UnityEngine.dll" \
    -noconfig -nostdlib \
    -r:"$MANAGED/mscorlib.dll" \
    -r:"$MANAGED/System.dll" \
    -r:"$MANAGED/System.Core.dll" \
    "$SCRIPT_DIR/ClaudeAdvisorMod.cs" \
    "$SCRIPT_DIR/HttpCommandServer.cs" \
    "$SCRIPT_DIR/GameActionExecutor.cs" \
    "$SCRIPT_DIR/CityDataCollector.cs" \
    "$SCRIPT_DIR/JsonHelper.cs"

if [ $? -eq 0 ]; then
    echo "BUILD SUCCESS: $MODS/ClaudeAdvisor.dll"
    ls -lh "$MODS/ClaudeAdvisor.dll"
    # Copy sources for the game's source loader
    cp "$SCRIPT_DIR"/*.cs "$MODS/Source/" 2>/dev/null
    echo "Sources copied to $MODS/Source/"
    echo ""
    echo "Restart Cities Skylines and enable 'Claude City Advisor MCP' in Content Manager > Mods"
else
    echo "BUILD FAILED"
    exit 1
fi
