#!/bin/sh
# Установка FamilyMoney в домашний каталог — без root и без пакетного менеджера.
#
#   ./install.sh                установить в ~/.local/opt/familymoney
#   ./install.sh --dir DIR      установить в каталог DIR
#   ./install.sh --uninstall    удалить приложение, ярлык и иконки
#
# Скрипт лежит в архиве из релиза рядом с FamilyMoney.Desktop, appsettings.json,
# нативными библиотеками и каталогом icons/. Данные (~/.local/share/FamilyMoney)
# при удалении не трогаются.

set -eu

APP_ID="familymoney"
APP_TITLE="Семейные деньги"
NO_ROOT_TITLE="Домашний учёт финансов"

HOME_DIR="${HOME:?HOME не задан}"
DATA_HOME="${XDG_DATA_HOME:-${HOME_DIR}/.local/share}"
DEFAULT_DIR="${HOME_DIR}/.local/opt/${APP_ID}"

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
INSTALL_DIR="${DEFAULT_DIR}"
DO_UNINSTALL=0

DESKTOP_FILE="${DATA_HOME}/applications/${APP_ID}.desktop"
ICONS_ROOT="${DATA_HOME}/icons/hicolor"

usage() {
    cat <<EOF
Установка «${APP_TITLE}» в домашний каталог.

  ./install.sh              установить в ${DEFAULT_DIR}
  ./install.sh --dir DIR    установить в каталог DIR
  ./install.sh --uninstall  удалить приложение и ярлык
EOF
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --dir)
            [ "$#" -ge 2 ] || { echo "Для --dir нужен каталог" >&2; exit 2; }
            INSTALL_DIR=$2
            shift 2
            ;;
        --uninstall)
            DO_UNINSTALL=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Неизвестный параметр: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

refresh_caches() {
    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "${DATA_HOME}/applications" >/dev/null 2>&1 || true
    fi
    if command -v gtk-update-icon-cache >/dev/null 2>&1 && [ -d "${ICONS_ROOT}" ]; then
        gtk-update-icon-cache -f -t "${ICONS_ROOT}" >/dev/null 2>&1 || true
    fi
}

remove_icon() {
    for apps in "${ICONS_ROOT}"/*/apps; do
        [ -d "$apps" ] || continue
        rm -f "${apps}/${APP_ID}.png"
    done
}

case "${INSTALL_DIR}" in
    ""|"/"|"//")
        echo "Небезопасный каталог установки: '${INSTALL_DIR}'" >&2
        exit 2
        ;;
esac

if [ "${DO_UNINSTALL}" -eq 1 ]; then
    # Страховка: удаляем только каталог, в котором действительно лежит приложение.
    if [ -d "${INSTALL_DIR}" ] && [ ! -f "${INSTALL_DIR}/FamilyMoney.Desktop" ]; then
        echo "В ${INSTALL_DIR} нет FamilyMoney.Desktop — удаление отменено." >&2
        exit 1
    fi
    rm -f "${DESKTOP_FILE}"
    remove_icon
    rm -rf "${INSTALL_DIR}"
    refresh_caches
    echo "Удалено: ${INSTALL_DIR}"
    echo "Данные в ${DATA_HOME}/FamilyMoney оставлены на месте."
    exit 0
fi

if [ ! -f "${SCRIPT_DIR}/FamilyMoney.Desktop" ]; then
    echo "Рядом со скриптом нет FamilyMoney.Desktop — распакуйте архив целиком." >&2
    exit 1
fi

mkdir -p "${INSTALL_DIR}"
for entry in "${SCRIPT_DIR}"/*; do
    case "$(basename -- "$entry")" in
        install.sh|icons) continue ;;
    esac
    cp -a "$entry" "${INSTALL_DIR}/"
done
chmod 755 "${INSTALL_DIR}/FamilyMoney.Desktop"

mkdir -p "${DATA_HOME}/applications"
cat > "${DESKTOP_FILE}" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=FamilyMoney
Name[ru]=${APP_TITLE}
GenericName=Family finance tracker
GenericName[ru]=${NO_ROOT_TITLE}
Comment=Accounts, transactions and reports
Comment[ru]=Счета, транзакции и отчёты
Exec="${INSTALL_DIR}/FamilyMoney.Desktop"
Icon=${APP_ID}
Terminal=false
Categories=Office;Finance;
Keywords=money;finance;budget;
StartupWMClass=${APP_ID}
EOF
chmod 644 "${DESKTOP_FILE}"

if [ -d "${SCRIPT_DIR}/icons" ]; then
    for icon in "${SCRIPT_DIR}"/icons/${APP_ID}-*.png; do
        [ -e "$icon" ] || continue
        size=$(basename -- "$icon" .png)
        size=${size#${APP_ID}-}
        case "$size" in
            *[!0-9]*) continue ;;
        esac
        mkdir -p "${ICONS_ROOT}/${size}x${size}/apps"
        cp -f "$icon" "${ICONS_ROOT}/${size}x${size}/apps/${APP_ID}.png"
    done
fi

refresh_caches

echo "Установлено: ${INSTALL_DIR}"
echo "Запуск: ${INSTALL_DIR}/FamilyMoney.Desktop или «${APP_TITLE}» в меню приложений."
echo "Данные: ${DATA_HOME}/FamilyMoney"
