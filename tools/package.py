"""Packs the published window program and background scheduler of one platform for a release and records the SHA-256.

    python tools/package.py <window folder> <scheduler folder> <rid> <version> [<output folder>]   (default artifacts/release)

win-x64:  artifacts/release/TMPublisher-<version>-win-x64.zip with one folder "TM Publisher": TMPublisher.exe (the window),
          TMPublisherScheduler.exe (the background scheduler), sqlite3mc.dll (the scheduler's SQLite library; the window carries its own
          copy inside), README, LICENSE, the third-party notices and the font licenses. No installer: unzip and run TMPublisher.exe.
linux-*:  artifacts/release/tm-publisher-<version>-<rid>.tar.gz with a tm-publisher/ folder: tm-publisher (the window),
          tm-publisher-scheduler, libsqlite3mc.so (both marked executable where they are programs), the same documents.

The file's line in artifacts/release/SHA256SUMS.txt is replaced (other lines are kept).
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


def contents(window, scheduler, windows):
    """(source, name in the package, mode) of every file."""
    if windows:
        files = [(os.path.join(window, "TMPublisher.exe"), "TMPublisher.exe", 0o755),
                 (os.path.join(scheduler, "TMPublisherScheduler.exe"), "TMPublisherScheduler.exe", 0o755),
                 (os.path.join(scheduler, "sqlite3mc.dll"), "sqlite3mc.dll", 0o644)]
    else:
        files = [(os.path.join(window, "TMPublisher"), "tm-publisher", 0o755),
                 (os.path.join(scheduler, "TMPublisherScheduler"), "tm-publisher-scheduler", 0o755),
                 (os.path.join(scheduler, "libsqlite3mc.so"), "libsqlite3mc.so", 0o755)]
    files += [(os.path.join(ROOT, name), name, 0o644) for name in DOCUMENTS]
    files += [(os.path.join(LICENSES, name), "licenses/" + name, 0o644) for name in sorted(os.listdir(LICENSES))]
    for source, _, _ in files:
        if not os.path.isfile(source):
            raise SystemExit("missing in the published output: " + source)
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
    window, scheduler, rid, version = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
    out = os.path.abspath(sys.argv[5]) if len(sys.argv) > 5 else os.path.join(ROOT, "artifacts", "release")
    os.makedirs(out, exist_ok=True)
    windows = rid.startswith("win")
    files = contents(window, scheduler, windows)
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
