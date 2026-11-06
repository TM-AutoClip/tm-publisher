#!/usr/bin/env sh
# Publishes and packs the Linux release files on Linux (artifacts/release). Usage: tools/publish.sh [rid ...]  (default: linux-arm64 linux-x64)
# TMPUBLISHER_OUT=<folder> puts the packages and SHA256SUMS.txt elsewhere, TMPUBLISHER_WORK=<folder> the published programs.
#
# Only from a clean working tree (or a `git archive` of a commit, with TMPUBLISHER_COMMIT set to it): the programs carry the commit they
# were built from. The background scheduler is Native AOT for this machine's own architecture (clang, or gcc when clang is missing) and
# the "jit" build (trimmed, ReadyToRun, single file) for the other one: Native AOT for another CPU needs a cross toolchain.
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
case "$(uname -m)" in
  aarch64|arm64) host=linux-arm64 ;;
  x86_64|amd64) host=linux-x64 ;;
  *) host=none ;;
esac
linker=clang
command -v clang >/dev/null 2>&1 || linker=gcc
echo "TM Publisher $version from commit $commit (Native AOT for $host with $linker)"
[ "$#" -gt 0 ] || set -- linux-arm64 linux-x64
for rid in "$@"; do
  window="$work/$rid/window"
  scheduler="$work/$rid/scheduler"
  rm -rf "$window" "$scheduler"
  dotnet publish "$root/src/TMPublisher.Desktop/TMPublisher.Desktop.csproj" -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false \
    -p:SourceRevisionId="$commit" -m:1 -nodeReuse:false -o "$window"
  if [ "$rid" = "$host" ]; then
    kind="-p:CppCompilerAndLinker=$linker"
  else
    kind="-p:SchedulerBuild=jit"
  fi
  dotnet publish "$root/src/TMPublisher.Background/TMPublisher.Background.csproj" -c Release -r "$rid" \
    -p:DebugType=none -p:DebugSymbols=false -p:SourceRevisionId="$commit" "$kind" -m:1 -nodeReuse:false -o "$scheduler"
  python3 "$root/tools/package.py" "$window" "$scheduler" "$rid" "$version" "$out"
done
