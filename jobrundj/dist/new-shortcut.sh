#!/usr/bin/env bash
# Creates a desktop launcher that runs a jobrundj job with the live dashboard.
#   macOS: a .command file (double click opens Terminal)
#   Linux: a .desktop file (applications menu and desktop)
# The launcher starts "jobrundj -j <Job> --ui --pause [job args]": the window stays open at the end
# until a key is pressed, so the summary can be read.
# Keep this script in the jobrundj folder (next to jobrundj.dll).

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: bash new-shortcut.sh -j JobName [-n "Name"] [-i icon] [-a "job args"] [-o folder] [--no-ui] [--interactive]

  -j, --job      job name (file name in the jobs folder, without .cs)        (required)
  -n, --name     launcher name shown under the icon (default: the job name)
  -i, --icon     icon image: .png on Linux; .png/.icns on macOS
  -a, --args     arguments for the job, e.g. "-p /data -a 30"
  -o, --output   destination folder
                 (default: macOS ~/Desktop; Linux applications menu + desktop)
      --no-ui    classic colored log instead of the dashboard
      --interactive
                 for jobs that ask for input: runs with -i, the missing values are asked
                 in the terminal (classic colored log instead of the dashboard)

Example:
  bash new-shortcut.sh -j JobExportCsv -n "Export customers" -i ~/icons/export.png
EOF
}

die() { echo "Error: $*" >&2; exit 1; }

JOB="" NAME="" ICON="" JOB_ARGS="" OUTPUT="" UI="--ui"
while [ $# -gt 0 ]; do
  case "$1" in
    -j|--job)    JOB="${2:-}"; shift 2 ;;
    -n|--name)   NAME="${2:-}"; shift 2 ;;
    -i|--icon)   ICON="${2:-}"; shift 2 ;;
    -a|--args)   JOB_ARGS="${2:-}"; shift 2 ;;
    -o|--output) OUTPUT="${2:-}"; shift 2 ;;
    --no-ui)     UI=""; shift ;;
    --interactive) UI="-i"; shift ;;
    -h|--help)   usage; exit 0 ;;
    *)           usage >&2; die "unknown option $1" ;;
  esac
done
[ -n "$JOB" ] || { usage >&2; die "the job name is required (-j)"; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JOB="$(basename "$JOB" .cs)"
[ -n "$NAME" ] || NAME="$JOB"
[ -f "$ROOT/jobs/$JOB.cs" ] || echo "Warning: jobs/$JOB.cs not found: the launcher is created anyway, check the job name." >&2

if [ -n "$ICON" ]; then
  [ -f "$ICON" ] || die "icon $ICON not found"
  ICON="$(cd "$(dirname "$ICON")" && pwd)/$(basename "$ICON")"
fi

# --- command to run -------------------------------------------------------------------------
# native launcher (self-contained or platform-specific package) or dotnet + jobrundj.dll.
# The absolute path of dotnet is written in the launcher: desktop launchers don't load the shell PATH.
CMD=()
if [ -x "$ROOT/jobrundj" ] && [ ! -d "$ROOT/jobrundj" ]; then
  CMD=("$ROOT/jobrundj")
else
  [ -f "$ROOT/jobrundj.dll" ] || die "jobrundj.dll not found in $ROOT. Keep new-shortcut.sh in the jobrundj folder."
  DOTNET="$(command -v dotnet || true)"
  for candidate in "${DOTNET_ROOT:-}/dotnet" /usr/local/share/dotnet/dotnet /usr/share/dotnet/dotnet /usr/lib/dotnet/dotnet "$HOME/.dotnet/dotnet"; do
    [ -n "$DOTNET" ] && break
    [ -x "$candidate" ] && DOTNET="$candidate"
  done
  [ -n "$DOTNET" ] || die "dotnet not found. Install the .NET 10 Runtime or set DOTNET_ROOT."
  # resolve symlinks (e.g. /usr/bin/dotnet) so the launcher keeps working with a minimal PATH
  DOTNET="$(readlink -f "$DOTNET" 2>/dev/null || echo "$DOTNET")"
  CMD=("$DOTNET" "$ROOT/jobrundj.dll")
fi
CMD+=(-j "$JOB")
[ -n "$UI" ] && CMD+=("$UI")
CMD+=(--pause)

SAFE_NAME="$(printf '%s' "$NAME" | tr '/:' '__')"

# --- macOS ----------------------------------------------------------------------------------
create_macos() {
  local folder="${OUTPUT:-$HOME/Desktop}"
  mkdir -p "$folder"
  local file="$folder/$SAFE_NAME.command"
  {
    echo "#!/bin/bash"
    echo "# jobrundj launcher for $JOB (created by new-shortcut.sh)"
    printf 'exec'
    printf ' %q' "${CMD[@]}"
    [ -n "$JOB_ARGS" ] && printf ' %s' "$JOB_ARGS"
    echo
  } > "$file"
  chmod +x "$file"

  if [ -n "$ICON" ]; then
    osascript -l JavaScript - "$ICON" "$file" >/dev/null <<'EOF' || echo "Warning: icon not set: use Get Info and paste the image on the icon." >&2
function run(argv) {
  ObjC.import('AppKit');
  var image = $.NSImage.alloc.initWithContentsOfFile(argv[0]);
  if (!image || image.isNil()) throw new Error('invalid image');
  if (!$.NSWorkspace.sharedWorkspace.setIconForFileOptions(image, argv[1], 0)) throw new Error('setIcon failed');
}
EOF
  fi
  echo "Created $file"
}

# --- Linux ----------------------------------------------------------------------------------
create_linux() {
  # Exec arguments are double quoted; refuse the characters that would need escaping
  local exec_line="" part
  for part in "${CMD[@]}"; do
    case "$part" in
      *[\"\`\$\\%]*) die "path not supported in a .desktop file (contains \" \` \$ \\ or %): $part" ;;
    esac
    exec_line+="\"$part\" "
  done
  exec_line="${exec_line% }"
  [ -n "$JOB_ARGS" ] && exec_line+=" $JOB_ARGS"

  local folders=()
  if [ -n "$OUTPUT" ]; then
    folders=("$OUTPUT")
  else
    folders=("${XDG_DATA_HOME:-$HOME/.local/share}/applications")
    local desktop
    desktop="$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")"
    [ -d "$desktop" ] && [ "$desktop" != "$HOME" ] && folders+=("$desktop")
  fi

  local id
  id="jobrundj-$(printf '%s' "$JOB" | tr -c 'A-Za-z0-9_-' '_')"
  local folder file
  for folder in "${folders[@]}"; do
    mkdir -p "$folder"
    file="$folder/$id.desktop"
    {
      echo "[Desktop Entry]"
      echo "Type=Application"
      echo "Version=1.0"
      echo "Name=$NAME"
      echo "Comment=jobrundj - $JOB"
      echo "Exec=$exec_line"
      echo "Path=$ROOT"
      echo "Terminal=true"
      echo "Icon=${ICON:-utilities-terminal}"
      echo "Categories=Utility;"
    } > "$file"
    chmod +x "$file"
    # GNOME: allow launching from the desktop without the "untrusted" prompt (best effort)
    command -v gio >/dev/null 2>&1 && gio set "$file" metadata::trusted true 2>/dev/null || true
    echo "Created $file"
  done
}

case "$(uname -s)" in
  Darwin) create_macos ;;
  Linux)  create_linux ;;
  *)      die "unsupported system $(uname -s): on Windows use new-shortcut.ps1" ;;
esac

printf 'Command:'; printf ' %q' "${CMD[@]}"; [ -n "$JOB_ARGS" ] && printf ' %s' "$JOB_ARGS"; echo
