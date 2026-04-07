# Cities Skylines - Claude Code MCP Advisor

A Cities: Skylines mod that exports real-time city statistics to a JSON file, enabling [Claude Code](https://claude.ai/code) to act as a live city advisor through the terminal.

## What it does

Every 30 seconds, the mod exports a comprehensive JSON report of your city's stats to:
```
~/Library/Application Support/Colossal Order/Cities_Skylines/claude_city_report.json
```

Claude Code reads this file and gives you real-time advice on your city: economy, traffic, services, population, and more.

## Stats exported

- Population (by age group)
- Money & weekly profit/loss
- Demand (residential, commercial, workplace)
- Services (electricity, water, sewage, garbage, heating, crime, happiness, education, healthcare)
- Buildings (by type, abandoned, burned)
- Traffic (flow %, congested roads with density)
- Transport lines (bus, metro, train, tram)
- Auto-generated problems & advice

## Installation

### Option A: Pre-compiled DLL
1. Copy the `ClaudeAdvisor/` folder to:
   - **Mac**: `~/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/`
   - **Windows**: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\`
   - **Linux**: `~/.local/share/Colossal Order/Cities_Skylines/Addons/Mods/`
2. Enable "Claude City Advisor" in **Content Manager > Mods**
3. Load a save

### Option B: Compile from source (Mac)
```bash
MANAGED="$HOME/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed"
MODS="$HOME/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/ClaudeAdvisor"

mkdir -p "$MODS/Source"
cp ClaudeAdvisor.cs "$MODS/Source/"

mcs -target:library -out:"$MODS/ClaudeAdvisor.dll" \
    -r:"$MANAGED/ICities.dll" \
    -r:"$MANAGED/Assembly-CSharp.dll" \
    -r:"$MANAGED/ColossalManaged.dll" \
    -r:"$MANAGED/UnityEngine.dll" \
    ClaudeAdvisor.cs
```

## Usage with Claude Code

1. Start Cities: Skylines and load a city
2. In your terminal, run `claude`
3. Ask Claude to check your city:
   ```
   > check my city report
   > what's wrong with my city?
   > how do I fix the traffic?
   ```

Claude reads `claude_city_report.json` and gives you advice based on live data.

## Example output

```json
{
  "cityName": "Verville",
  "population": 14287,
  "money": 48883930,
  "moneyFormatted": "$488,839",
  "weeklyProfit": 518677,
  "services": {
    "happiness": 87,
    "crimeRate": 4,
    "electricityCapacity": 238256,
    "electricityConsumption": 208416
  },
  "abandonedBuildings": 38,
  "trafficFlowPercent": 89,
  "problems": ["High abandoned: 38"],
  "advice": ["Add metro lines!"]
}
```

## Requirements

- Cities: Skylines (Steam, any edition)
- Mono/.NET Framework 3.5 runtime (for compilation)

## How it works

The mod implements `IUserMod` and `LoadingExtensionBase` from the ICities API. On level load, it creates a Unity `MonoBehaviour` that runs `ExportStats()` every 30 seconds, reading from the game's internal managers:

- `DistrictManager` - population, services, happiness
- `EconomyManager` - money, profit
- `ZoneManager` - demand
- `BuildingManager` - building counts, abandoned/burned
- `VehicleManager` - vehicle counts
- `NetManager` - road segments, traffic density
- `TransportManager` - transport lines

## License

MIT
