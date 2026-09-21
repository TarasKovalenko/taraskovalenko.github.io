#!/usr/bin/env bash
#
# Capture headless Chrome screenshots of the built site (_site) for visual review.
#
# Usage: bash tools/screenshots.sh <out-dir> [url-path ...]

set -euo pipefail

OUT_DIR="${1:?Usage: bash tools/screenshots.sh <out-dir> [url-path ...]}"
shift
if (($# == 0)); then
  set -- / /en/ /posts/result-pattern/ /posts/cli-jit-il/ /posts/mongodb-encryption/ /paths/ /categories/ /archives/ /about/ /404.html
fi

CHROME="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
PORT="${PORT:-4010}"

mkdir -p "$OUT_DIR"
python3 -m http.server "$PORT" -d _site >/dev/null 2>&1 &
SERVER_PID=$!
trap 'kill "$SERVER_PID"' EXIT
sleep 1

for path in "$@"; do
  name="$(echo "$path" | sed 's#^/##; s#/$##; s#[/.]#-#g')"
  name="${name:-home}"
  for width in 1440 390; do
    for theme in light dark; do
      flags=()
      if [[ $theme == dark ]]; then
        flags+=(--blink-settings=preferredColorScheme=0)
      else
        flags+=(--blink-settings=preferredColorScheme=1)
      fi
      "$CHROME" --headless=new --disable-gpu --hide-scrollbars ${flags[@]+"${flags[@]}"} \
        --window-size="$width,2400" \
        --screenshot="$OUT_DIR/$name-$width-$theme.png" \
        "http://localhost:$PORT$path" >/dev/null 2>&1
    done
  done
done
echo "Screenshots saved to $OUT_DIR"
