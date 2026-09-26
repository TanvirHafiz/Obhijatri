"""
Looks for user-facing text written directly in code instead of the resource files, and for
em or en dashes anywhere in source, resources and docs. Run from the repository root:

    python tools/check_strings.py

Prints candidates and exits with code 1 if any are found. Lines that must keep literal text
(for example developer-only log messages) can end with the comment  // not-ui
"""
import glob
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
BANGLA = re.compile("[ঀ-৿]")
LITERAL = re.compile(r'(?<![@$])"((?:[^"\\]|\\.)*)"')
XAML_ATTR = re.compile(r'\b(Text|Content|Header|PlaceholderText|Title|ToolTipService\.ToolTip|Message|Label)="([^"{][^"]*)"')
DASHES = re.compile("[–—]")

# Literals that are code, not UI: SQL, connection strings, font files and family names,
# and filter-list syntax ("! Title:", "[Adblock Plus").
NOT_UI_LITERAL = re.compile(r"^\s*(SELECT|INSERT|UPDATE|DELETE|PRAGMA|CREATE|ALTER|Data Source)\b|\.ttf#|^Segoe UI|^! [A-Z]|^\[Adblock", re.IGNORECASE)
# Lines whose text is for developers only (exception messages are never shown to users).
NOT_UI_LINE = re.compile(r"Exception\(")


def source_files(pattern):
    for path in glob.glob(os.path.join(ROOT, "src", "**", pattern), recursive=True):
        parts = path.replace("\\", "/").split("/")
        if "bin" in parts or "obj" in parts:
            continue
        yield path


def main():
    hits = []

    for path in source_files("*.cs"):
        lines = open(path, encoding="utf-8").read().splitlines()
        # Files that are entirely "#if DEBUG" are developer tools (benchmark, self-tests), never shown to users.
        if lines and lines[0].strip() == "#if DEBUG":
            continue
        for number, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.endswith("// not-ui"):
                continue
            for literal in LITERAL.findall(line):
                if not BANGLA.search(literal) and (NOT_UI_LITERAL.search(literal) or NOT_UI_LINE.search(line)):
                    continue
                if BANGLA.search(literal) or (" " in literal and re.search("[A-Za-z]{3,}", literal)):
                    hits.append(f"{os.path.relpath(path, ROOT)}:{number}: \"{literal[:80]}\"")

    for path in source_files("*.xaml"):
        for number, line in enumerate(open(path, encoding="utf-8"), 1):
            for attribute, value in XAML_ATTR.findall(line):
                hits.append(f"{os.path.relpath(path, ROOT)}:{number}: {attribute}=\"{value}\"")

    dash_files = list(source_files("*.cs")) + list(source_files("*.xaml")) + list(source_files("*.resw"))
    dash_files += [os.path.join(ROOT, name) for name in ("CLAUDE.md", "PROGRESS.md", "THIRD_PARTY_NOTICES.md")]
    for path in dash_files:
        if not os.path.exists(path):
            continue
        for number, line in enumerate(open(path, encoding="utf-8"), 1):
            if DASHES.search(line):
                hits.append(f"{os.path.relpath(path, ROOT)}:{number}: dash character")

    if hits:
        print("\n".join(hits))
        return 1
    print("No hardcoded user-facing strings or dashes found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
