#!/usr/bin/env sh
# Publishes and packs the Linux release files on Linux. Usage: tools/publish.sh [rid ...]  (default: linux-arm64 linux-x64)
# TMPUBLISHER_OUT=<folder> puts the packages and SHA256SUMS.txt elsewhere (default artifacts/release), TMPUBLISHER_WORK=<folder> the
# published program (default artifacts/publish).
#
# Only from a clean working tree (or a `git archive` of a commit, with TMPUBLISHER_COMMIT set to it): the program carries the commit it
# was built from. One program file per platform: self-contained, single file, trimmed, the native libraries inside it; no native compiler
# is needed, so both architectures are built on one machine (tools/publish.ps1 does the same on Windows).
set -eu
root=$(cd "$(dirname "$0")/.." && pwd)
out=${TMPUBLISHER_OUT:-$root/artifacts/release}
work=${TMPUBLISHER_WORK:-$root/artifacts/publish}
if git -C "$root" rev-parse --git-dir >/dev/null 2>&1; then
  if [ -n "$(git -C "$root" status --porcelain)" ]; then
    echo "The working tree is not clean; commit first (release files must match a commit)." >&2
    exit 1
  fi
  commit=$(git -C "$root" rev-parse HEAD)
else
  commit=${TMPUBLISHER_COMMIT:?not a git working tree: set TMPUBLISHER_COMMIT to the commit this archive was made from}
fi
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -n 1)
echo "TM Publisher $version from commit $commit"
[ "$#" -gt 0 ] || set -- linux-arm64 linux-x64
for rid in "$@"; do
  program="$work/$rid/program"
  rm -rf "$program"
  dotnet publish "$root/src/TMPublisher.Desktop/TMPublisher.Desktop.csproj" -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false \
    -p:SourceRevisionId="$commit" -m:1 -nodeReuse:false -o "$program"
  python3 "$root/tools/package.py" "$program" "$rid" "$version" "$out"
done
