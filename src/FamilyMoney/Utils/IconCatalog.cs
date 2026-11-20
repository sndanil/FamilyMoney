using System;
using System.Collections.Generic;
using System.Linq;
using Material.Icons;

namespace FamilyMoney.Utils;

public sealed record IconChoice(string Spec, string Label, string[] Keywords);

public sealed record IconGroup(string Title, IReadOnlyList<IconChoice> Items);

/// <summary>
/// Курируемый каталог иконок. Полные 13 645 имён Material остаются доступны
/// через поиск в пикере, но по умолчанию показываем понятный русский набор.
/// </summary>
public static class IconCatalog
{
    public static IReadOnlyList<IconGroup> Groups { get; } = Build();

    public static IReadOnlyList<IconChoice> All { get; } = Groups.SelectMany(group => group.Items).ToList();

    public static IEnumerable<IconChoice> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return All;
        }

        var text = Normalize(query);
        return All.Where(item =>
            Normalize(item.Label).Contains(text, StringComparison.Ordinal)
            || item.Keywords.Any(keyword => Normalize(keyword).Contains(text, StringComparison.Ordinal))
            || Normalize(item.Spec).Contains(text, StringComparison.Ordinal));
    }

    private static IReadOnlyList<IconGroup> Build()
    {
        return
        [
            new("Деньги",
            [
                I(MaterialIconKind.CashMultiple, "Доход", "зарплата", "поступление"),
                I(MaterialIconKind.WalletOutline, "Кошелёк", "наличные", "кэш"),
                I(MaterialIconKind.CreditCardOutline, "Карта", "банковская карта"),
                I(MaterialIconKind.BankOutline, "Банк", "счёт", "счет"),
                I(MaterialIconKind.PiggyBankOutline, "Копилка", "вклад", "сбережения"),
                I(MaterialIconKind.HandCoinOutline, "Долг", "кредит", "займ"),
                I(MaterialIconKind.ChartLine, "Инвестиции", "график", "акции"),
                I(MaterialIconKind.Percent, "Проценты", "ставка"),
                I(MaterialIconKind.SwapHorizontal, "Перевод", "переводы"),
                I(MaterialIconKind.ReceiptTextOutline, "Чек", "квитанция", "расход"),
                I(MaterialIconKind.Atm, "Банкомат", "снятие"),
                I(MaterialIconKind.CurrencyUsd, "Валюта", "доллар", "евро"),
            ]),
            new("Дом",
            [
                I(MaterialIconKind.HomeOutline, "Дом", "квартира", "аренда", "жкх"),
                I(MaterialIconKind.HomeCityOutline, "Город", "недвижимость"),
                I(MaterialIconKind.SofaOutline, "Мебель", "диван", "интерьер"),
                I(MaterialIconKind.HammerWrench, "Ремонт", "инструменты", "стройка"),
                I(MaterialIconKind.FlashOutline, "Электричество", "свет", "энергия"),
                I(MaterialIconKind.WaterOutline, "Вода", "сантехника", "водоснабжение"),
                I(MaterialIconKind.TrashCanOutline, "Мусор", "вывоз", "коммунальные"),
                I(MaterialIconKind.KeyOutline, "Ключи", "замок", "доступ"),
                I(MaterialIconKind.Wrench, "Сантехника", "мастер"),
                I(MaterialIconKind.SprayBottle, "Уборка", "бытовая химия"),
            ]),
            new("Транспорт",
            [
                I(MaterialIconKind.Car, "Машина", "авто", "автомобиль"),
                I(MaterialIconKind.Taxi, "Такси", "поездка"),
                I(MaterialIconKind.Bus, "Автобус", "транспорт", "метро"),
                I(MaterialIconKind.Train, "Поезд", "жд", "электричка"),
                I(MaterialIconKind.GasStation, "Заправка", "бензин", "топливо"),
                I(MaterialIconKind.Airplane, "Самолёт", "путешествие", "отпуск"),
                I(MaterialIconKind.Bicycle, "Велосипед", "спорт"),
                I(MaterialIconKind.Parking, "Парковка", "стоянка"),
                I(MaterialIconKind.CarWash, "Мойка", "автомобиль"),
            ]),
            new("Еда",
            [
                I(MaterialIconKind.CartOutline, "Корзина", "продукты", "магазин", "супермаркет"),
                I(MaterialIconKind.BasketOutline, "Продукты", "еда", "покупки"),
                I(MaterialIconKind.SilverwareForkKnife, "Ресторан", "кафе", "обед"),
                I(MaterialIconKind.CoffeeOutline, "Кофе", "кофейня", "напиток"),
                I(MaterialIconKind.FoodApple, "Фрукты", "еда", "здоровое питание"),
                I(MaterialIconKind.BeerOutline, "Пиво", "алкоголь", "бар"),
                I(MaterialIconKind.GlassMugVariant, "Напитки", "бар", "кружка"),
            ]),
            new("Здоровье",
            [
                I(MaterialIconKind.MedicalBag, "Медицина", "врач", "здоровье", "клиника"),
                I(MaterialIconKind.HospitalBuilding, "Больница", "стационар", "клиника"),
                I(MaterialIconKind.LocalPharmacy, "Аптека", "лекарства"),
                I(MaterialIconKind.Pill, "Таблетки", "лекарства"),
                I(MaterialIconKind.ToothOutline, "Стоматология", "зубы"),
                I(MaterialIconKind.Dumbbell, "Спорт", "фитнес", "тренажёр"),
                I(MaterialIconKind.HeartOutline, "Здоровье", "сердце"),
                I(MaterialIconKind.Glasses, "Очки", "зрение"),
            ]),
            new("Семья",
            [
                I(MaterialIconKind.BabyFaceOutline, "Дети", "ребёнок", "садик"),
                I(MaterialIconKind.AccountGroup, "Семья", "люди", "группа"),
                I(MaterialIconKind.PawOutline, "Питомцы", "животные", "кошка", "собака"),
                I(MaterialIconKind.GiftOutline, "Подарки", "праздник"),
                I(MaterialIconKind.SchoolOutline, "Образование", "школа", "курсы"),
                I(MaterialIconKind.HandHeart, "Помощь", "благотворительность"),
            ]),
            new("Досуг",
            [
                I(MaterialIconKind.MovieOpen, "Кино", "фильм", "развлечения"),
                I(MaterialIconKind.GamepadVariant, "Игры", "game", "playstation"),
                I(MaterialIconKind.MusicNote, "Музыка", "концерт", "подписка"),
                I(MaterialIconKind.TicketOutline, "Билеты", "театр", "событие"),
                I(MaterialIconKind.PartyPopper, "Праздник", "вечеринка"),
                I(MaterialIconKind.Flower, "Цветы", "подарок"),
                I(MaterialIconKind.Tree, "Природа", "дача", "сад"),
                I(MaterialIconKind.BookOpenVariant, "Книги", "чтение", "обучение"),
            ]),
            new("Работа",
            [
                I(MaterialIconKind.BriefcaseOutline, "Работа", "офис", "бизнес"),
                I(MaterialIconKind.Laptop, "Техника", "компьютер", "ноутбук"),
                I(MaterialIconKind.Cellphone, "Телефон", "мобильная связь"),
                I(MaterialIconKind.Wifi, "Интернет", "связь", "вайфай"),
                I(MaterialIconKind.Phone, "Звонки", "связь"),
                I(MaterialIconKind.EmailOutline, "Почта", "email"),
                I(MaterialIconKind.CalendarMonth, "Календарь", "планирование"),
                I(MaterialIconKind.FileDocumentOutline, "Документы", "налоги", "бумаги"),
                I(MaterialIconKind.Gavel, "Штрафы", "суд", "госпошлина"),
            ]),
            new("Разное",
            [
                I(MaterialIconKind.ShieldCheckOutline, "Страхование", "защита"),
                I(MaterialIconKind.FaceWomanShimmer, "Красота", "салон", "косметика"),
                I(MaterialIconKind.Hanger, "Одежда", "гардероб"),
                I(MaterialIconKind.TshirtCrewOutline, "Футболка", "одежда"),
                I(MaterialIconKind.ShoeSneaker, "Обувь", "кроссовки"),
                I(MaterialIconKind.Scissors, "Стрижка", "салон"),
                I(MaterialIconKind.StarOutline, "Избранное", "звезда"),
                I(MaterialIconKind.Sparkles, "Прочее", "другое", "разное"),
                I(MaterialIconKind.TagOutline, "Категория", "тег"),
            ]),
        ];
    }

    private static IconChoice I(MaterialIconKind kind, string label, params string[] keywords)
    {
        return new IconChoice(IconSpec.ForMaterial(kind), label, keywords);
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace('ё', 'е');
}
