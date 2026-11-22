"""Validate the shipped Steam config and the achievement registration reference."""
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Assets/Marble/Core/Achievements.cs"
FIELDS = ("apiName", "displayName", "description", "reward")
ENTRY = re.compile(r'new\s+AchievementDefinition\s*\(\s*' + r'\s*,\s*'.join([r'"([^"]+)"'] * 4) + r'\s*\)')


def unique_index(rows, origin):
    index = {}
    for row in rows:
        if re.fullmatch(r"WC_[A-Z0-9_]+", row[0]) is None or any(not value.strip() for value in row):
            raise ValueError(f"{origin}: invalid achievement {row[0]!r}")
        if row[0] in index:
            raise ValueError(f"{origin}: duplicate achievement {row[0]}")
        index[row[0]] = row[1:]
    if not index:
        raise ValueError(f"{origin}: empty catalog")
    return index


def main():
    steam = json.loads((ROOT / "Assets/StreamingAssets/steam-config.json").read_text(encoding="utf-8-sig"))
    app_id = steam["appId"]
    development = steam["developmentAppId"]
    if type(app_id) is not int or not 0 < app_id <= 0xFFFFFFFF or type(development) is not bool:
        raise ValueError("Invalid Steam configuration")
    if app_id == 480 and not development:
        raise ValueError("Development App ID 480 is invalid for a release")

    reference = json.loads((ROOT / "Documentation/steam-achievements.json").read_text(encoding="utf-8-sig"))
    code = CATALOG.read_text(encoding="utf-8-sig")
    declared = unique_index(ENTRY.findall(code), CATALOG.name)
    documented = unique_index([tuple(item[field] for field in FIELDS) for item in reference["achievements"]], "Steam reference")
    missing = sorted(set(re.findall(r'"(WC_[A-Z0-9_]+)"', code)) - declared.keys())
    if missing:
        raise ValueError("Unknown achievement references: " + ", ".join(missing))
    differing = sorted(key for key in declared.keys() | documented.keys() if declared.get(key) != documented.get(key))
    if differing:
        raise ValueError("Code and Steam reference differ: " + ", ".join(differing))
    print(f"Steam configuration and {len(declared)} achievement definitions verified")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        sys.exit(str(error))
