#!/usr/bin/env bash
# Sestaví WebGL verzi hry a nahraje ji na GitHub Pages (větev gh-pages).
# Použití:  Tools/publish_webgl.sh            (sestaví + nahraje)
#           SKIP_BUILD=1 Tools/publish_webgl.sh  (jen nahraje existující Build/WebGL)
set -euo pipefail
cd "$(dirname "$0")/.."
PROJ="$(pwd)"
UNITY="${UNITY:-/workspace/unity/2022.3.62f3/Editor/Unity}"
REPO_URL="$(git remote get-url origin)"

if [ "${SKIP_BUILD:-0}" != "1" ]; then
  ulimit -n 65536 || true
  "$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
    -executeMethod SnakeBatch.BuildScene -logFile /tmp/snake_webgl_scene.log
  "$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" -buildTarget WebGL \
    -executeMethod SnakeBatch.BuildWebGL -logFile /tmp/snake_webgl_build.log
fi
test -f Build/WebGL/index.html

TMP="$(mktemp -d)"
cp -r Build/WebGL/. "$TMP/"
touch "$TMP/.nojekyll"
cd "$TMP"
git init -q -b gh-pages
git add -A
git -c user.name="$(git -C "$PROJ" config user.name)" -c user.email="$(git -C "$PROJ" config user.email)" \
  commit -q -m "WebGL build $(date '+%Y-%m-%d %H:%M')"
git push -f "$REPO_URL" gh-pages
cd "$PROJ"; rm -rf "$TMP"
echo "Hotovo: https://adamino32cz.github.io/snake3d/ (projeví se do 1–2 minut)"
