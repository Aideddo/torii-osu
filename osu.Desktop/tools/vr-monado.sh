#!/usr/bin/env bash
# Torii: runs the desktop build in VR mode (--vr) against Monado's simulated headset and controllers, for developing without a headset.
#
# Usage: osu.Desktop/tools/vr-monado.sh [build|run|shot <name>]   (default: build + run)
#   build        build osu.Desktop (Debug)
#   run          start monado-service if needed, then run the game with --vr (foreground)
#   shot <name>  save a screenshot of Monado's window to vr-screenshots/<name>.png (needs ImageMagick's import and xprop)
#
# Monado's "qwerty" driver simulates the devices: focus Monado's window and drive the headset/controllers with mouse and keyboard
# (see src/xrt/drivers/qwerty/README.md in the Monado repository for the bindings).
# With REMOTE=1, Monado's "remote" driver simulates them instead (Index controllers with grip and thumbsticks), scripted with
# osu.Desktop/tools/vr-remote.py. A running monado-service with the other driver is restarted.
# OpenXR debug flags (debug-headlock-panel, debug-frame-timing) are files in <storage>/vr-debug.
set -euo pipefail

REPO=$(cd "$(dirname "$0")/../.." && pwd)
DOTNET=${DOTNET:-$HOME/.dotnet/dotnet}
BIN=$REPO/osu.Desktop/bin/Debug/net10.0
OUT=$REPO/vr-screenshots
LOGDIR=${XDG_RUNTIME_DIR:-/tmp}/torii-monado
export XR_RUNTIME_JSON=${XR_RUNTIME_JSON:-/usr/share/openxr/1/openxr_monado.json}

build() { "$DOTNET" build "$REPO/osu.Desktop/osu.Desktop.csproj" -c Debug -nologo -v q 2>&1 | grep -E " error |Error\(s\)" | sort -u; }

monado() {
  local mode=qwerty
  [ "${REMOTE:-0}" = 1 ] && mode=remote
  if pgrep -x monado-service >/dev/null; then
    [ "$(cat "$LOGDIR/mode" 2>/dev/null || echo qwerty)" = "$mode" ] && return
    pkill -x monado-service; sleep 1
  fi
  mkdir -p "$LOGDIR"
  echo "$mode" >"$LOGDIR/mode"
  # The remote driver is chosen by Monado's config file, kept separate from the user's own.
  mkdir -p "$LOGDIR/config/monado"
  echo '{"active": "remote", "remote": {"version": 0, "port": 4242}}' >"$LOGDIR/config/monado/config_v0.json"
  local env=(QWERTY_ENABLE=1)
  [ "$mode" = remote ] && env=(XDG_CONFIG_HOME="$LOGDIR/config")
  # Monado's Wayland compositor target needs a present-capable queue some drivers lack; its X11 window (XWayland) works everywhere.
  # monado-service exits if stdin can't be polled, so it gets a never-ending one.
  (tail -f /dev/null | env "${env[@]}" XRT_COMPOSITOR_FORCE_XCB=1 monado-service >"$LOGDIR/service.log" 2>&1 &)
  for _ in $(seq 50); do [ -S "${XDG_RUNTIME_DIR:-/tmp}/monado_comp_ipc" ] && return; sleep 0.2; done
  echo "monado-service didn't start; see $LOGDIR/service.log" >&2
  exit 1
}

run() { monado; cd "$BIN" && ./torii --vr; }

shot() {
  local win
  for w in $(xprop -root _NET_CLIENT_LIST | grep -o '0x[0-9a-f]*'); do
    xprop -id "$w" WM_NAME | grep -q '"Monado"' && win=$w
  done
  [ -n "${win:-}" ] || { echo "Monado's window not found" >&2; exit 1; }
  mkdir -p "$OUT"
  import -window "$win" "$OUT/$1.png" && echo "$OUT/$1.png"
}

case "${1:-all}" in
  build) build ;;
  run) run ;;
  shot) shot "${2:?name}" ;;
  all) build; run ;;
  *) sed -n '2,12p' "$0"; exit 1 ;;
esac
