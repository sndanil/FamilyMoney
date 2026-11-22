#load "SvgPng.csx"

using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

// Скачивает свободные картинки OpenMoji и собирает наборы «Готовых» картинок.
// Источник: https://openmoji.org/ (CC BY-SA 4.0). Скрипт идемпотентен:
// повторный запуск обновляет PNG, pack.json и NOTICE-openmoji.md.
// Запуск из корня репозитория:
//   dotnet script resources/fetch_openmoji_packs.csx
//
// Нужны только NuGet-пакеты SkiaSharp и Svg.Skia (их подключает SvgPng.csx):
// Python, cairosvg и Pillow не требуются.

const string OpenMojiRev = "3e01de523ddefe9c5e46a7765b15efef04bf9002";
const string MetaUrl = $"https://raw.githubusercontent.com/hfg-gmuend/openmoji/{OpenMojiRev}/data/openmoji.json";
const string SvgUrl = "https://raw.githubusercontent.com/hfg-gmuend/openmoji/{0}/color/svg/{1}.svg";
const string SourceUrl = "https://github.com/hfg-gmuend/openmoji/blob/{0}/color/svg/{1}.svg";
const string LicenseUrl = "https://creativecommons.org/licenses/by-sa/4.0/";
const int PngSize = 256;
const int MaxPngBytes = 32 * 1024;

var root = Directory.GetCurrentDirectory();
var packsRoot = Path.Combine(root, "src", "FamilyMoney.Navigation", "Assets", "Packs");

Pack[] packs =
[
    new("business-finance", "Бизнес и финансы",
    [
        new("briefcase", "Портфель", "1F4BC", ["работа", "офис", "бизнес"]),
        new("chart-up", "Рост", "1F4C8", ["график", "инвестиции", "прибыль"]),
        new("analytics", "Аналитика", "1F4CA", ["график", "отчёт", "статистика"]),
        new("money-bag", "Деньги", "1F4B0", ["доход", "прибыль", "наличные"]),
        new("bank", "Банк", "1F3E6", ["счёт", "банки"]),
        new("office", "Офис", "1F3E2", ["здание", "работа", "компания"]),
        new("deal", "Сделка", "1F91D", ["партнёры", "договор", "рукопожатие"]),
        new("receipt", "Чек", "1F9FE", ["квитанция", "покупка", "расход"]),
        new("cash", "Валюта", "1F4B5", ["доллар", "наличные", "деньги"]),
        new("chart-down", "Расходы", "1F4C9", ["график", "убыток", "снижение"]),
        new("computer", "Компьютер", "1F5A5", ["работа", "техника", "ноутбук"]),
        new("idea", "Идея", "1F4A1", ["стартап", "мысль", "лампочка"]),
    ]),
    new("products", "Продукты",
    [
        new("apple", "Яблоко", "1F34E", ["фрукты", "еда"]),
        new("bread", "Хлеб", "1F35E", ["булочка", "еда", "выпечка"]),
        new("milk", "Молоко", "1F95B", ["напитки", "молочное"]),
        new("cheese", "Сыр", "1F9C0", ["молочное", "еда"]),
        new("egg", "Яйцо", "1F95A", ["завтрак", "еда"]),
        new("chicken", "Курица", "1F357", ["мясо", "птица", "еда"]),
        new("fish", "Рыба", "1F41F", ["морепродукты", "еда"]),
        new("broccoli", "Брокколи", "1F966", ["овощи", "еда"]),
        new("carrot", "Морковь", "1F955", ["овощи", "еда"]),
        new("banana", "Банан", "1F34C", ["фрукты", "еда"]),
        new("grapes", "Виноград", "1F347", ["фрукты", "еда"]),
        new("potato", "Картофель", "1F954", ["овощи", "еда"]),
        new("onion", "Лук", "1F9C5", ["овощи", "еда"]),
        new("tomato", "Помидор", "1F345", ["овощи", "еда"]),
    ]),
    new("home", "Дом и квартира",
    [
        new("house", "Дом", "1F3E0", ["квартира", "жильё"]),
        new("sofa", "Диван", "1F6CB", ["мебель", "гостиная"]),
        new("bed", "Кровать", "1F6CF", ["мебель", "спальня"]),
        new("shower", "Душ", "1F6BF", ["ванная", "сантехника"]),
        new("basket", "Корзина", "1F9FA", ["быт", "покупки"]),
        new("plant", "Растение", "1FAB4", ["цветы", "интерьер"]),
        new("candle", "Свеча", "1F56F", ["уют", "декор"]),
        new("picture", "Картина", "1F5BC", ["декор", "интерьер"]),
        new("door", "Дверь", "1F6AA", ["вход", "квартира"]),
        new("window", "Окно", "1FA9F", ["дом", "интерьер"]),
        new("cleaning", "Уборка", "1F9F9", ["быт", "чистота"]),
        new("sponge", "Губка", "1F9FD", ["уборка", "быт"]),
        new("chair", "Стул", "1FA91", ["мебель", "интерьер"]),
        new("bath", "Ванна", "1F6C1", ["ванная", "сантехника"]),
    ]),
    new("transport", "Машина и транспорт",
    [
        new("car", "Машина", "1F697", ["авто", "автомобиль"]),
        new("taxi", "Такси", "1F695", ["поездка", "машина"]),
        new("bus", "Автобус", "1F68C", ["транспорт", "поездка"]),
        new("train", "Поезд", "1F686", ["жд", "электричка"]),
        new("airplane", "Самолёт", "2708", ["авиа", "путешествие"]),
        new("fuel", "Заправка", "26FD", ["бензин", "топливо"]),
        new("bicycle", "Велосипед", "1F6B2", ["велик", "спорт"]),
        new("scooter", "Самокат", "1F6F5", ["транспорт", "город"]),
        new("truck", "Грузовик", "1F69A", ["доставка", "перевозка"]),
        new("ship", "Корабль", "1F6A2", ["море", "круиз"]),
        new("metro", "Метро", "1F687", ["транспорт", "город"]),
        new("wheel", "Колесо", "1F6DE", ["машина", "шины"]),
        new("traffic-light", "Светофор", "1F6A6", ["дорога", "город"]),
        new("map", "Карта", "1F5FA", ["навигация", "путь"]),
    ]),
    new("leisure", "Отдых",
    [
        new("beach", "Пляж", "1F3D6", ["отпуск", "море"]),
        new("party", "Праздник", "1F389", ["вечеринка", "событие"]),
        new("cinema", "Кино", "1F3AC", ["фильм", "развлечение"]),
        new("games", "Игры", "1F3AE", ["гейминг", "досуг"]),
        new("music", "Музыка", "1F3B5", ["концерт", "песня"]),
        new("camping", "Кемпинг", "1F3D5", ["поход", "палатка"]),
        new("fishing", "Рыбалка", "1F3A3", ["хобби", "отдых"]),
        new("sport", "Спорт", "26BD", ["футбол", "игра"]),
        new("popcorn", "Кинотеатр", "1F37F", ["фильм", "снек"]),
        new("tickets", "Билеты", "1F39F", ["событие", "театр"]),
        new("trip", "Поездка", "1F6EB", ["путешествие", "отпуск"]),
        new("island", "Отпуск", "1F3DD", ["море", "отдых"]),
        new("art", "Творчество", "1F3A8", ["хобби", "рисование"]),
    ]),
    new("animals", "Животные",
    [
        new("dog", "Собака", "1F436", ["питомец", "пёс"]),
        new("cat", "Кошка", "1F431", ["питомец", "кот"]),
        new("rabbit", "Кролик", "1F430", ["питомец", "заяц"]),
        new("fox", "Лиса", "1F98A", ["животное", "лес"]),
        new("bear", "Медведь", "1F43B", ["животное", "лес"]),
        new("panda", "Панда", "1F43C", ["животное", "зоопарк"]),
        new("koala", "Коала", "1F428", ["животное", "зоопарк"]),
        new("tiger", "Тигр", "1F42F", ["животное", "зоопарк"]),
        new("lion", "Лев", "1F981", ["животное", "зоопарк"]),
        new("cow", "Корова", "1F42E", ["животное", "ферма"]),
        new("pig", "Свинья", "1F437", ["животное", "ферма"]),
        new("frog", "Лягушка", "1F438", ["животное", "пруд"]),
        new("monkey", "Обезьяна", "1F435", ["животное", "зоопарк"]),
        new("chicken", "Курица", "1F414", ["птица", "ферма"]),
    ]),
    new("education", "Образование",
    [
        new("graduation", "Выпускной", "1F393", ["учёба", "диплом"]),
        new("books", "Книги", "1F4DA", ["учёба", "библиотека"]),
        new("pencil", "Карандаш", "270F", ["учёба", "письмо"]),
        new("school", "Школа", "1F3EB", ["учёба", "здание"]),
        new("notes", "Заметки", "1F4DD", ["письмо", "учёба"]),
        new("textbook", "Учебник", "1F4D6", ["книга", "учёба"]),
        new("microscope", "Микроскоп", "1F52C", ["наука", "биология"]),
        new("chemistry", "Химия", "1F9EA", ["наука", "опыт"]),
        new("geometry", "Геометрия", "1F4D0", ["учёба", "математика"]),
        new("pen", "Ручка", "1F58A", ["письмо", "учёба"]),
        new("backpack", "Рюкзак", "1F392", ["школа", "учёба"]),
        new("abacus", "Счёт", "1F9EE", ["математика", "учёба"]),
    ]),
    new("fashion", "Одежда и косметика",
    [
        new("tshirt", "Футболка", "1F455", ["одежда", "верх"]),
        new("jeans", "Джинсы", "1F456", ["одежда", "брюки"]),
        new("dress", "Платье", "1F457", ["одежда", "женское"]),
        new("heels", "Туфли", "1F460", ["обувь", "каблук"]),
        new("sneakers", "Кроссовки", "1F45F", ["обувь", "спорт"]),
        new("coat", "Пальто", "1F9E5", ["одежда", "верхняя"]),
        new("cap", "Кепка", "1F9E2", ["головной убор", "аксессуар"]),
        new("bag", "Сумка", "1F45C", ["аксессуар", "одежда"]),
        new("lipstick", "Помада", "1F484", ["косметика", "макияж"]),
        new("nails", "Маникюр", "1F485", ["косметика", "ногти"]),
        new("lotion", "Крем", "1F9F4", ["косметика", "уход"]),
        new("mirror", "Зеркало", "1FA9E", ["уход", "косметика"]),
        new("sunglasses", "Очки", "1F576", ["аксессуар", "лето"]),
        new("soap", "Мыло", "1F9FC", ["гигиена", "уход"]),
    ]),
    new("health", "Здоровье",
    [
        new("hospital", "Больница", "1F3E5", ["медицина", "врач"]),
        new("pill", "Таблетка", "1F48A", ["лекарство", "аптека"]),
        new("stethoscope", "Стетоскоп", "1FA7A", ["врач", "осмотр"]),
        new("tooth", "Зуб", "1F9B7", ["стоматология", "врач"]),
        new("brain", "Мозг", "1F9E0", ["здоровье", "мышление"]),
        new("heart", "Сердце", "2764", ["здоровье", "любовь"]),
        new("bandage", "Пластырь", "1FA79", ["травма", "аптека"]),
        new("dna", "ДНК", "1F9EC", ["анализы", "наука"]),
        new("syringe", "Укол", "1F489", ["прививка", "медицина"]),
        new("bone", "Кость", "1F9B4", ["скелет", "здоровье"]),
        new("glasses", "Очки", "1F453", ["зрение", "аксессуар"]),
        new("meditation", "Медитация", "1F9D8", ["спокойствие", "йога"]),
        new("run", "Бег", "1F3C3", ["спорт", "здоровье"]),
    ]),
];

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("FamilyMoney-pack-builder/1.0");

var metadata = await LoadMetadataAsync();
var errors = new List<string>();
var total = 0;

foreach (var pack in packs)
{
    var directory = Path.Combine(packsRoot, pack.Id);
    Directory.CreateDirectory(directory);
    var assets = new List<ManifestAsset>();
    var usedIds = new HashSet<string>(StringComparer.Ordinal);
    var hexcodes = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var item in pack.Assets)
    {
        hexcodes.TryAdd(item.Id, item.Hexcode);
    }

    foreach (var item in pack.Assets)
    {
        if (!usedIds.Add(item.Id))
        {
            errors.Add($"{pack.Id}: duplicate id {item.Id}");
            continue;
        }

        var meta = metadata.GetValueOrDefault(item.Hexcode.ToUpperInvariant(), Meta.Empty);
        var sourceUrl = string.Format(SourceUrl, OpenMojiRev, item.Hexcode);
        var svgUrl = string.Format(SvgUrl, OpenMojiRev, item.Hexcode);
        var destination = Path.Combine(directory, item.Id + ".png");

        byte[] svg;
        try
        {
            svg = await http.GetByteArrayAsync(svgUrl);
        }
        catch (HttpRequestException error)
        {
            errors.Add(error.StatusCode is { } status
                ? $"{pack.Id}/{item.Id}: HTTP {(int)status} for {item.Hexcode}"
                : $"{pack.Id}/{item.Id}: download failed: {error.Message}");
            continue;
        }

        byte[] png;
        try
        {
            using var bitmap = RenderSvg(new MemoryStream(svg), PngSize, PngSize);
            png = EncodePng(bitmap);
        }
        catch (Exception error)
        {
            errors.Add($"{pack.Id}/{item.Id}: render failed: {error.Message}");
            continue;
        }

        if (png.Length > MaxPngBytes)
        {
            errors.Add($"{pack.Id}/{item.Id}: PNG {png.Length} bytes > 32 KB");
            continue;
        }

        await File.WriteAllBytesAsync(destination, png);

        assets.Add(new ManifestAsset(
            item.Id,
            item.Name,
            SearchAliases(item.Aliases, meta),
            Path.GetFileName(destination),
            "#FFFFFF",
            "illustration",
            sourceUrl,
            "1.1.0"));
        total++;
    }

    if (assets.Count < 10)
    {
        errors.Add($"{pack.Id}: only {assets.Count} assets");
    }

    var manifest = new PackManifest(pack.Id, pack.Title, 1, "NOTICE-openmoji.md", assets.ToArray());
    await File.WriteAllTextAsync(
        Path.Combine(directory, "pack.json"),
        JsonSerializer.Serialize(manifest, jsonOptions) + "\r\n",
        utf8NoBom);

    var notice = new List<string>
    {
        $"# {pack.Title} — OpenMoji",
        "",
        $"Картинки: [OpenMoji](https://openmoji.org/), лицензия [CC BY-SA 4.0]({LicenseUrl}).",
        "",
        "SVG из OpenMoji отрендерены в PNG 256×256 с прозрачным фоном; графика не изменялась. Атрибуция по каждому файлу:",
        "",
        "| Файл | Emoji | Автор OpenMoji |",
        "|---|---|---|",
    };
    foreach (var asset in assets)
    {
        var meta = metadata.GetValueOrDefault(hexcodes[asset.Id].ToUpperInvariant(), Meta.Empty);
        notice.Add($"| {asset.File} | {meta.Emoji} | {meta.Author} |");
    }

    notice.Add("");
    notice.Add("Если вы правообладатель и хотите изменить или убрать изображение — откройте issue в FamilyMoney.");
    await File.WriteAllTextAsync(
        Path.Combine(directory, "NOTICE-openmoji.md"),
        string.Join("\r\n", notice) + "\r\n",
        utf8NoBom);
}

if (errors.Count > 0)
{
    Console.Error.WriteLine("Ошибки:");
    foreach (var error in errors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    Console.Error.WriteLine($"Собрано ассетов: {total}");
    Environment.Exit(1);
}

Console.WriteLine($"OK: {packs.Length} групп, {total} PNG");
http.Dispose();

async Task<Dictionary<string, Meta>> LoadMetadataAsync()
{
    var payload = await http.GetStringAsync(MetaUrl);
    using var document = JsonDocument.Parse(payload);
    var result = new Dictionary<string, Meta>(StringComparer.Ordinal);
    foreach (var item in document.RootElement.EnumerateArray())
    {
        var hexcode = ReadString(item, "hexcode");
        if (string.IsNullOrEmpty(hexcode))
        {
            continue;
        }

        var author = item.TryGetProperty("openmoji_author", out _)
            ? ReadString(item, "openmoji_author")
            : "OpenMoji contributors";
        result[hexcode.ToUpperInvariant()] = new Meta(
            ReadString(item, "annotation"),
            ReadString(item, "tags"),
            ReadString(item, "emoji"),
            author);
    }

    return result;
}

static string ReadString(JsonElement element, string name)
{
    return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? ""
        : "";
}

static string[] SearchAliases(string[] ownAliases, Meta meta)
{
    var tags = meta.Tags.Split(',', StringSplitOptions.TrimEntries)
        .Where(tag => tag.Length > 0)
        .Take(4);
    var result = new List<string>();
    foreach (var alias in ownAliases.Concat(new[] { meta.Annotation }).Concat(tags))
    {
        if (!result.Contains(alias, StringComparer.Ordinal))
        {
            result.Add(alias);
        }
    }

    return result.ToArray();
}

sealed record Pack(string Id, string Title, Asset[] Assets);

sealed record Asset(string Id, string Name, string Hexcode, string[] Aliases);

sealed record Meta(string Annotation, string Tags, string Emoji, string Author)
{
    public static Meta Empty { get; } = new("", "", "", "OpenMoji contributors");
}

sealed record ManifestAsset(
    string Id,
    string Name,
    string[] Aliases,
    string File,
    string Background,
    string Kind,
    string Source,
    string AddedIn);

sealed record PackManifest(
    string Id,
    string Title,
    int Version,
    string Notice,
    ManifestAsset[] Assets);
