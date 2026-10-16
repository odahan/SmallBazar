"""Check ELF load segments and APK offsets for Android 16 KiB native library loading."""

import argparse
import json
import struct
import zipfile
from pathlib import Path


def check_apk(path):
    failures = []
    libraries = []
    with zipfile.ZipFile(path) as archive, path.open("rb") as apk:
        for entry in archive.infolist():
            if not entry.filename.startswith("lib/") or not entry.filename.endswith(".so"):
                continue
            data = archive.read(entry)
            if data[:4] != b"\x7fELF":
                failures.append(f"{entry.filename}: missing ELF header")
                continue
            if data[4] != 2:
                continue
            endian = "<" if data[5] == 1 else ">"
            offset = struct.unpack_from(endian + "Q", data, 32)[0]
            entry_size, count = struct.unpack_from(endian + "HH", data, 54)
            alignments = []
            for index in range(count):
                segment = struct.unpack_from(endian + "IIQQQQQQ", data, offset + index * entry_size)
                segment_type, _, file_offset, address, _, _, _, alignment = segment
                if segment_type == 1:
                    alignments.append(alignment)
                    if alignment < 16384 or (file_offset - address) % 16384:
                        failures.append(f"{entry.filename}: load segment not aligned to 16 KiB")
            if not alignments:
                failures.append(f"{entry.filename}: no load segments")
            apk.seek(entry.header_offset + 26)
            name_length, extra_length = struct.unpack("<HH", apk.read(4))
            data_offset = entry.header_offset + 30 + name_length + extra_length
            if entry.compress_type == zipfile.ZIP_STORED and data_offset % 16384:
                failures.append(f"{entry.filename}: uncompressed APK entry not aligned to 16 KiB")
            libraries.append({"name": entry.filename, "segment_alignments": sorted(set(alignments)),
                              "compressed": entry.compress_type != zipfile.ZIP_STORED,
                              "apk_data_offset": data_offset})
    if not libraries:
        failures.append("No 64-bit native libraries found")
    return {"apk": str(path), "checked_64_bit_libraries": len(libraries),
            "sqlite": [library for library in libraries if library["name"].endswith("/libe_sqlite3.so")],
            "failures": failures}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    report = check_apk(parser.parse_args().apk)
    print(json.dumps(report, indent=2))
    raise SystemExit(bool(report["failures"]))
