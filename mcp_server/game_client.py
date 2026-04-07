import httpx
from typing import Optional

DEFAULT_URL = "http://localhost:7828"
TIMEOUT = 5.0


class GameClient:
    def __init__(self, base_url: str = DEFAULT_URL):
        self.base_url = base_url

    async def get(self, path: str, params: Optional[dict] = None) -> dict:
        async with httpx.AsyncClient(timeout=TIMEOUT) as client:
            resp = await client.get(f"{self.base_url}{path}", params=params)
            resp.raise_for_status()
            return resp.json()

    async def post(self, path: str, data: dict) -> dict:
        async with httpx.AsyncClient(timeout=TIMEOUT) as client:
            resp = await client.post(f"{self.base_url}{path}", json=data)
            resp.raise_for_status()
            return resp.json()

    async def is_connected(self) -> bool:
        try:
            result = await self.get("/api/v1/health")
            return result.get("status") == "ok"
        except Exception:
            return False
