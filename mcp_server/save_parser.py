import os
import struct
import shutil
from datetime import datetime
from pathlib import Path
from typing import Optional

SAVES_DIR = os.path.expanduser(
    "~/Library/Application Support/Colossal Order/Cities_Skylines/Saves"
)


def list_saves(saves_dir: str = SAVES_DIR) -> list[dict]:
    results = []
    if not os.path.isdir(saves_dir):
        return results
    for f in sorted(os.listdir(saves_dir)):
        if not f.endswith(".crp"):
            continue
        path = os.path.join(saves_dir, f)
        stat = os.stat(path)
        meta = read_crp_header(path)
        results.append({
            "filename": f,
            "path": path,
            "size_mb": round(stat.st_size / (1024 * 1024), 2),
            "modified": datetime.fromtimestamp(stat.st_mtime).isoformat(),
            "city_name": meta.get("city_name", "Unknown"),
        })
    return results


def read_crp_header(path: str) -> dict:
    result = {}
    try:
        with open(path, "rb") as f:
            magic = f.read(4)
            if magic != b"CRAP":
                result["error"] = f"Not a CRP file (magic: {magic})"
                return result
            result["magic"] = "CRAP"

            # Version (2 bytes, little-endian)
            version = struct.unpack("<H", f.read(2))[0]
            result["version"] = version

            # City name (length-prefixed string, 7-bit encoded length)
            name_len = _read_7bit_int(f)
            if name_len > 0 and name_len < 500:
                city_name = f.read(name_len).decode("utf-8", errors="replace")
                result["city_name"] = city_name

            # Asset count
            asset_count = struct.unpack("<I", f.read(4))[0]
            result["asset_count"] = asset_count

            # Read asset entries
            assets = []
            for _ in range(min(asset_count, 20)):
                try:
                    a_name_len = _read_7bit_int(f)
                    if a_name_len <= 0 or a_name_len > 500:
                        break
                    a_name = f.read(a_name_len).decode("utf-8", errors="replace")
                    # checksum (16 bytes)
                    checksum = f.read(16).hex()
                    # offset and size
                    offset = struct.unpack("<Q", f.read(8))[0]
                    size = struct.unpack("<Q", f.read(8))[0]
                    assets.append({
                        "name": a_name,
                        "offset": offset,
                        "size": size,
                        "size_mb": round(size / (1024 * 1024), 2),
                    })
                except Exception:
                    break
            result["assets"] = assets

    except Exception as e:
        result["error"] = str(e)
    return result


def backup_save(filename: str, saves_dir: str = SAVES_DIR) -> dict:
    src = os.path.join(saves_dir, filename)
    if not os.path.exists(src):
        return {"error": f"Save not found: {filename}"}

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_name = f"{Path(filename).stem}_backup_{timestamp}.crp"
    dst = os.path.join(saves_dir, backup_name)
    shutil.copy2(src, dst)
    return {
        "original": filename,
        "backup": backup_name,
        "path": dst,
        "size_mb": round(os.path.getsize(dst) / (1024 * 1024), 2),
    }


def _read_7bit_int(f) -> int:
    result = 0
    shift = 0
    while True:
        byte = f.read(1)
        if not byte:
            return 0
        b = byte[0]
        result |= (b & 0x7F) << shift
        if (b & 0x80) == 0:
            break
        shift += 7
    return result
