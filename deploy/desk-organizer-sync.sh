#!/bin/bash
# Synchronise desk-organizer.artmonie.com avec GitHub :
#  1. miroir Git du dépôt dans /srv/git/PersonalComputerDeskOrganizer.git
#  2. télécharge le zip de la release "latest" quand GitHub Actions en publie un nouveau
set -euo pipefail

REPO="mathiasgero-hub/PersonalComputerDeskOrganizer"
ASSET="PersonalComputerDeskOrganizer.zip"
MIRROR="/srv/git/PersonalComputerDeskOrganizer.git"
WEB="/var/www/desk-organizer"
DL="$WEB/download"
STATE="$DL/.asset-id"

# 1. Miroir Git
if [ ! -d "$MIRROR" ]; then
  git clone --mirror -q "https://github.com/$REPO.git" "$MIRROR"
else
  git -C "$MIRROR" remote update --prune >/dev/null
fi

# 2. Release
json=$(curl -fsSL -H "Accept: application/vnd.github+json" "https://api.github.com/repos/$REPO/releases/tags/latest")
asset_id=$(jq -r --arg n "$ASSET" '.assets[] | select(.name==$n) | .id' <<<"$json")
[ -n "$asset_id" ] || { echo "asset $ASSET introuvable"; exit 1; }
[ -f "$STATE" ] && [ "$(cat "$STATE")" = "$asset_id" ] && exit 0

mkdir -p "$DL"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
curl -fsSL -o "$tmp/$ASSET" "https://github.com/$REPO/releases/download/latest/$ASSET"
unzip -q "$tmp/$ASSET" -d "$tmp/x"
exe=$(find "$tmp/x" -maxdepth 1 -iname '*.exe' | head -1)

install -m 644 "$tmp/$ASSET" "$DL/$ASSET"

# Installateur (présent depuis le build 1.0.x avec Inno Setup)
SETUP="PersonalComputerDeskOrganizer-Setup.exe"
setupSize=0
if jq -e --arg n "$SETUP" '.assets[] | select(.name==$n)' <<<"$json" >/dev/null; then
  curl -fsSL -o "$tmp/$SETUP" "https://github.com/$REPO/releases/download/latest/$SETUP"
  install -m 644 "$tmp/$SETUP" "$DL/$SETUP"
  setupSize=$(stat -c %s "$DL/$SETUP")
fi
if [ -n "$exe" ]; then
  # .gz servi par nginx (gzip_static) : ~80 Mo transférés au lieu de ~200 Mo
  gzip -9 -c "$exe" > "$tmp/exe.gz"
  install -m 644 "$exe" "$DL/PersonalComputerDeskOrganizer.exe"
  install -m 644 "$tmp/exe.gz" "$DL/PersonalComputerDeskOrganizer.exe.gz"
fi

sha=$(git -C "$MIRROR" rev-parse --short main)
version=$(jq -r '.name' <<<"$json" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' || true)
jq -n --arg version "$version" --argjson setupSize "$setupSize" --arg date "$(jq -r --arg n "$ASSET" '.assets[] | select(.name==$n) | .updated_at' <<<"$json")" \
      --arg commit "$sha" \
      --arg message "$(git -C "$MIRROR" log -1 --format=%s main)" \
      --argjson zipSize "$(stat -c %s "$DL/$ASSET")" \
      --argjson exeSize "$( [ -n "$exe" ] && stat -c %s "$DL/PersonalComputerDeskOrganizer.exe" || echo 0)" \
      '{version:$version, setupSize:$setupSize, date:$date, commit:$commit, message:$message, zipSize:$zipSize, exeSize:$exeSize}' > "$DL/version.json"
echo "$asset_id" > "$STATE"
echo "Nouveau build synchronisé : $sha ($asset_id)"
