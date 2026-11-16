#!/bin/sh
# Собирает tar.gz для Linux из готового publish-каталога.
#
#   packaging/linux/package.sh <publish-dir> <output.tar.gz>
#
# Каталог публикации должен быть самодостаточной сборкой FamilyMoney.Desktop
# (см. .github/workflows/release.yml). В архив добавляются установщик и иконки.

set -eu

if [ "$#" -ne 2 ]; then
    echo "Использование: $0 <publish-dir> <output.tar.gz>" >&2
    exit 2
fi

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
PUBLISH_DIR=$(CDPATH= cd -- "$1" && pwd)

case "$2" in
    /*) OUTPUT=$2 ;;
    *) OUTPUT="$(pwd)/$2" ;;
esac

if [ ! -f "${PUBLISH_DIR}/FamilyMoney.Desktop" ]; then
    echo "В ${PUBLISH_DIR} нет FamilyMoney.Desktop — это не каталог публикации." >&2
    exit 1
fi

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT INT TERM

cp -a "${PUBLISH_DIR}/." "${STAGE}/"
rm -f "${STAGE}"/*.pdb 2>/dev/null || true

cp "${ROOT}/packaging/linux/install.sh" "${STAGE}/install.sh"
chmod 755 "${STAGE}/install.sh" "${STAGE}/FamilyMoney.Desktop"

mkdir -p "${STAGE}/icons"
for icon in "${ROOT}"/resources/icon-*.png; do
    [ -e "$icon" ] || continue
    size=$(basename -- "$icon" .png)
    size=${size#icon-}
    cp "$icon" "${STAGE}/icons/familymoney-${size}.png"
done

mkdir -p "$(dirname -- "$OUTPUT")"
tar -czf "$OUTPUT" -C "$STAGE" .

echo "Готово: ${OUTPUT}"
du -h "$OUTPUT" 2>/dev/null || true
