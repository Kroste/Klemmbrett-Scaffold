#!/usr/bin/env bash
# Baut das AppImage aus einem fertigen linux-x64-Publish-Ordner (Kroste-Standard).
# Aufruf: packaging/linux/build-appimage.sh <version> <publish-dir>
# Hinweis: --appimage-extract-and-run ist noetig, weil im CI kein FUSE verfuegbar ist.
set -euo pipefail

VERSION="$1"
PUBLISH_DIR="$2"
APPDIR="AppDir"

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
cp -r "$PUBLISH_DIR"/* "$APPDIR/usr/bin/"
cp packaging/linux/Klemmbrett.desktop "$APPDIR/"
cp Klemmbrett/Assets/Klemmbrett.png "$APPDIR/"
cp packaging/linux/AppRun "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/Klemmbrett"

curl -sSL -o appimagetool \
  https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
chmod +x appimagetool
./appimagetool --appimage-extract-and-run "$APPDIR" "Klemmbrett-${VERSION}-x86_64.AppImage"
echo "AppImage gebaut: Klemmbrett-${VERSION}-x86_64.AppImage"
