"""Read-only session reminder for the two local clients."""
import json
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
note = "Read AGENTS.md and HANDOFF.md before editing. "
if (root / "HANDOFF.md").is_file():
    note += "Resume the active task only if it matches the user's request."
else:
    note += "The project handoff is missing."
if sys.argv[1] == "codex":
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": note}}))
else:
    print(json.dumps({"injectSteps": [{"ephemeralMessage": note}]}))
