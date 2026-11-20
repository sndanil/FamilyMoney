#nullable enable
#r "nuget: SkiaSharp, 3.119.4"
#r "nuget: Svg.Skia, 3.0.4"

// Собирает банковский набор: квадратные иконки, стилизованные карты,
// pack.json и контактные листы для проверки.
// Запуск из корня репозитория:
//   dotnet script resources/render_bank_pack.csx

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SkiaSharp;
using Svg.Skia;

var root = Directory.GetCurrentDirectory();
var sourceDir = Path.Combine(root, "resources", "banks", "src");
var packDir = Path.Combine(root, "src", "FamilyMoney.Navigation", "Assets", "Packs", "banks");
Directory.CreateDirectory(packDir);

var banks = new Bank[]
{
    new("sber", "Сбербанк", "Сбер", new[] { "сбер", "sber", "сбербанк" }, Path.Combine(sourceDir, "gicons", "sberbank.ru.png"), new SKColor(0x21, 0xA0, 0x38), false, "https://www.sberbank.ru"),
    new("vtb", "ВТБ", "ВТБ", new[] { "втб", "vtb" }, Path.Combine(sourceDir, "gicons", "www_vtb_ru.png"), new SKColor(0x0A, 0x28, 0x96), false, "https://www.vtb.ru"),
    new("gazprombank", "Газпромбанк", "Газпромбанк", new[] { "газпромбанк", "гпб", "gazprombank" }, Path.Combine(sourceDir, "gicons", "gazprombank.ru.png"), new SKColor(0x0F, 0x4C, 0x9C), false, "https://www.gazprombank.ru"),
    new("alfa", "Альфа-Банк", "Альфа-Банк", new[] { "альфа", "альфа-банк", "alfa" }, Path.Combine(sourceDir, "gicons", "alfabank.ru.png"), new SKColor(0xEF, 0x31, 0x24), false, "https://alfabank.ru"),
    new("rshb", "Россельхозбанк", "РСХБ", new[] { "россельхозбанк", "рсхб", "rshb" }, Path.Combine(sourceDir, "gicons", "rshb.ru.png"), new SKColor(0x00, 0x84, 0x3D), false, "https://www.rshb.ru"),
    new("psb", "Промсвязьбанк", "ПСБ", new[] { "промсвязьбанк", "псб", "psb" }, Path.Combine(sourceDir, "gicons", "psbank.ru.png"), new SKColor(0x1F, 0x5A, 0xA6), false, "https://www.psbank.ru"),
    new("tbank", "Т-Банк", "Т-Банк", new[] { "тинькофф", "т-банк", "tbank", "tinkoff" }, Path.Combine(sourceDir, "gicons", "tbank.ru.png"), new SKColor(0xFF, 0xDD, 0x2D), true, "https://www.tbank.ru"),
    new("mkb", "МКБ", "МКБ", new[] { "мкб", "московский кредитный банк", "mkb" }, Path.Combine(sourceDir, "gicons", "mkb.ru.png"), new SKColor(0xE3, 0x1E, 0x24), false, "https://mkb.ru"),
    new("domrf", "Банк ДОМ.РФ", "ДОМ.РФ", new[] { "дом.рф", "дом рф", "domrf" }, Path.Combine(sourceDir, "gicons", "domrfbank.ru.png"), new SKColor(0x00, 0x5C, 0xA9), false, "https://domrfbank.ru"),
    new("sovcombank", "Совкомбанк", "Совкомбанк", new[] { "совкомбанк", "sovcombank" }, Path.Combine(sourceDir, "gicons", "sovcombank.ru.png"), new SKColor(0xC8, 0x10, 0x2E), false, "https://sovcombank.ru"),
    new("raiffeisen", "Райффайзен Банк", "Райффайзен", new[] { "райффайзен", "райф", "raiffeisen" }, Path.Combine(sourceDir, "gicons", "raiffeisen.ru.png"), new SKColor(0xFE, 0xE6, 0x00), true, "https://www.raiffeisen.ru"),
    new("novikom", "НОВИКОМ", "НОВИКОМ", new[] { "новиком", "новикомбанк", "novikom" }, Path.Combine(sourceDir, "icons", "novikom.svg"), new SKColor(0x6A, 0x2C, 0x91), false, "https://www.novikom.ru"),
    new("bspb", "Банк Санкт-Петербург", "БСПБ", new[] { "санкт-петербург", "бспб", "bspb" }, Path.Combine(sourceDir, "gicons", "bspb.ru.png"), new SKColor(0x00, 0x3C, 0x71), false, "https://www.bspb.ru"),
    new("akbars", "Ак Барс Банк", "Ак Барс", new[] { "ак барс", "акбарс", "akbars" }, Path.Combine(sourceDir, "gicons", "akbars.ru.png"), new SKColor(0x00, 0x8C, 0x45), false, "https://www.akbars.ru"),
    new("uralsib", "Уралсиб", "Уралсиб", new[] { "уралсиб", "uralsib" }, Path.Combine(sourceDir, "gicons", "uralsib.ru.png"), new SKColor(0x5B, 0x2C, 0x86), false, "https://uralsib.ru"),
    new("otp", "ОТП Банк", "ОТП", new[] { "отп", "otp" }, Path.Combine(sourceDir, "gicons", "otpbank.ru.png"), new SKColor(0x00, 0xA6, 0x51), false, "https://www.otpbank.ru"),
    new("mts", "МТС Банк", "МТС", new[] { "мтс", "mts" }, Path.Combine(sourceDir, "gicons", "mtsbank.ru.png"), new SKColor(0xE3, 0x06, 0x11), false, "https://www.mtsbank.ru"),
    new("ozon", "Ozon Банк", "Ozon", new[] { "озон", "ozon" }, Path.Combine(sourceDir, "gicons", "bank_ozon_ru.png"), new SKColor(0x00, 0x5B, 0xFF), false, "https://ozonbank.ru"),
    new("yandex", "Яндекс Банк", "Яндекс", new[] { "яндекс", "yandex" }, Path.Combine(sourceDir, "gicons", "bank.yandex.ru.png"), new SKColor(0xFC, 0x3F, 0x1D), false, "https://bank.yandex.ru"),
    new("absolut", "Абсолют Банк", "Абсолют", new[] { "абсолют", "absolut" }, Path.Combine(sourceDir, "gicons", "absolutbank.ru.png"), new SKColor(0xF2, 0x65, 0x22), false, "https://absolutbank.ru"),
};

var assets = new List<PackAsset>();
var iconBitmaps = new Dictionary<string, SKBitmap>(StringComparer.Ordinal);

foreach (var bank in banks)
{
    var iconSource = LoadBitmap(bank.IconSource, 512)
        ?? throw new InvalidOperationException($"Не удалось прочитать иконку {bank.Id}: {bank.IconSource}");
    iconBitmaps[bank.Id] = iconSource;

    var iconPng = Path.Combine(packDir, $"{bank.Id}.png");
    using (var icon = MakeIcon(iconSource))
    using (var image = SKImage.FromBitmap(icon))
    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
    using (var file = File.Create(iconPng))
    {
        data.SaveTo(file);
    }

    var cardPng = Path.Combine(packDir, $"{bank.Id}-card.png");
    using (var card = MakeCard(bank, iconSource))
    using (var image = SKImage.FromBitmap(card))
    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
    using (var file = File.Create(cardPng))
    {
        data.SaveTo(file);
    }

    assets.Add(new PackAsset
    {
        Id = bank.Id,
        Name = bank.Name,
        Aliases = bank.Aliases,
        File = $"{bank.Id}.png",
        Background = "#FFFFFF",
        Kind = "logo",
        AddedIn = "1.0.0",
    });

    assets.Add(new PackAsset
    {
        Id = $"{bank.Id}-card",
        Name = $"{bank.Name} — карта",
        Aliases = [.. bank.Aliases, $"{bank.Aliases[0]} карта", $"{bank.Id} card"],
        File = $"{bank.Id}-card.png",
        Background = "#FFFFFF",
        Kind = "card",
        AddedIn = "1.0.0",
    });
}

var manifest = new PackManifest
{
    Id = "banks",
    Title = "Банки",
    Version = 1,
    Notice = "NOTICE-banks.md",
    Assets = assets,
};

var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
});
File.WriteAllText(Path.Combine(packDir, "pack.json"), json + Environment.NewLine);

WriteContactSheet(Path.Combine(root, "docs", "mockups", "banks-icons.png"), banks, iconBitmaps, false);
WriteContactSheet(Path.Combine(root, "docs", "mockups", "banks-cards.png"), banks, iconBitmaps, true);

foreach (var bitmap in iconBitmaps.Values)
{
    bitmap.Dispose();
}

Console.WriteLine($"Saved {assets.Count} assets to {packDir}");

static SKBitmap? LoadBitmap(string path, int maxSize)
{
    if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
    {
        using var svg = new SKSvg();
        var picture = svg.Load(path);
        if (picture == null)
        {
            return null;
        }

        var bounds = picture.CullRect;
        var scale = Math.Min(maxSize / bounds.Width, maxSize / bounds.Height);
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
        var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
        }

        return bitmap;
    }

    var decoded = SKBitmap.Decode(path);
    if (decoded == null)
    {
        return null;
    }

    if (decoded.Width <= maxSize && decoded.Height <= maxSize)
    {
        return decoded;
    }

    var ratio = Math.Min(maxSize / (float)decoded.Width, maxSize / (float)decoded.Height);
    var resized = new SKBitmap(Math.Max(1, (int)(decoded.Width * ratio)), Math.Max(1, (int)(decoded.Height * ratio)));
    using (var canvas = new SKCanvas(resized))
    {
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(decoded, SKRect.Create(0, 0, resized.Width, resized.Height));
    }

    decoded.Dispose();
    return resized;
}

static SKBitmap MakeIcon(SKBitmap source)
{
    var bitmap = new SKBitmap(256, 256);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.White);

    var target = SKRect.Create(28, 28, 200, 200);
    var scale = Math.Min(target.Width / source.Width, target.Height / source.Height);
    var width = source.Width * scale;
    var height = source.Height * scale;
    var dest = SKRect.Create(target.Left + (target.Width - width) / 2f, target.Top + (target.Height - height) / 2f, width, height);

    using (var paint = new SKPaint { IsAntialias = true })
    {
        var roundRect = new SKRoundRect(dest, Math.Min(dest.Width, dest.Height) * 0.12f);
        canvas.ClipRoundRect(roundRect, antialias: true);
        canvas.DrawBitmap(source, dest, paint);
    }

    return bitmap;
}

static SKBitmap MakeCard(Bank bank, SKBitmap source)
{
    var bitmap = new SKBitmap(256, 256);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);

    var rect = SKRect.Create(10, 54, 236, 148);
    var round = new SKRoundRect(rect, 15);

    // Мягкая тень под картой.
    using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 42), IsAntialias = true })
    {
        canvas.DrawRoundRect(SKRect.Create(rect.Left, rect.Top + 5, rect.Width, rect.Height), 15, 15, shadow);
    }

    var lighter = Lighten(bank.Color, 0.14f);
    var darker = Darken(bank.Color, 0.28f);
    using (var paint = new SKPaint { IsAntialias = true })
    {
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Right, rect.Bottom),
            [lighter, bank.Color, darker],
            [0f, 0.55f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(round, paint);
    }

    canvas.Save();
    canvas.ClipRoundRect(round, antialias: true);

    // Лёгкий фирменный узор.
    using (var pattern = new SKPaint { Color = new SKColor(255, 255, 255, 26), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 18 })
    {
        canvas.DrawCircle(rect.Right - 30, rect.Top + 20, 70, pattern);
        canvas.DrawCircle(rect.Right + 20, rect.Bottom + 10, 90, pattern);
    }

    var textColor = bank.DarkText ? new SKColor(0x14, 0x20, 0x2B) : SKColors.White;

    // Белая плашка с квадратной иконкой банка.
    using (var plate = new SKPaint { Color = SKColors.White, IsAntialias = true })
    {
        canvas.DrawRoundRect(SKRect.Create(26, 70, 48, 48), 12, 12, plate);
    }

    var iconTarget = SKRect.Create(32, 76, 36, 36);
    DrawFitted(canvas, source, iconTarget);

    // Название банка.
    using (var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)!, 11))
    using (var text = new SKPaint { Color = textColor, IsAntialias = true })
    {
        canvas.DrawText(bank.ShortName, 84, 94, font, text);
        using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)!, 8);
        canvas.DrawText("банковская карта", 84, 108, fontSmall, text);
    }

    // Чип.
    using (var chip = new SKPaint { IsAntialias = true })
    {
        chip.Shader = SKShader.CreateLinearGradient(
            new SKPoint(26, 126), new SKPoint(52, 146),
            [new SKColor(0xF3, 0xD9, 0x8B), new SKColor(0xC9, 0xA2, 0x4A)],
            null,
            SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(SKRect.Create(26, 126, 26, 19), 4, 4, chip);
    }

    using (var chipLine = new SKPaint { Color = new SKColor(120, 88, 30, 120), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
    {
        canvas.DrawLine(26, 135.5f, 52, 135.5f, chipLine);
        canvas.DrawLine(39, 126, 39, 145, chipLine);
    }

    // Бесконтактная оплата.
    using (var waves = new SKPaint { Color = WithAlpha(textColor, 190), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f })
    {
        canvas.DrawArc(SKRect.Create(60, 128, 10, 14), -55, 110, false, waves);
        canvas.DrawArc(SKRect.Create(64, 125, 15, 20), -55, 110, false, waves);
        canvas.DrawArc(SKRect.Create(68, 122, 20, 26), -55, 110, false, waves);
    }

    // Номер и подпись.
    using (var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)!, 12))
    using (var text = new SKPaint { Color = textColor, IsAntialias = true })
    {
        canvas.DrawText("••••  1234", 26, 172, font, text);
    }

    using (var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)!, 9))
    using (var text = new SKPaint { Color = WithAlpha(textColor, 170), IsAntialias = true })
    {
        canvas.DrawText("МИР", 230, 190, SKTextAlign.Right, font, text);
    }

    canvas.Restore();
    return bitmap;
}

static void WriteContactSheet(string path, Bank[] banks, Dictionary<string, SKBitmap> icons, bool cards)
{
    const float cell = 240;
    const int cols = 5;
    var rows = (banks.Length + cols - 1) / cols;
    var sheet = new SKBitmap((int)(cols * cell), (int)(rows * cell));
    using (var canvas = new SKCanvas(sheet))
    {
        canvas.Clear(new SKColor(0x17, 0x21, 0x2B));
        using var labelPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var labelFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)!, 15);

        for (var i = 0; i < banks.Length; i++)
        {
            var bank = banks[i];
            var col = i % cols;
            var row = i / cols;
            var x = col * cell;
            var y = row * cell;

            if (cards)
            {
                using var card = MakeCard(bank, icons[bank.Id]);
                var scale = 200f / card.Width;
                canvas.DrawBitmap(card, SKRect.Create(x + 20, y + 14, card.Width * scale, card.Height * scale));
            }
            else
            {
                using var icon = MakeIcon(icons[bank.Id]);
                canvas.DrawBitmap(icon, SKRect.Create(x + 20, y + 12, 200, 200));
            }

            canvas.DrawText(bank.Name, x + 120, y + 232, SKTextAlign.Center, labelFont, labelPaint);
        }
    }

    using var image = SKImage.FromBitmap(sheet);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var file = File.Create(path);
    data.SaveTo(file);
    sheet.Dispose();
}

static void DrawFitted(SKCanvas canvas, SKBitmap source, SKRect target)
{
    var scale = Math.Min(target.Width / source.Width, target.Height / source.Height);
    var width = source.Width * scale;
    var height = source.Height * scale;
    var dest = SKRect.Create(target.Left + (target.Width - width) / 2f, target.Top + (target.Height - height) / 2f, width, height);
    using var paint = new SKPaint { IsAntialias = true };
    canvas.DrawBitmap(source, dest, paint);
}

static SKColor Lighten(SKColor color, float amount) => new(
    (byte)Math.Min(255, color.Red + (255 - color.Red) * amount),
    (byte)Math.Min(255, color.Green + (255 - color.Green) * amount),
    (byte)Math.Min(255, color.Blue + (255 - color.Blue) * amount));

static SKColor Darken(SKColor color, float amount) => new(
    (byte)(color.Red * (1 - amount)),
    (byte)(color.Green * (1 - amount)),
    (byte)(color.Blue * (1 - amount)));

static SKColor WithAlpha(SKColor color, byte alpha) => new(color.Red, color.Green, color.Blue, alpha);

sealed record Bank(string Id, string Name, string ShortName, string[] Aliases, string IconSource, SKColor Color, bool DarkText, string SourceUrl);

sealed class PackManifest
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Version { get; set; }
    public string? Notice { get; set; }
    public List<PackAsset> Assets { get; set; } = [];
}

sealed class PackAsset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public string File { get; set; } = "";
    public string Background { get; set; } = "#FFFFFF";
    public string Kind { get; set; } = "logo";
    public string AddedIn { get; set; } = "1.0.0";
}
