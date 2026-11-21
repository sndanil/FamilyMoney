#!/usr/bin/env python3
"""Download free OpenMoji artwork and build the bundled ready-picture packs.

Source: https://openmoji.org/ (CC BY-SA 4.0).
The script renders each 72x72 SVG into a transparent 256x256 PNG and writes
pack.json + NOTICE-openmoji.md next to it. It is intentionally re-runnable.
"""

from __future__ import annotations

import io
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

import cairosvg
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PACKS_ROOT = ROOT / "src" / "FamilyMoney.Navigation" / "Assets" / "Packs"
OPENMOJI_REV = "3e01de523ddefe9c5e46a7765b15efef04bf9002"
META_URL = f"https://raw.githubusercontent.com/hfg-gmuend/openmoji/{OPENMOJI_REV}/data/openmoji.json"
SVG_URL = "https://raw.githubusercontent.com/hfg-gmuend/openmoji/{rev}/color/svg/{hexcode}.svg"
SOURCE_ROOT = "https://github.com/hfg-gmuend/openmoji/blob/{rev}/color/svg/{hexcode}.svg"
LICENSE_URL = "https://creativecommons.org/licenses/by-sa/4.0/"

PACKS = {
    "business-finance": {
        "title": "Бизнес и финансы",
        "assets": [
            ("briefcase", "Портфель", "1F4BC", ["работа", "офис", "бизнес"]),
            ("chart-up", "Рост", "1F4C8", ["график", "инвестиции", "прибыль"]),
            ("analytics", "Аналитика", "1F4CA", ["график", "отчёт", "статистика"]),
            ("money-bag", "Деньги", "1F4B0", ["доход", "прибыль", "наличные"]),
            ("bank", "Банк", "1F3E6", ["счёт", "банки"]),
            ("office", "Офис", "1F3E2", ["здание", "работа", "компания"]),
            ("deal", "Сделка", "1F91D", ["партнёры", "договор", "рукопожатие"]),
            ("receipt", "Чек", "1F9FE", ["квитанция", "покупка", "расход"]),
            ("cash", "Валюта", "1F4B5", ["доллар", "наличные", "деньги"]),
            ("chart-down", "Расходы", "1F4C9", ["график", "убыток", "снижение"]),
            ("computer", "Компьютер", "1F5A5", ["работа", "техника", "ноутбук"]),
            ("idea", "Идея", "1F4A1", ["стартап", "мысль", "лампочка"]),
        ],
    },
    "products": {
        "title": "Продукты",
        "assets": [
            ("apple", "Яблоко", "1F34E", ["фрукты", "еда"]),
            ("bread", "Хлеб", "1F35E", ["булочка", "еда", "выпечка"]),
            ("milk", "Молоко", "1F95B", ["напитки", "молочное"]),
            ("cheese", "Сыр", "1F9C0", ["молочное", "еда"]),
            ("egg", "Яйцо", "1F95A", ["завтрак", "еда"]),
            ("chicken", "Курица", "1F357", ["мясо", "птица", "еда"]),
            ("fish", "Рыба", "1F41F", ["морепродукты", "еда"]),
            ("broccoli", "Брокколи", "1F966", ["овощи", "еда"]),
            ("carrot", "Морковь", "1F955", ["овощи", "еда"]),
            ("banana", "Банан", "1F34C", ["фрукты", "еда"]),
            ("grapes", "Виноград", "1F347", ["фрукты", "еда"]),
            ("potato", "Картофель", "1F954", ["овощи", "еда"]),
            ("onion", "Лук", "1F9C5", ["овощи", "еда"]),
            ("tomato", "Помидор", "1F345", ["овощи", "еда"]),
        ],
    },
    "home": {
        "title": "Дом и квартира",
        "assets": [
            ("house", "Дом", "1F3E0", ["квартира", "жильё"]),
            ("sofa", "Диван", "1F6CB", ["мебель", "гостиная"]),
            ("bed", "Кровать", "1F6CF", ["мебель", "спальня"]),
            ("shower", "Душ", "1F6BF", ["ванная", "сантехника"]),
            ("basket", "Корзина", "1F9FA", ["быт", "покупки"]),
            ("plant", "Растение", "1FAB4", ["цветы", "интерьер"]),
            ("candle", "Свеча", "1F56F", ["уют", "декор"]),
            ("picture", "Картина", "1F5BC", ["декор", "интерьер"]),
            ("door", "Дверь", "1F6AA", ["вход", "квартира"]),
            ("window", "Окно", "1FA9F", ["дом", "интерьер"]),
            ("cleaning", "Уборка", "1F9F9", ["быт", "чистота"]),
            ("sponge", "Губка", "1F9FD", ["уборка", "быт"]),
            ("chair", "Стул", "1FA91", ["мебель", "интерьер"]),
            ("bath", "Ванна", "1F6C1", ["ванная", "сантехника"]),
        ],
    },
    "transport": {
        "title": "Машина и транспорт",
        "assets": [
            ("car", "Машина", "1F697", ["авто", "автомобиль"]),
            ("taxi", "Такси", "1F695", ["поездка", "машина"]),
            ("bus", "Автобус", "1F68C", ["транспорт", "поездка"]),
            ("train", "Поезд", "1F686", ["жд", "электричка"]),
            ("airplane", "Самолёт", "2708", ["авиа", "путешествие"]),
            ("fuel", "Заправка", "26FD", ["бензин", "топливо"]),
            ("bicycle", "Велосипед", "1F6B2", ["велик", "спорт"]),
            ("scooter", "Самокат", "1F6F5", ["транспорт", "город"]),
            ("truck", "Грузовик", "1F69A", ["доставка", "перевозка"]),
            ("ship", "Корабль", "1F6A2", ["море", "круиз"]),
            ("metro", "Метро", "1F687", ["транспорт", "город"]),
            ("wheel", "Колесо", "1F6DE", ["машина", "шины"]),
            ("traffic-light", "Светофор", "1F6A6", ["дорога", "город"]),
            ("map", "Карта", "1F5FA", ["навигация", "путь"]),
        ],
    },
    "leisure": {
        "title": "Отдых",
        "assets": [
            ("beach", "Пляж", "1F3D6", ["отпуск", "море"]),
            ("party", "Праздник", "1F389", ["вечеринка", "событие"]),
            ("cinema", "Кино", "1F3AC", ["фильм", "развлечение"]),
            ("games", "Игры", "1F3AE", ["гейминг", "досуг"]),
            ("music", "Музыка", "1F3B5", ["концерт", "песня"]),
            ("camping", "Кемпинг", "1F3D5", ["поход", "палатка"]),
            ("fishing", "Рыбалка", "1F3A3", ["хобби", "отдых"]),
            ("sport", "Спорт", "26BD", ["футбол", "игра"]),
            ("popcorn", "Кинотеатр", "1F37F", ["фильм", "снек"]),
            ("tickets", "Билеты", "1F39F", ["событие", "театр"]),
            ("trip", "Поездка", "1F6EB", ["путешествие", "отпуск"]),
            ("island", "Отпуск", "1F3DD", ["море", "отдых"]),
            ("art", "Творчество", "1F3A8", ["хобби", "рисование"]),
        ],
    },
    "animals": {
        "title": "Животные",
        "assets": [
            ("dog", "Собака", "1F436", ["питомец", "пёс"]),
            ("cat", "Кошка", "1F431", ["питомец", "кот"]),
            ("rabbit", "Кролик", "1F430", ["питомец", "заяц"]),
            ("fox", "Лиса", "1F98A", ["животное", "лес"]),
            ("bear", "Медведь", "1F43B", ["животное", "лес"]),
            ("panda", "Панда", "1F43C", ["животное", "зоопарк"]),
            ("koala", "Коала", "1F428", ["животное", "зоопарк"]),
            ("tiger", "Тигр", "1F42F", ["животное", "зоопарк"]),
            ("lion", "Лев", "1F981", ["животное", "зоопарк"]),
            ("cow", "Корова", "1F42E", ["животное", "ферма"]),
            ("pig", "Свинья", "1F437", ["животное", "ферма"]),
            ("frog", "Лягушка", "1F438", ["животное", "пруд"]),
            ("monkey", "Обезьяна", "1F435", ["животное", "зоопарк"]),
            ("chicken", "Курица", "1F414", ["птица", "ферма"]),
        ],
    },
    "education": {
        "title": "Образование",
        "assets": [
            ("graduation", "Выпускной", "1F393", ["учёба", "диплом"]),
            ("books", "Книги", "1F4DA", ["учёба", "библиотека"]),
            ("pencil", "Карандаш", "270F", ["учёба", "письмо"]),
            ("school", "Школа", "1F3EB", ["учёба", "здание"]),
            ("notes", "Заметки", "1F4DD", ["письмо", "учёба"]),
            ("textbook", "Учебник", "1F4D6", ["книга", "учёба"]),
            ("microscope", "Микроскоп", "1F52C", ["наука", "биология"]),
            ("chemistry", "Химия", "1F9EA", ["наука", "опыт"]),
            ("geometry", "Геометрия", "1F4D0", ["учёба", "математика"]),
            ("pen", "Ручка", "1F58A", ["письмо", "учёба"]),
            ("backpack", "Рюкзак", "1F392", ["школа", "учёба"]),
            ("abacus", "Счёт", "1F9EE", ["математика", "учёба"]),
        ],
    },
    "fashion": {
        "title": "Одежда и косметика",
        "assets": [
            ("tshirt", "Футболка", "1F455", ["одежда", "верх"]),
            ("jeans", "Джинсы", "1F456", ["одежда", "брюки"]),
            ("dress", "Платье", "1F457", ["одежда", "женское"]),
            ("heels", "Туфли", "1F460", ["обувь", "каблук"]),
            ("sneakers", "Кроссовки", "1F45F", ["обувь", "спорт"]),
            ("coat", "Пальто", "1F9E5", ["одежда", "верхняя"]),
            ("cap", "Кепка", "1F9E2", ["головной убор", "аксессуар"]),
            ("bag", "Сумка", "1F45C", ["аксессуар", "одежда"]),
            ("lipstick", "Помада", "1F484", ["косметика", "макияж"]),
            ("nails", "Маникюр", "1F485", ["косметика", "ногти"]),
            ("lotion", "Крем", "1F9F4", ["косметика", "уход"]),
            ("mirror", "Зеркало", "1FA9E", ["уход", "косметика"]),
            ("sunglasses", "Очки", "1F576", ["аксессуар", "лето"]),
            ("soap", "Мыло", "1F9FC", ["гигиена", "уход"]),
        ],
    },
    "health": {
        "title": "Здоровье",
        "assets": [
            ("hospital", "Больница", "1F3E5", ["медицина", "врач"]),
            ("pill", "Таблетка", "1F48A", ["лекарство", "аптека"]),
            ("stethoscope", "Стетоскоп", "1FA7A", ["врач", "осмотр"]),
            ("tooth", "Зуб", "1F9B7", ["стоматология", "врач"]),
            ("brain", "Мозг", "1F9E0", ["здоровье", "мышление"]),
            ("heart", "Сердце", "2764", ["здоровье", "любовь"]),
            ("bandage", "Пластырь", "1FA79", ["травма", "аптека"]),
            ("dna", "ДНК", "1F9EC", ["анализы", "наука"]),
            ("syringe", "Укол", "1F489", ["прививка", "медицина"]),
            ("bone", "Кость", "1F9B4", ["скелет", "здоровье"]),
            ("glasses", "Очки", "1F453", ["зрение", "аксессуар"]),
            ("meditation", "Медитация", "1F9D8", ["спокойствие", "йога"]),
            ("run", "Бег", "1F3C3", ["спорт", "здоровье"]),
        ],
    },
}


def fetch(url: str, *, timeout: int = 30) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "FamilyMoney-pack-builder/1.0"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


def load_metadata() -> dict[str, dict]:
    payload = json.loads(fetch(META_URL).decode("utf-8"))
    return {item["hexcode"].upper(): item for item in payload if item.get("hexcode")}


def render(svg_bytes: bytes, destination: Path) -> None:
    buffer = io.BytesIO()
    cairosvg.svg2png(
        bytestring=svg_bytes,
        write_to=buffer,
        output_width=256,
        output_height=256,
        background_color=None,
    )
    buffer.seek(0)
    with Image.open(buffer) as image:
        image = image.convert("RGBA")
        image.save(destination, "PNG", optimize=True)


def main() -> int:
    metadata = load_metadata()
    errors: list[str] = []
    total = 0

    for pack_id, pack in PACKS.items():
        directory = PACKS_ROOT / pack_id
        directory.mkdir(parents=True, exist_ok=True)
        assets = []
        used_ids: set[str] = set()

        for slug, name, hexcode, aliases in pack["assets"]:
            if slug in used_ids:
                errors.append(f"{pack_id}: duplicate id {slug}")
                continue
            used_ids.add(slug)
            meta = metadata.get(hexcode.upper(), {})
            source_url = SOURCE_ROOT.format(rev=OPENMOJI_REV, hexcode=hexcode)
            svg_url = SVG_URL.format(rev=OPENMOJI_REV, hexcode=hexcode)
            destination = directory / f"{slug}.png"
            try:
                svg = fetch(svg_url)
            except urllib.error.HTTPError as error:
                errors.append(f"{pack_id}/{slug}: HTTP {error.code} for {hexcode}")
                continue

            try:
                render(svg, destination)
            except Exception as error:  # noqa: BLE001 - report and continue
                errors.append(f"{pack_id}/{slug}: render failed: {error}")
                continue

            size = destination.stat().st_size
            if size > 32 * 1024:
                errors.append(f"{pack_id}/{slug}: PNG {size} bytes > 32 KB")
                continue

            english = meta.get("annotation", "")
            tags = [value.strip() for value in meta.get("tags", "").split(",") if value.strip()]
            search_aliases = list(dict.fromkeys([*aliases, english, *tags[:4]]))
            assets.append(
                {
                    "id": slug,
                    "name": name,
                    "aliases": search_aliases,
                    "file": destination.name,
                    "background": "#FFFFFF",
                    "kind": "illustration",
                    "source": source_url,
                    "addedIn": "1.1.0",
                }
            )
            total += 1

        if len(assets) < 10:
            errors.append(f"{pack_id}: only {len(assets)} assets")

        manifest = {
            "id": pack_id,
            "title": pack["title"],
            "version": 1,
            "notice": "NOTICE-openmoji.md",
            "assets": assets,
        }
        (directory / "pack.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

        notice = [
            f"# {pack['title']} — OpenMoji",
            "",
            "Картинки: [OpenMoji](https://openmoji.org/), лицензия [CC BY-SA 4.0]"
            f"({LICENSE_URL}).",
            "",
            "SVG из OpenMoji отрендерены в PNG 256×256 с прозрачным фоном; "
            "графика не изменялась. Атрибуция по каждому файлу:",
            "",
            "| Файл | Emoji | Автор OpenMoji |",
            "|---|---|---|",
        ]
        for asset in assets:
            meta = metadata.get(
                next(
                    hexcode
                    for slug, _, hexcode, _ in pack["assets"]
                    if slug == asset["id"]
                ).upper(),
                {},
            )
            emoji = meta.get("emoji", "")
            author = meta.get("openmoji_author", "OpenMoji contributors")
            notice.append(f"| {asset['file']} | {emoji} | {author} |")
        notice.extend(
            [
                "",
                "Если вы правообладатель и хотите изменить или убрать "
                "изображение — откройте issue в FamilyMoney.",
            ]
        )
        (directory / "NOTICE-openmoji.md").write_text("\n".join(notice) + "\n", encoding="utf-8")

    if errors:
        print("Ошибки:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        print(f"Собрано ассетов: {total}", file=sys.stderr)
        return 1

    print(f"OK: {len(PACKS)} групп, {total} PNG")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
