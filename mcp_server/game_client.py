import logging
import time
import uuid
from typing import Optional

import httpx

DEFAULT_URL = "http://localhost:7828"
TIMEOUT = 10.0

log = logging.getLogger("cities.client")


class GameConnectionError(Exception):
    """Game is not running or mod is not loaded."""


class GameTimeoutError(Exception):
    """Game took too long to respond (might be loading or frozen)."""


class GameAPIError(Exception):
    """Game responded with an error status."""

    def __init__(self, status_code: int, message: str, correlation_id: str = ""):
        self.status_code = status_code
        self.correlation_id = correlation_id
        super().__init__(f"HTTP {status_code}: {message} [cid:{correlation_id}]")


class GameClient:
    def __init__(self, base_url: str = DEFAULT_URL):
        self.base_url = base_url

    def _new_cid(self) -> str:
        return uuid.uuid4().hex[:8]

    async def get(self, path: str, params: Optional[dict] = None) -> dict:
        cid = self._new_cid()
        url = f"{self.base_url}{path}"
        log.info("GET %s started  [cid:%s]", path, cid)
        start = time.monotonic()

        try:
            async with httpx.AsyncClient(timeout=TIMEOUT) as client:
                resp = await client.get(
                    url,
                    params=params,
                    headers={"X-Correlation-ID": cid},
                )
            elapsed_ms = int((time.monotonic() - start) * 1000)

            if resp.status_code >= 400:
                body = resp.text[:200]
                log.error(
                    "GET %s failed  [cid:%s] status=%d duration=%dms body=%s",
                    path, cid, resp.status_code, elapsed_ms, body,
                )
                raise GameAPIError(resp.status_code, body, cid)

            log.info(
                "GET %s done    [cid:%s] status=%d duration=%dms",
                path, cid, resp.status_code, elapsed_ms,
            )
            return resp.json()

        except httpx.ConnectError as e:
            elapsed_ms = int((time.monotonic() - start) * 1000)
            log.error(
                "GET %s connection_failed [cid:%s] duration=%dms error=%s",
                path, cid, elapsed_ms, e,
            )
            raise GameConnectionError(
                f"Cannot connect to game at {self.base_url}. "
                "Is Cities Skylines running with the ClaudeAdvisor mod enabled?"
            ) from e

        except httpx.TimeoutException as e:
            log.error(
                "GET %s timeout [cid:%s] timeout=%.1fs error=%s",
                path, cid, TIMEOUT, e,
            )
            raise GameTimeoutError(
                f"Game did not respond within {TIMEOUT}s. "
                "It might be loading, paused at a menu, or frozen."
            ) from e

    async def post(self, path: str, data: dict) -> dict:
        cid = self._new_cid()
        url = f"{self.base_url}{path}"
        log.info("POST %s started [cid:%s] body=%s", path, cid, data)
        start = time.monotonic()

        try:
            async with httpx.AsyncClient(timeout=TIMEOUT) as client:
                resp = await client.post(
                    url,
                    json=data,
                    headers={"X-Correlation-ID": cid},
                )
            elapsed_ms = int((time.monotonic() - start) * 1000)

            if resp.status_code >= 400:
                body = resp.text[:200]
                log.error(
                    "POST %s failed  [cid:%s] status=%d duration=%dms body=%s",
                    path, cid, resp.status_code, elapsed_ms, body,
                )
                raise GameAPIError(resp.status_code, body, cid)

            log.info(
                "POST %s done   [cid:%s] status=%d duration=%dms",
                path, cid, resp.status_code, elapsed_ms,
            )
            return resp.json()

        except httpx.ConnectError as e:
            elapsed_ms = int((time.monotonic() - start) * 1000)
            log.error(
                "POST %s connection_failed [cid:%s] duration=%dms error=%s",
                path, cid, elapsed_ms, e,
            )
            raise GameConnectionError(
                f"Cannot connect to game at {self.base_url}. "
                "Is Cities Skylines running with the ClaudeAdvisor mod enabled?"
            ) from e

        except httpx.TimeoutException as e:
            log.error(
                "POST %s timeout [cid:%s] timeout=%.1fs error=%s",
                path, cid, TIMEOUT, e,
            )
            raise GameTimeoutError(
                f"Game did not respond within {TIMEOUT}s. "
                "It might be loading, paused at a menu, or frozen."
            ) from e

    async def is_connected(self) -> bool:
        try:
            result = await self.get("/api/v1/health")
            return result.get("status") == "ok"
        except Exception as e:
            log.debug("Health check failed: %s", e)
            return False
