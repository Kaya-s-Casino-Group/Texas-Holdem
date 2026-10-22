"""Read-only verification of the handoff's changed files, not a full backup check."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def digest_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    script = Path(__file__).resolve()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=script.parents[3],
                        help="Texas-Holdem root (defaults to this document's project)")
    args = parser.parse_args()
    project = args.root.resolve()
    manifest = json.loads(script.with_name("worktree-manifest.json").read_text(encoding="utf-8"))
    checked = 0
    problems = []
    for repo in manifest["repositories"]:
        root = project / repo["path"]
        for record in repo["files"]:
            path = root / record["path"]
            label = str(path.relative_to(project))
            if record["kind"] == "deleted":
                if path.exists() or path.is_symlink():
                    problems.append("EXPECTED ABSENT: " + label)
                checked += 1
                continue
            if not path.is_file():
                problems.append("MISSING: " + label)
                continue
            if path.stat().st_size != record["size"] or digest_file(path) != record["sha256"]:
                problems.append("CHANGED: " + label)
            checked += 1
        try:
            result = subprocess.run(["git", "-C", str(root), "rev-parse", "HEAD"],
                                    capture_output=True, text=True, check=False)
            if result.returncode != 0:
                print("NOTE: Git metadata unavailable for " + repo["path"])
            elif result.stdout.strip() != repo["head"]:
                print("NOTE: HEAD differs for " + repo["path"] + "; compare intentional commits.")
        except FileNotFoundError:
            print("NOTE: git unavailable; file verification is still performed.")
    for problem in problems:
        print(problem)
    print("Checked {} recorded files; {} missing/changed entries.".format(checked, len(problems)))
    print("Scope: changed/untracked and selected environment files; not every unchanged project asset.")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
