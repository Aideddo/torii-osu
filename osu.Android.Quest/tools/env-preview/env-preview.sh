#!/usr/bin/env bash
# Offline check of the VR environment shaders (no headset needed): compiles them on the desktop's OpenGL ES 3 driver via surfaceless
# EGL, runs the dominant colour pass on a synthetic orange/teal background, and renders one eye's view of the ambient void and the
# custom field to PNGs.
#
#   env-preview.sh [framework dir] [output dir]
#
# Needs gcc, EGL/GLES3 headers and a GLES 3 capable driver (Mesa or NVIDIA), and ImageMagick for the PNGs.
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
FRAMEWORK=${1:-$HERE/../../../../toriirefresh-framework}
OUT=${2:-$(mktemp -d)}
mkdir -p "$OUT"

python3 -I - "$FRAMEWORK/osu.Framework/Platform/OpenXR/OpenXREnvironmentRenderer.cs" "$OUT" <<'PY'
import re, sys
src = open(sys.argv[1]).read()
for name, body in re.findall(r'private const string (\w+) = @"(.*?)";', src, re.S):
    ext = 'vert' if 'vertex' in name else 'frag'
    open(f'{sys.argv[2]}/{name}.{ext}', 'w').write(body.replace('""', '"'))
PY

gcc -o "$OUT/check" "$HERE/check.c" -lEGL -lGLESv2
gcc -o "$OUT/render" "$HERE/render.c" -lEGL -lGLESv2 -lm

cd "$OUT"
./check fullscreen_vertex.vert luminance_fragment.frag adapt_fragment.frag dominant_fragment.frag colour_adapt_fragment.frag scene_fragment.frag
./render "$OUT" 0 -5 void.ppm && magick void.ppm void.png
./render "$OUT" 1 -5 field.ppm && magick field.ppm field.png
echo "previews: $OUT/void.png $OUT/field.png"
