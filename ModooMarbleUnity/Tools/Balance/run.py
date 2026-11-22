"""Compile the game rules and generate a local balance report."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

GAME = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--games", type=int, default=200)
    parser.add_argument("--seed", type=int, default=4200)
    parser.add_argument("--replay", type=int, nargs=4, metavar=("MAP", "NOISE", "ROTATION", "FORCED"))
    parser.add_argument("--unity", type=Path, default=Path(os.environ.get("ASURA_UNITY_EDITOR", "C:/Unity Editer/6000.3.9f1/Editor")))
    parser.add_argument("--out", type=Path, default=GAME / "Validation/Balance")
    args = parser.parse_args()
    if not 1 <= args.games <= 10_000 or not 0 <= args.seed <= 2_000_000_000:
        parser.error("Expected 1–10000 games and a seed between 0 and 2000000000")
    if args.replay and not all(value in allowed for value, allowed in zip(
        args.replay, (range(2), range(3), range(20), range(-2, 5))
    )):
        parser.error("Replay expects map 0–1, noise 0–2, rotation 0–19, forced card -2–4")
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    editor = args.unity.resolve()
    runtime = editor / "Data/NetCoreRuntime"
    csc = editor / "Data/DotNetSdkRoslyn/csc.dll"
    versions = sorted((runtime / "shared/Microsoft.NETCore.App").glob("6.*"))
    if not csc.is_file() or not versions:
        parser.error("Set --unity to the Unity 6000.3.9f1 Editor folder")
    refs = ["/reference:" + str(p) for p in sorted(versions[-1].glob("*.dll"))
            if p.name.startswith(("System.", "Microsoft.", "netstandard")) and ".Native." not in p.name]
    exe = out / "BalanceRunner.dll"
    sources = sorted((GAME / "Assets/Marble/Core").glob("*.cs"))
    dotnet = runtime / "dotnet.exe"
    subprocess.run([dotnet, csc, "/nologo", "/target:exe", "/langversion:latest", "/out:" + str(exe),
                    *refs, *sources, TOOLS / "BalanceRunner.cs"], check=True)
    shutil.copyfile(GAME / "Validation/loop-source-tests.runtimeconfig.json", out / "BalanceRunner.runtimeconfig.json")
    matches = out / "matches.jsonl"
    temporary = out / "matches.jsonl.tmp"
    subprocess.run([dotnet, exe, temporary, str(args.games), str(args.seed), *map(str, args.replay or [])], check=True)
    games = [json.loads(line) for line in temporary.read_text(encoding="utf-8").splitlines()]
    expected = 1 if args.replay else args.games + args.games // 10 * 6
    if len(games) != expected:
        raise ValueError("Unexpected match count")
    temporary.replace(matches)
    payload = json.dumps(games, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
    template = (TOOLS / "dashboard.html").read_text(encoding="utf-8")
    dashboard = out / "dashboard.html"
    dashboard.write_text(template.replace("/*__MATCHES__*/[]", payload), encoding="utf-8")
    print(f"Generated {len(games)} matches")
    print(dashboard)


if __name__ == "__main__":
    main()
