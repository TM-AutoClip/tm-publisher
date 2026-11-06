"""Packs the one published program file of a platform for a release and records the SHA-256.

    python tools/package.py <published folder> <rid> <version> [<output folder>]   (default artifacts/release)

win-x64:  <output>/TMPublisher-<version>-win-x64.zip with one folder "TM Publisher": TMPublisher.exe (the whole program: the window, the
          run the system scheduler starts, the notifications; the .NET runtime and every native library are inside it), README, LICENSE, the
          third-party notices and the font licenses. No installer: unzip and run TMPublisher.exe.
linux-*:  <output>/tm-publisher-<version>-<rid>.tar.gz with a tm-publisher/ folder: tm-publisher (marked executable) and the same documents.

Only documents are next to the program: a published folder with any other file than the program is refused (a native library left outside
the single file would be missing after an update that copies only the program).
The file's line in <output>/SHA256SUMS.txt is replaced (other lines are kept).
"""
import hashlib
import os
import sys
import tarfile
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCUMENTS = ("README.md", "README.tr.md", "LICENSE", "THIRD-PARTY-NOTICES.md")
LICENSES = os.path.join(ROOT, "src", "TMPublisher.Desktop", "Assets", "Licenses")


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def contents(published, windows):
    """(source, name in the package, mode) of every file."""
    program = "TMPublisher.exe" if windows else "TMPublisher"
    extra = sorted(n for n in os.listdir(published) if n != program and not n.endswith(".pdb") and not n.endswith(".dbg"))
    if extra:
        raise SystemExit("not a single file: the published folder also has " + ", ".join(extra))
    files = [(os.path.join(published, program), "TMPublisher.exe" if windows else "tm-publisher", 0o755)]
    files += [(os.path.join(ROOT, name), name, 0o644) for name in DOCUMENTS]
    files += [(os.path.join(LICENSES, name), "licenses/" + name, 0o644) for name in sorted(os.listdir(LICENSES))]
    for source, _, _ in files:
        if not os.path.isfile(source):
            raise SystemExit("missing: " + source)
    return files


def record(target):
    sums = os.path.join(os.path.dirname(target), "SHA256SUMS.txt")
    name = os.path.basename(target)
    lines = []
    if os.path.exists(sums):
        with open(sums, encoding="utf-8") as f:
            lines = [line.rstrip("\n") for line in f if line.strip() and not line.rstrip("\n").endswith("  " + name)]
    lines.append(f"{sha256(target)}  {name}")
    with open(sums, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


def main():
    published, rid, version = sys.argv[1], sys.argv[2], sys.argv[3]
    out = os.path.abspath(sys.argv[4]) if len(sys.argv) > 4 else os.path.join(ROOT, "artifacts", "release")
    os.makedirs(out, exist_ok=True)
    windows = rid.startswith("win")
    files = contents(published, windows)
    if windows:
        target = os.path.join(out, f"TMPublisher-{version}-{rid}.zip")
        with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for source, name, _ in files:
                archive.write(source, "TM Publisher/" + name)
    else:
        target = os.path.join(out, f"tm-publisher-{version}-{rid}.tar.gz")
        with tarfile.open(target, "w:gz") as archive:
            for source, name, mode in files:
                info = archive.gettarinfo(source, arcname="tm-publisher/" + name)
                info.mode, info.uid, info.gid, info.uname, info.gname = mode, 0, 0, "", ""
                with open(source, "rb") as f:
                    archive.addfile(info, f)
    record(target)
    print(f"{sha256(target)}  {os.path.basename(target)}  ({os.path.getsize(target) / (1024 * 1024):.1f} MiB)")


if __name__ == "__main__":
    main()
