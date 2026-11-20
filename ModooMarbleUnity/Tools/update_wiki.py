"""Publish the game's maintained pages to the shared local HTML wiki."""
from pathlib import Path
import shutil
import subprocess
import sys

game = Path(__file__).resolve().parents[1]
wiki = game.parent / "Game-Analysis-Wiki-OpenSource"
workspace_wiki = game.parent.parent / "Game-Analysis-Wiki-OpenSource"
if not wiki.is_dir() and workspace_wiki.is_dir():
    wiki = workspace_wiki
pages = wiki / "offline/content/pages"
for topic in ("start", "maintenance", "history", "handoff"):
    source = game / ("HANDOFF.md" if topic == "handoff" else f"Documentation/wiki/{topic}.md")
    shutil.copyfile(source, pages / f"asura-{topic}.md")

for script in ("build-offline-wiki.py", "verify-offline-wiki.py"):
    subprocess.run([sys.executable, "-X", "utf8", str(wiki / "scripts" / script)], cwd=wiki, check=True)
