#!/usr/bin/env bash
# Dev loop helper for the Quest (VR) build.
#
#   quest-dev.sh build | deploy | launch | shot <name> | fps | log [regex] | tap <u> <v> | songselect | autoplay | all <name>
#
# Requirements:
#   DOTNET      .NET SDK with the android workload (default: ~/.dotnet/dotnet)
#   JAVA_HOME   a JDK 21 (default: /usr/lib/jvm/java-21-temurin-jdk)
#   ANDROID_SDK Android SDK (default: ~/Android/Sdk)
#   OPTIMIZE    compile with optimizations (default true). A plain Debug build runs the JIT without optimizations, which costs ~3x
#               the CPU time per frame. Switching it needs a full rebuild (REBUILD=1), as incremental builds don't notice the change.
#   HEADLOCK=1  launch with the panel locked to the head (debug; controller pointer input disabled), so screenshots show the game while
#               the headset lies on a desk. Default 0. Remove with: adb shell rm <files dir>/debug-headlock-panel
#
# Screenshots of the headset view (as composited, all layers) are numbered and saved to vr-screenshots/ (untracked).
# If the headset sleeps while not worn: adb shell am broadcast -a com.oculus.vrpowermanager.prox_close (undo: ...automation_disable).
set -euo pipefail
REPO=$(git -C "$(dirname "$0")" rev-parse --show-toplevel)
PKG=sh.shikkesora.torii.vr
FILES=/sdcard/Android/data/$PKG/files
APK=$REPO/osu.Android.Quest/bin/Debug/net10.0-android/$PKG-Signed.apk
OUT=$REPO/vr-screenshots
DOTNET=${DOTNET:-$HOME/.dotnet/dotnet}
export JAVA_HOME=${JAVA_HOME:-/usr/lib/jvm/java-21-temurin-jdk}
ANDROID_SDK=${ANDROID_SDK:-$HOME/Android/Sdk}
mkdir -p "$OUT"

pause() { timeout "$1" cat /dev/zero >/dev/null 2>&1 || true; }

build() {
  "$DOTNET" build "$REPO/osu.Android.Quest/osu.Android.Quest.csproj" -c Debug -nologo -v q -p:Optimize="${OPTIMIZE:-true}" \
    $([ "${REBUILD:-0}" = 1 ] && echo --no-incremental) \
    -p:AndroidSdkDirectory="$ANDROID_SDK" -p:JavaSdkDirectory="$JAVA_HOME" 2>&1 | grep -E " error |Error\(s\)" | sort -u
}
# Not incremental: an incremental install interrupted by a dropped USB connection can end with the package deleted (and its data with it).
deploy() { adb install -r --no-incremental "$APK" 2>&1 | tail -1; }
launch() {
  adb shell input keyevent KEYCODE_WAKEUP
  if [ "${HEADLOCK:-0}" = 1 ]; then adb shell touch "$FILES/debug-headlock-panel"; else adb shell rm -f "$FILES/debug-headlock-panel"; fi
  adb shell am force-stop $PKG
  adb logcat -c
  adb shell monkey -p $PKG -c com.oculus.intent.category.VR 1 >/dev/null 2>&1
  timeout 40 adb logcat -v brief 2>/dev/null | grep -m1 -E "entered MainMenu|F/libc|OpenXR failed" || true
}
shot() {
  local n; n=$(ls "$OUT" 2>/dev/null | grep -c -E '^[0-9]{3}-' || true)
  local name; name=$(printf "%03d-%s" $((n + 1)) "${1:-shot}")
  local before; before=$(adb shell ls -t /sdcard/Oculus/Screenshots | head -1)
  adb shell am startservice -n com.oculus.metacam/.capture.CaptureService -a TAKE_SCREENSHOT >/dev/null 2>&1
  for _ in $(seq 1 20); do
    local f; f=$(adb shell ls -t /sdcard/Oculus/Screenshots | head -1)
    if [ "$f" != "$before" ]; then
      local last=-1 size=0
      for _ in $(seq 1 20); do
        size=$(adb shell stat -c %s "/sdcard/Oculus/Screenshots/$f" 2>/dev/null || echo 0)
        if [ "$size" -gt 0 ] && [ "$size" = "$last" ]; then break; fi
        last=$size; pause 0.3
      done
      adb pull "/sdcard/Oculus/Screenshots/$f" "$OUT/$name.jpg" >/dev/null; echo "$OUT/$name.jpg"; return
    fi
    pause 0.5
  done
  echo "no screenshot"
}
fps() { timeout 8 adb logcat -v brief 2>/dev/null | grep "VrApi.*FPS" | tail -2 | sed -E 's/.*(FPS=[^,]*).*(Stale=[^,]*).*(App=[^,]*).*(CPU&GPU=[^,]*).*(LCnt=[^,]*).*/\1 \2 \3 \4 \5/' || true; }
log() { local L; L=$(adb shell ls -t "$FILES/logs" | grep runtime | head -1); adb shell cat "$FILES/logs/$L" | grep -E "${1:-OpenXR|rror|xception}" | grep -v "SDL error\|at SDL_Set" | cut -c1-300 | tail -40; }
# Panel coordinates u, v in 0..1 -> Android surface (4128x2208); SDL scales touches to the panel.
tap() { adb shell input tap "$(python3 -c "print(int($1*4128))")" "$(python3 -c "print(int($2*2208))")"; }

case "${1:-}" in
  build) build ;;
  deploy) deploy ;;
  launch) launch ;;
  shot) shot "${2:-shot}" ;;
  fps) fps ;;
  log) log "${2:-}" ;;
  tap) tap "$2" "$3" ;;
  songselect)
    # Fresh launch only: dismiss first-run setup, open the menu (the login form grabs focus), tap empty space, then play -> solo.
    adb shell "input tap 3847 90; sleep 1.5; input keyevent KEYCODE_P; sleep 1.2; input tap 800 1900; sleep 0.8; input keyevent KEYCODE_P; sleep 0.8; input keyevent KEYCODE_P"
    pause 5 ;;
  autoplay) adb shell input keycombination KEYCODE_CTRL_LEFT KEYCODE_ENTER ;;
  all) build && deploy && launch && shot "${2:-shot}" && fps ;;
  *) sed -n '2,4p' "$0"; exit 1 ;;
esac
