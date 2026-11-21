# Готовые картинки: OpenMoji

Категорийные наборы в `Assets/Packs/<id>/` собраны из [OpenMoji](https://openmoji.org/)
и распространяются по лицензии [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
Атрибуция по каждому файлу — в `NOTICE-openmoji.md` рядом с `pack.json`.

| Каталог | Группа | Картинок |
|---|---|---:|
| `banks` | Банки | 40 |
| `business-finance` | Бизнес и финансы | 12 |
| `products` | Продукты | 14 |
| `home` | Дом и квартира | 14 |
| `transport` | Машина и транспорт | 14 |
| `leisure` | Отдых | 13 |
| `animals` | Животные | 14 |
| `education` | Образование | 12 |
| `fashion` | Одежда и косметика | 14 |
| `health` | Здоровье | 13 |

SVG отрендерены в PNG 256×256 с прозрачным фоном; исходная графика OpenMoji
не менялась, только масштаб. Скрипт повторной сборки — `resources/fetch_openmoji_packs.py`
(OpenMoji зафиксирован на коммите `3e01de523ddefe9c5e46a7765b15efef04bf9002`).
Для запуска нужны Python-пакеты `cairosvg` и `Pillow`.

`banks` использует отдельный `NOTICE-banks.md`: логотипы принадлежат банкам,
это не часть OpenMoji.
