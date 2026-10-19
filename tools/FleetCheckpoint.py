"""Explicit, hash-guarded fleet recovery. Rollback is check-only unless --apply.

Known files restore the saved NOW baseline; --path also captures extra existing
files (including ignored previews). --new-path declares a file absent at NOW.
Seal once after a stage is finished; later edits deliberately block its rollback.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timezone


def digest(path):
    if not path.exists():
        return None
    if not path.is_file():
        raise ValueError(f"Not a regular file: {path}")
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().lower()


def bounded(root, value):
    raw = Path(value)
    if ".." in raw.parts:
        raise ValueError(f"Parent traversal refused: {value}")
    path = raw if raw.is_absolute() else root / raw
    resolved = path.resolve()
    if not resolved.is_relative_to(root) or resolved == root:
        raise ValueError(f"Outside workspace: {value}")
    if resolved.relative_to(root).parts[0].lower() == ".git":
        raise ValueError("Git internals are not recovery targets")
    if path.absolute() != resolved:
        raise ValueError(f"Linked or redirected path refused: {value}")
    return resolved


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def source_path(root, baseline, value):
    path = bounded(root, value)
    if path.is_relative_to(baseline):
        raise ValueError("Recovery storage cannot be a recovery target")
    return path


def stage_path(root, baseline, name):
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*", name):
        raise ValueError("Stage must be a simple name, without path separators")
    return bounded(root, baseline / "stages" / name)


def manifest(root, baseline):
    result = {}
    for item in read_json(baseline / "source-manifest.json"):
        path = source_path(root, baseline, item["path"])
        key = path.relative_to(root).as_posix()
        if key in result:
            raise ValueError(f"Duplicate baseline entry: {key}")
        result[key] = item["sha256"].lower()
    return result


def checkpoint(args, root, baseline):
    saved = manifest(root, baseline)
    stage = stage_path(root, baseline, args.stage)
    if stage.exists():
        raise ValueError("Stage already exists; choose a new stage name")
    entries, seen = [], set()
    for value, is_new in [(p, False) for p in args.path] + [(p, True) for p in args.new_path]:
        target = source_path(root, baseline, value)
        key = target.relative_to(root).as_posix()
        if key in seen:
            raise ValueError(f"Duplicate target: {key}")
        seen.add(key)
        if is_new and key in saved:
            raise ValueError(f"File existed at NOW; cannot mark new: {key}")
        original = bounded(root, baseline / "source-before" / key) if key in saved else target
        before = None if is_new else digest(original)
        if not is_new and (before is None or (key in saved and before != saved[key])):
            raise ValueError(f"Missing/corrupt original; use --new-path only for new files: {key}")
        entries.append({"path": key, "before": before})
    if not entries:
        raise ValueError("Supply at least one --path or --new-path")
    stage.mkdir(parents=True)
    for entry in entries:
        if entry["before"] is not None:
            key = entry["path"]
            original = baseline / "source-before" / key if key in saved else root / key
            backup = bounded(root, stage / "before" / key)
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(original, backup)
            if digest(backup) != entry["before"]:
                raise ValueError(f"Original changed during checkpoint: {key}")
    write_json(stage / "checkpoint.json", {"workspace": str(root), "sealed": False, "files": entries})
    return {"checkpoint": str(stage), "files": len(entries), "sealed": False}


def load_stage(root, baseline, name):
    stage = stage_path(root, baseline, name)
    data = read_json(stage / "checkpoint.json")
    if data["workspace"] != str(root):
        raise ValueError("Stage belongs to a different workspace")
    seen = set()
    for entry in data["files"]:
        path = source_path(root, baseline, entry["path"])
        if path in seen:
            raise ValueError("Duplicate stage target")
        seen.add(path)
        if entry["before"] is not None and digest(bounded(root, stage / "before" / entry["path"])) != entry["before"]:
            raise ValueError(f"Corrupt recovery backup: {entry['path']}")
    return stage, data


def seal(args, root, baseline):
    stage, data = load_stage(root, baseline, args.stage)
    if (stage / "sealed.json").exists():
        raise ValueError("Stage already sealed; later edits require a new stage")
    for entry in data["files"]:
        entry["after"] = digest(source_path(root, baseline, entry["path"]))
    data["sealed"] = True
    write_json(stage / "sealed.json", data)
    return {"stage": args.stage, "sealed": True, "files": len(data["files"])}


def rollback(args, root, baseline):
    stage, original = load_stage(root, baseline, args.stage)
    data = read_json(stage / "sealed.json")
    if not data.get("sealed") or data.get("workspace") != str(root):
        raise ValueError("Stage is not sealed for this workspace")
    if [{k: e[k] for k in ("path", "before")} for e in data["files"]] != original["files"]:
        raise ValueError("Sealed allowlist differs from checkpoint")
    conflicts = [e["path"] for e in data["files"] if digest(source_path(root, baseline, e["path"])) != e["after"]]
    if conflicts:
        raise ValueError("Later edits block rollback: " + ", ".join(conflicts))
    if not args.apply:
        return {"stage": args.stage, "check": "PASS", "files": len(data["files"]), "applied": False}
    retained = stage / "retained" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")
    for entry in data["files"]:
        target = source_path(root, baseline, entry["path"])
        if entry["after"] is not None:
            candidate = bounded(root, retained / entry["path"])
            candidate.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, candidate)
            if digest(candidate) != entry["after"]:
                raise ValueError(f"File changed during retention: {entry['path']}")
    for entry in data["files"]:
        target = source_path(root, baseline, entry["path"])
        if digest(target) != entry["after"]:
            raise ValueError(f"Concurrent edit blocks remaining rollback: {entry['path']}")
        if entry["before"] is None:
            target.unlink(missing_ok=True)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            fd, temporary = tempfile.mkstemp(prefix=".fleet-restore-", dir=target.parent)
            os.close(fd)
            try:
                shutil.copy2(stage / "before" / entry["path"], temporary)
                if digest(Path(temporary)) != entry["before"]:
                    raise ValueError("Restore copy failed hash verification")
                os.replace(temporary, target)
            finally:
                Path(temporary).unlink(missing_ok=True)
    return {"stage": args.stage, "applied": True, "retained": str(retained), "files": len(data["files"])}


def audit(args, root, baseline):
    saved = manifest(root, baseline)
    current = subprocess.check_output(["git", "-C", str(root), "ls-files", "--cached", "--others", "--exclude-standard", "-z"]).decode("utf-8").split("\0")
    modified, missing = [], []
    for key, expected in saved.items():
        actual = digest(source_path(root, baseline, key))
        if actual is None:
            missing.append(key)
        elif actual != expected:
            modified.append(key)
    return {"modified": modified, "missing": missing, "new": sorted(set(current) - set(saved) - {""}), "new_scope": "Git-visible files; ignored files require explicit checkpoint --path"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("audit", "checkpoint", "seal", "rollback"))
    parser.add_argument("--workspace", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--stage")
    parser.add_argument("--path", action="append", default=[])
    parser.add_argument("--new-path", action="append", default=[])
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    try:
        root = args.workspace.resolve(strict=True)
        baseline = bounded(root, args.baseline)
        if args.command != "audit" and not args.stage:
            raise ValueError("--stage is required")
        if args.apply and args.command != "rollback":
            raise ValueError("--apply is only valid for rollback")
        print(json.dumps(globals()[args.command](args, root, baseline), indent=2))
    except (ValueError, OSError, KeyError, subprocess.SubprocessError) as error:
        print(f"REFUSED: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
