"""Run from a game's root: python ../GameHarness/run.py doctor|status|check NAME."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["status", "doctor", "check"])
    parser.add_argument("name", nargs="?")
    parser.add_argument("--project", type=Path, default=Path.cwd())
    args = parser.parse_args()
    root = args.project.resolve()
    profile = json.loads((root / "harness.json").read_text(encoding="utf-8-sig"))
    if args.action == "status":
        print((root / "HANDOFF.md").read_text(encoding="utf-8-sig"))
        return 0
    if args.action == "doctor":
        missing = [item for item in profile["requiredFiles"] if not (root / item).is_file()]
        missing += [exe for exe in profile["tools"] if not shutil.which(exe)]
        print("Missing: " + ", ".join(missing) if missing else "Environment OK")
        print("Checks:", ", ".join(profile["checks"]))
        return int(bool(missing))
    if args.name not in profile["checks"]:
        parser.error("Select one of: " + ", ".join(profile["checks"]))
    spec = profile["checks"][args.name]
    now = datetime.now(timezone.utc)
    out = root / profile["reports"] / (now.strftime("%Y%m%dT%H%M%SZ") + "-" + args.name + "-" + uuid.uuid4().hex[:8])
    out.mkdir(parents=True)
    command = [arg.replace("{out}", str(out)) for arg in spec["command"]]
    if "/" in command[0] or "\\" in command[0]:
        command[0] = str((root / command[0]).resolve())
    report = dict(check=args.name, started=now.isoformat(), command=command, status="not_run")
    try:
        report["inputsSha256"] = {p: digest(root / p) for p in spec.get("identify", [])}
        with (out / "run.log").open("wb") as log:
            result = subprocess.run(command, cwd=root, stdout=log, stderr=subprocess.STDOUT,
                                    timeout=spec.get("timeout", 180))
        report["exitCode"] = result.returncode
        report["status"] = "failed"
        if result.returncode == 0:
            if "result" in spec:
                details = json.loads((out / spec["result"]).read_text(encoding="utf-8-sig"))
                if details.get("status") != "passed":
                    raise ValueError("Player did not report passed")
            report["status"] = "passed"
    except subprocess.TimeoutExpired as error:
        report.update(status="failed", error=str(error))
    except (OSError, ValueError) as error:
        report["error"] = str(error)
    (out / "report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(report["status"], out)
    if "error" in report:
        print(report["error"])
    return int(report["status"] != "passed")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError) as error:
        sys.exit(str(error))
