#!/usr/bin/env python3
"""Test connection to the ClaudeAdvisor mod's HTTP server."""

import sys
import httpx

URL = "http://localhost:7828"

def main():
    print(f"Testing connection to ClaudeAdvisor at {URL}...")
    print()

    # Health check
    try:
        r = httpx.get(f"{URL}/api/v1/health", timeout=3)
        data = r.json()
        print(f"[OK] Health: {data}")
    except httpx.ConnectError:
        print("[FAIL] Cannot connect. Is Cities Skylines running with the mod enabled?")
        sys.exit(1)
    except Exception as e:
        print(f"[FAIL] {e}")
        sys.exit(1)

    # Stats
    try:
        r = httpx.get(f"{URL}/api/v1/stats", timeout=5)
        data = r.json()
        if data.get("success"):
            d = data["data"]
            print(f"[OK] City: {d.get('cityName', '?')}")
            print(f"     Population: {d.get('population', '?')}")
            print(f"     Money: {d.get('moneyFormatted', '?')}")
            print(f"     Happiness: {d.get('services', {}).get('happiness', '?')}")
        else:
            print(f"[WARN] Stats returned error: {data.get('error')}")
    except Exception as e:
        print(f"[WARN] Stats failed: {e}")

    # Buildings
    try:
        r = httpx.get(f"{URL}/api/v1/buildings", params={"flags": "abandoned", "limit": "5"}, timeout=5)
        data = r.json()
        if data.get("success"):
            print(f"[OK] Abandoned buildings: {data['data'].get('count', 0)}")
    except Exception as e:
        print(f"[WARN] Buildings query failed: {e}")

    print()
    print("Connection test complete!")

if __name__ == "__main__":
    main()
