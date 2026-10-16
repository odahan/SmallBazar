"""Verify reference backups and create new working copies without modifying the sources."""

import argparse
import hashlib
import json
import sqlite3
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8-sig"))
    args.output_directory.mkdir(parents=True, exist_ok=False)
    results = []

    for entry in manifest["databases"]:
        source = Path(entry["backup"]).resolve(strict=True)
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if digest != entry["sha256"]:
            raise ValueError(f"Checksum mismatch: {entry['label']}")

        label = entry["label"]
        if label in (".", "..") or Path(label).name != label:
            raise ValueError("The backup label must be a single directory name.")
        destination = args.output_directory.resolve() / label / source.name
        destination.parent.mkdir()

        with sqlite3.connect(source.as_uri() + "?mode=ro", uri=True) as database:
            if database.execute("PRAGMA integrity_check").fetchall() != [("ok",)]:
                raise ValueError(f"Integrity check failed: {label}")
            counts = {}
            for table, expected in entry["row_counts"].items():
                quoted_table = table.replace('"', '""')
                counts[table] = database.execute(
                    f'SELECT COUNT(*) FROM "{quoted_table}"'
                ).fetchone()[0]
                if counts[table] != expected:
                    raise ValueError(f"Unexpected row count: {label}/{table}")
            with sqlite3.connect(destination) as copy:
                database.backup(copy)
                if copy.execute("PRAGMA integrity_check").fetchall() != [("ok",)]:
                    raise ValueError(f"Working copy integrity check failed: {label}")

        results.append({
            "label": label,
            "sha256": digest,
            "integrity": "ok",
            "counts": counts,
            "working_copy": str(destination),
        })

    (args.output_directory / "verification.json").write_text(
        json.dumps(results, indent=2), encoding="utf-8"
    )
    print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
