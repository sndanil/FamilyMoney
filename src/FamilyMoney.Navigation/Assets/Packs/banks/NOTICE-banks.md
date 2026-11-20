# Набор «Банки» — источники и замечания

Набор поставляется с приложением FamilyMoney для идентификации банков в счетах.
Логотипы и товарные знаки принадлежат соответствующим банкам. Если вы
правообладатель и хотите изменить или убрать изображение — откройте issue или
пришлите MR; ассет будет помечен `deprecated` или удалён.

Карты в наборе — **стилизованные изображения**, сгенерированные для приложения
по бренд-цветам и иконкам. Это не официальные фотографии карточных продуктов.

## Банки и источники

| Банк | Сайт | Источник иконки / логотипа |
|---|---|---|
| Сбербанк | https://www.sberbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:Sberbank Logo 2020.svg |
| ВТБ | https://www.vtb.ru | favicon сайта; словоформа — Wikimedia Commons, File:VTB Logo 2018.svg |
| Газпромбанк | https://www.gazprombank.ru | favicon сайта; словоформа — Wikimedia, File:Gazprombank.svg |
| Альфа-Банк | https://alfabank.ru | favicon сайта; словоформа — Wikimedia Commons, File:Alfa Bank RU logo.svg |
| Россельхозбанк | https://www.rshb.ru | favicon сайта; словоформа — официальный сайт |
| Промсвязьбанк | https://www.psbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:PSB logo ru.png |
| Т-Банк | https://www.tbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:T-Bank RU logo.svg |
| МКБ | https://mkb.ru | favicon сайта; словоформа — Wikimedia, File:Логотип МКБ.svg |
| Банк ДОМ.РФ | https://domrfbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:Dom.rf Bank Logo.png |
| Совкомбанк | https://sovcombank.ru | favicon сайта; словоформа — Wikimedia Commons, File:New Sovcombank logo (updated version).svg |
| Райффайзен Банк | https://www.raiffeisen.ru | favicon сайта; словоформа — Wikimedia Commons, File:Raiffeisen Bank 2022 RU Logo.svg |
| НОВИКОМ | https://www.novikom.ru | favicon сайта |
| Банк Санкт-Петербург | https://www.bspb.ru | favicon сайта; словоформа — Wikimedia, File:Банк «Санкт-Петербург» логотип.png |
| Ак Барс Банк | https://www.akbars.ru | favicon сайта; словоформа — Wikimedia Commons, File:Ak Bars Bank Logo.svg |
| Уралсиб | https://uralsib.ru | favicon сайта; словоформа — Wikimedia, File:Банк Уралсиб логотип.png |
| ОТП Банк | https://www.otpbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:Otp bank Logo.svg |
| МТС Банк | https://www.mtsbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:MTSBank Logo 800px.png |
| Ozon Банк | https://ozonbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:Ozon Bank Logo.svg |
| Яндекс Банк | https://bank.yandex.ru | favicon сайта; словоформа — Wikimedia Commons, File:Yandex Bank logo.svg |
| Абсолют Банк | https://absolutbank.ru | favicon сайта; словоформа — Wikimedia Commons, File:Logo Absolut Bank.png |

## Состав первого набора

20 банков: крупнейшие и заметные розничные банки. Инфраструктурные НКО/НРД,
BM-Банк, а также уходящие с рынка Ситибанк и ЮниКредит в первый набор не
включены — их логотипы не нужны типовому семейному счёту. Набор пополняется
через MR; `id` существующих записей не меняются.

## Технические замечания

- Квадратные иконки — 256×256 PNG на белом фоне, с полями, пригодные для
  плитки 24–56 px.
- Карты — 256×256 PNG с прозрачным фоном, карта 236×148; в плитке показывается
  как миниатюра.
- Все PNG пересжаты и не содержат метаданных.
- Генератор: `resources/render_bank_pack.csx`.
