"""Compare Unity build provenance against the current project and player files."""
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Builds/Windows/build-identity.json"


def fingerprint(directories, exclude=()):
    hashes = {}
    for directory in directories:
        location = ROOT / directory
        if not location.is_dir():
            raise ValueError(f"Missing directory: {directory}")
        for path in location.rglob("*"):
            if path.is_file() and path not in exclude:
                with path.open("rb") as source:
                    sha = hashlib.sha256()
                    for chunk in iter(lambda: source.read(1024 * 1024), b""):
                        sha.update(chunk)
                hashes[path.relative_to(ROOT).as_posix()] = sha.hexdigest()
    return hashes


def compare(label, listed, current):
    expected = {entry["path"]: entry["sha256"] for entry in listed}
    changed = sorted(path for path in expected.keys() | current.keys() if expected.get(path) != current.get(path))
    if len(expected) != len(listed) or not expected:
        raise ValueError(f"Invalid {label} fingerprint")
    if changed:
        raise ValueError(f"{label} mismatch ({len(changed)} files): " + ", ".join(changed[:8]))


def main():
    identity = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
    if identity["schema"] != 1:
        raise ValueError("Unknown build identity schema")
    compare("Build input", identity["inputs"], fingerprint(("Assets", "Packages", "ProjectSettings")))
    compare("Player output", identity["outputs"], fingerprint(("Builds/Windows",), exclude=(MANIFEST,)))
    print("Verified current source and Windows player:", identity["gameVersion"], identity["unityVersion"])


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError) as error:
        sys.exit(f"{error}\nRun Unity Build > Validate and build in this project.")
