using System;
using System.Collections.Generic;
using Material.Icons;

namespace FamilyMoney.Utils;

/// <summary>
/// Простые эвристики «название → иконка». Без сети и без ML: словарь основ,
/// самое длинное совпадение. Возвращает spec-строку, а не MaterialIconKind,
/// потому что подсказка может указывать на готовую картинку из набора.
/// </summary>
public static class IconSuggester
{
    private static readonly (string[] Keywords, string Spec)[] Rules =
    [
        (["продукт", "еда", "магазин", "супермаркет", "пятерочка", "магнит"], M(MaterialIconKind.CartOutline)),
        (["кафе", "ресторан", "обед", "кофе", "кофейня"], M(MaterialIconKind.SilverwareForkKnife)),
        (["бензин", "заправка", "топливо", "азс"], M(MaterialIconKind.GasStation)),
        (["машина", "авто", "автомобиль"], M(MaterialIconKind.Car)),
        (["такси"], M(MaterialIconKind.Taxi)),
        (["транспорт", "автобус", "метро", "электричка"], M(MaterialIconKind.Bus)),
        (["поезд", "жд", "железная дорога"], M(MaterialIconKind.Train)),
        (["дом", "квартира", "аренда", "жкх", "ипотека"], M(MaterialIconKind.HomeOutline)),
        (["ремонт", "инструмент", "стройка"], M(MaterialIconKind.HammerWrench)),
        (["мебель", "диван"], M(MaterialIconKind.SofaOutline)),
        (["аптека", "лекарств", "таблетк"], M(MaterialIconKind.LocalPharmacy)),
        (["врач", "здоров", "больниц", "клиник", "медицин"], M(MaterialIconKind.MedicalBag)),
        (["ребен", "ребён", "дети", "детск", "садик"], M(MaterialIconKind.BabyFaceOutline)),
        (["одежда", "футболк", "куртк"], M(MaterialIconKind.TshirtCrewOutline)),
        (["обувь", "кроссовк", "ботинк"], M(MaterialIconKind.ShoeSneaker)),
        (["кино", "фильм", "развлечен", "театр"], M(MaterialIconKind.MovieOpen)),
        (["игр", "game", "playstation", "xbox"], M(MaterialIconKind.GamepadVariant)),
        (["зарплат", "доход", "поступлен", "аванс"], M(MaterialIconKind.CashMultiple)),
        (["наличн", "кэш", "cash"], M(MaterialIconKind.WalletOutline)),
        (["карта", "карточн"], M(MaterialIconKind.CreditCardOutline)),
        (["банк", "счет", "счёт"], M(MaterialIconKind.BankOutline)),
        (["вклад", "депозит", "сбережен", "копилк"], M(MaterialIconKind.PiggyBankOutline)),
        (["кредит", "займ", "долг"], M(MaterialIconKind.HandCoinOutline)),
        (["инвестиц", "брокер", "акци", "биржа"], M(MaterialIconKind.ChartLine)),
        (["процент", "ставка"], M(MaterialIconKind.Percent)),
        (["перевод", "перевел", "перевёл"], M(MaterialIconKind.SwapHorizontal)),
        (["интернет", "связь", "вайфай", "wifi"], M(MaterialIconKind.Wifi)),
        (["телефон", "мобильн", "сотов"], M(MaterialIconKind.Cellphone)),
        (["подписк", "подписк", "subscription"], M(MaterialIconKind.Autorenew)),
        (["путешеств", "отпуск", "поездк", "самолет", "самолёт"], M(MaterialIconKind.Airplane)),
        (["подар", "подарк"], M(MaterialIconKind.GiftOutline)),
        (["животн", "питом", "кошк", "собак", "ветеринар"], M(MaterialIconKind.PawOutline)),
        (["налог", "документ", "страхов"], M(MaterialIconKind.FileDocumentOutline)),
        (["штраф", "суд", "госпошлин"], M(MaterialIconKind.Gavel)),
        (["образован", "школ", "курс", "учеб"], M(MaterialIconKind.SchoolOutline)),
        (["страхов", "защит"], M(MaterialIconKind.ShieldCheckOutline)),
        (["красот", "салон", "космет", "уход"], M(MaterialIconKind.FaceWomanShimmer)),
        (["спорт", "фитнес", "тренажер", "тренажёр"], M(MaterialIconKind.Dumbbell)),
        (["свет", "электрич", "энерг"], M(MaterialIconKind.FlashOutline)),
        (["вода", "водоснабжен", "сантехник"], M(MaterialIconKind.WaterOutline)),
        (["работа", "офис", "бизнес"], M(MaterialIconKind.BriefcaseOutline)),
        (["сбер", "сбербанк", "sber"], P("sber")),
        (["тинькофф", "т-банк", "тбанк", "tbank", "tinkoff"], P("tbank")),
        (["альфа", "альфа-банк", "alfa"], P("alfa")),
        (["втб", "vtb"], P("vtb")),
        (["газпромбанк", "гпб"], P("gazprombank")),
        (["россельхозбанк", "рсхб", "rshb"], P("rshb")),
        (["промсвязьбанк", "псб", "psb"], P("psb")),
        (["мкб", "московский кредитный"], P("mkb")),
        (["дом.рф", "дом рф", "domrf"], P("domrf")),
        (["совкомбанк", "sovcombank"], P("sovcombank")),
        (["райффайзен", "raiffeisen"], P("raiffeisen")),
        (["новиком", "novikom"], P("novikom")),
        (["санкт-петербург", "бспб", "bspb"], P("bspb")),
        (["ак барс", "акбарс", "akbars"], P("akbars")),
        (["уралсиб", "uralsib"], P("uralsib")),
        (["отп", "otp"], P("otp")),
        (["мтс", "mts"], P("mts")),
        (["ozon", "озон"], P("ozon")),
        (["яндекс", "yandex"], P("yandex")),
        (["абсолют", "absolut"], P("absolut")),
    ];

    public static string? Suggest(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = Normalize(name);
        string? best = null;
        var bestLength = 0;

        foreach (var (keywords, spec) in Rules)
        {
            foreach (var keyword in keywords)
            {
                if (keyword.Length < bestLength)
                {
                    continue;
                }

                if (normalized.Contains(keyword, StringComparison.Ordinal))
                {
                    best = spec;
                    bestLength = keyword.Length;
                }
            }
        }

        return best;
    }

    private static string M(MaterialIconKind kind) => IconSpec.ForMaterial(kind);

    private static string P(string assetId) => IconSpec.ForPack("banks", assetId);

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant().Replace('ё', 'е');
    }
}
