"""Check the Git index, including forced additions and partially staged files."""
import subprocess
import sys


def git(*args, data=None):
    return subprocess.run(["git", *args], input=data, stdout=subprocess.PIPE)


def main():
    files = git("ls-files", "-z")
    if files.returncode:
        return files.returncode
    ignored = git("check-ignore", "--no-index", "--stdin", "-z", data=files.stdout)
    if ignored.returncode not in (0, 1):
        return ignored.returncode
    # Use filenames only so diagnostics do not reproduce private material.
    markers = r"MarbleResearchArchive|RgnInfo[0-9]+\.dat|netmarble\.(net|kr)|모두의 *마블|mcp__rea|splus-items|ADD_ALL|모마"
    matches = git("grep", "--cached", "-IlizE", markers, "--", ".",
                  ":(exclude)tools/check_public_tree.py", ":(exclude).gitignore")
    if matches.returncode not in (0, 1):
        return matches.returncode
    paths = set((ignored.stdout + matches.stdout).split(b"\0")) - {b""}
    for name in sorted(paths):
        print("Review before publishing:", name.decode("utf-8", errors="replace"))
    return int(bool(paths))


if __name__ == "__main__":
    sys.exit(main())
