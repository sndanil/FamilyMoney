# Банковский набор: исходники и сборка

Готовый набор лежит в `src/FamilyMoney.Navigation/Assets/Packs/banks/`.
Здесь — только исходники и инструмент, чтобы набор можно было пересобрать.

## Что где

- `src/gicons/` — квадратные иконки банков (256×256), получены из favicon
  официальных сайтов через Google favicon service. Используются как основа
  иконок набора.
- `src/icons/novikom.svg` — иконка НОВИКОМа с официального сайта.
- `pack.json` и PNG-ассеты — в `src/FamilyMoney.Navigation/Assets/Packs/banks/`.
- `docs/mockups/banks-icons.png` — контактный лист иконок.
- `docs/mockups/banks-cards.png` — контактный лист карт.
- `src/FamilyMoney.Navigation/Assets/Packs/banks/NOTICE-banks.md` — источники
  и порядок удаления по запросу.

## Сборка

Из корня репозитория:

```powershell
dotnet script resources/render_bank_pack.csx
```

Скрипт создаёт 40 PNG, `pack.json` и контактные листы. Карты — стилизованные,
генерируются по бренд-цветам и квадратным иконкам; это не официальные
изображения карточных продуктов.

Проверка набора:

```powershell
dotnet script resources/validate_bank_pack.csx
```

## Добавить банк

1. Положить квадратную иконку в `src/gicons/` (256×256 PNG).
2. Добавить запись в массив `banks` в `render_bank_pack.csx`: `id`, название,
   короткое имя, алиасы, путь к иконке, бренд-цвет, цвет текста, сайт.
3. Запустить скрипт, проверить контактные листы и `pack.json`.
4. Добавить строку в `NOTICE-banks.md`.

`id` существующих записей не меняются; карты и логотипы добавляются
аддитивно.
