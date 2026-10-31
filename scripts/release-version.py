"""Derive personal release labels from the single authoritative native version."""
from pathlib import Path
import argparse
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

def parts(value):
    if not re.fullmatch(r"[1-9][0-9]*\.[0-9]\.0", value):
        raise ValueError("Expected x.y.0: x >= 1, one-digit y, patch 0")
    major, minor, patch = map(int, value.split("."))
    if major > 255:
        raise ValueError("Release exceeds the installer version limit")
    return major, minor, patch

def label(value):
    major, minor, _ = parts(value)
    return f"{major}.{minor}"

def next_version(value):
    major, minor, _ = parts(value)
    result = f"{major + 1}.0.0" if minor == 9 else f"{major}.{minor + 1}.0"
    parts(result)
    return result

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--next", action="store_true", help="Print the next native version; do not edit files")
    parser.add_argument("--native", action="store_true", help="Print the checked native version")
    args = parser.parse_args()
    value = ET.parse(ROOT / "Directory.Build.props").findtext(".//VersionPrefix")
    parts(value)
    print(next_version(value) if args.next else value if args.native else label(value))
