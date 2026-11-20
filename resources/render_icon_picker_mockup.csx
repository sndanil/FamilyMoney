#r "nuget: SkiaSharp, 3.119.4"
#r "nuget: Svg.Skia, 3.0.4"
#r "nuget: Material.Icons, 3.0.2"

// Схема диалога «Иконка» для docs/entity-icons.md.
// Запуск из корня репозитория: dotnet script resources/render_icon_picker_mockup.csx

using System;
using System.IO;
using System.Text;
using Material.Icons;
using SkiaSharp;
using Svg.Skia;

const float W = 760;
const float H = 760;
const float Scale = 2f;

var background = new SKColor(0x17, 0x21, 0x2B);
var surface = new SKColor(0x1E, 0x2A, 0x38);
var surfaceRaised = new SKColor(0x23, 0x2E, 0x3C);
var border = new SKColor(0x2A, 0x38, 0x48);
var textPrimary = new SKColor(0xEA, 0xF1, 0xF8);
var textSecondary = new SKColor(0x8F, 0xA3, 0xB5);
var accent = new SKColor(0x33, 0x90, 0xEC);
var iconColor = new SKColor(0xC9, 0xD8, 0xE6);

var palette = new[]
{
    new SKColor(0x33, 0x90, 0xEC),
    new SKColor(0x2E, 0x7D, 0x32),
    new SKColor(0xEF, 0x6C, 0x00),
    new SKColor(0xE5, 0x39, 0x35),
    new SKColor(0xAB, 0x47, 0xBC),
    new SKColor(0x00, 0x89, 0x7B),
    new SKColor(0x8D, 0x6E, 0x63),
    new SKColor(0x78, 0x90, 0x9C),
    new SKColor(0xD8, 0x1B, 0x60),
    new SKColor(0x5C, 0x6B, 0xC0),
};

var regular = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
    ?? SKTypeface.Default;
var bold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
    ?? regular;

var bitmap = new SKBitmap((int)(W * Scale), (int)(H * Scale));
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(new SKColor(0x0B, 0x12, 0x19));
    canvas.Scale(Scale);

    Rounded(canvas, 0, 0, W, H, 18, background);
    RoundedStroke(canvas, 0.5f, 0.5f, W - 1, H - 1, 18, border);

    Text(canvas, "Иконка счёта или категории", 32, 50, 20, textPrimary, bold);
    DrawIcon(canvas, MaterialIconKind.Close, textSecondary, 690, 28, 22);

    // Вкладки.
    Rounded(canvas, 32, 72, 520, 38, 19, surface);
    Rounded(canvas, 36, 76, 132, 30, 15, accent);
    Text(canvas, "Иконки", 102, 96, 14, SKColors.White, bold, SKTextAlign.Center);
    Text(canvas, "Готовые", 204, 96, 14, textSecondary, regular, SKTextAlign.Center);
    Text(canvas, "Буква", 298, 96, 14, textSecondary, regular, SKTextAlign.Center);
    Text(canvas, "Свой файл", 420, 96, 14, textSecondary, regular, SKTextAlign.Center);

    // Поиск.
    Rounded(canvas, 32, 126, 696, 42, 12, surface);
    DrawIcon(canvas, MaterialIconKind.Magnify, textSecondary, 44, 135, 24);
    Text(canvas, "продукты", 76, 153, 15, textPrimary, regular);
    Rounded(canvas, 76 + TextWidth("продукты", 15, regular) + 2, 137, 2, 22, 1, accent);

    // Группы.
    var chips = new[] { "Деньги", "Дом", "Транспорт", "Еда", "Здоровье", "Досуг", "Ещё" };
    var chipX = 32f;
    foreach (var chip in chips)
    {
        var width = TextWidth(chip, 13, regular) + 30;
        var active = chip == "Еда";
        Rounded(canvas, chipX, 184, width, 30, 15, active ? accent : surface);
        Text(canvas, chip, chipX + width / 2, 204, 13,
            active ? SKColors.White : textSecondary, active ? bold : regular, SKTextAlign.Center);
        chipX += width + 10;
    }

    // Сетка 8 × 3: цветные бейджи.
    var icons = new[]
    {
        MaterialIconKind.CartOutline,
        MaterialIconKind.SilverwareForkKnife,
        MaterialIconKind.GasStation,
        MaterialIconKind.Car,
        MaterialIconKind.Bus,
        MaterialIconKind.HomeOutline,
        MaterialIconKind.HammerWrench,
        MaterialIconKind.SofaOutline,
        MaterialIconKind.LocalPharmacy,
        MaterialIconKind.MedicalBag,
        MaterialIconKind.BabyFaceOutline,
        MaterialIconKind.TshirtCrewOutline,
        MaterialIconKind.MovieOpen,
        MaterialIconKind.GamepadVariant,
        MaterialIconKind.CashMultiple,
        MaterialIconKind.WalletOutline,
        MaterialIconKind.CreditCardOutline,
        MaterialIconKind.PiggyBankOutline,
        MaterialIconKind.BankOutline,
        MaterialIconKind.ChartLine,
        MaterialIconKind.SwapHorizontal,
        MaterialIconKind.Wifi,
        MaterialIconKind.Airplane,
        MaterialIconKind.GiftOutline,
    };

    const float cell = 76;
    const float gap = 10;
    const float badge = 54;
    var gridLeft = 32f;
    var gridTop = 234f;

    for (var i = 0; i < icons.Length; i++)
    {
        var col = i % 8;
        var row = i / 8;
        var x = gridLeft + col * (cell + gap);
        var y = gridTop + row * (cell + gap);
        var selected = icons[i] == MaterialIconKind.CartOutline;
        var color = selected ? palette[1] : accent;

        Rounded(canvas, x, y, cell, cell, 16, surface);
        Rounded(canvas, x + 11, y + 11, badge, badge, 15, color);
        DrawIcon(canvas, icons[i], SKColors.White, x + 23, y + 23, 30);

        if (selected)
        {
            RoundedStroke(canvas, x + 9, y + 9, badge + 4, badge + 4, 17, textPrimary, 2);
            using (var badgePaint = new SKPaint { Color = textPrimary, IsAntialias = true })
            {
                canvas.DrawCircle(x + 64, y + 12, 11, badgePaint);
            }

            DrawIcon(canvas, MaterialIconKind.Check, color, x + 56, y + 4, 16);
        }
    }

    // Палитра.
    Text(canvas, "Цвет:", 32, 520, 13, textSecondary, regular);
    for (var i = 0; i < palette.Length; i++)
    {
        var cx = 88 + i * 30;
        using (var paint = new SKPaint { Color = palette[i], IsAntialias = true })
        {
            canvas.DrawCircle(cx, 516, 12, paint);
        }

        if (i == 1)
        {
            using (var ring = new SKPaint
                   {
                       Color = textPrimary,
                       IsAntialias = true,
                       Style = SKPaintStyle.Stroke,
                       StrokeWidth = 2,
                   })
            {
                canvas.DrawCircle(cx, 516, 15, ring);
            }

            DrawIcon(canvas, MaterialIconKind.Check, SKColors.White, cx - 6, 510, 12);
        }
    }

    Text(canvas, "Рекомендуем: «Продукты» → корзина", 32, 556, 13, textSecondary, regular);

    // Превью в реальных размерах.
    Text(canvas, "Так будет в списке:", 32, 620, 13, textSecondary, regular);
    var previewX = 178f;
    foreach (var size in new[] { 28f, 42f, 64f })
    {
        var y = 640 - size;
        Rounded(canvas, previewX, y, size, size, size * 0.28f, palette[1]);
        DrawIcon(canvas, MaterialIconKind.CartOutline, SKColors.White,
            previewX + size * 0.24f, y + size * 0.24f, size * 0.52f);
        previewX += size + 18;
    }

    Text(canvas, "Продукты", previewX + 4, 628, 15, textPrimary, regular);

    // Пример буквенного бейджа.
    var letterX = 484f;
    Rounded(canvas, letterX, 598, 42, 42, 12, palette[1]);
    Text(canvas, "Д", letterX + 21, 626, 22, SKColors.White, bold, SKTextAlign.Center);
    Text(canvas, "Дача", letterX + 54, 628, 15, textPrimary, regular);

    // Нижняя панель.
    RoundedStroke(canvas, 0.5f, 684.5f, W - 1, 0.5f, 0, border, 1);
    Text(canvas, "Без иконки", 32, 728, 14, textSecondary, regular);

    Rounded(canvas, 512, 704, 96, 40, 12, accent);
    Text(canvas, "OK", 560, 729, 14, SKColors.White, bold, SKTextAlign.Center);

    Rounded(canvas, 620, 704, 108, 40, 12, surface);
    Text(canvas, "Отмена", 674, 729, 14, textPrimary, regular, SKTextAlign.Center);
}

var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "docs", "mockups");
Directory.CreateDirectory(outputDirectory);
var outputPath = Path.Combine(outputDirectory, "icon-picker.png");
using (var image = SKImage.FromBitmap(bitmap))
using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
using (var file = File.Create(outputPath))
{
    data.SaveTo(file);
}

Console.WriteLine($"Saved {outputPath}: {bitmap.Width}x{bitmap.Height}");

static void Rounded(SKCanvas canvas, float x, float y, float width, float height, float radius, SKColor color)
{
    using var paint = new SKPaint { Color = color, IsAntialias = true };
    canvas.DrawRoundRect(SKRect.Create(x, y, width, height), radius, radius, paint);
}

static void RoundedStroke(SKCanvas canvas, float x, float y, float width, float height, float radius, SKColor color, float strokeWidth = 1)
{
    using var paint = new SKPaint
    {
        Color = color,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = strokeWidth,
    };
    canvas.DrawRoundRect(SKRect.Create(x, y, width, height), radius, radius, paint);
}

static void Text(SKCanvas canvas, string text, float x, float y, float size, SKColor color, SKTypeface typeface, SKTextAlign align = SKTextAlign.Left)
{
    using var font = new SKFont(typeface, size);
    using var paint = new SKPaint { Color = color, IsAntialias = true };
    canvas.DrawText(text, x, y, align, font, paint);
}

static float TextWidth(string text, float size, SKTypeface typeface)
{
    using var font = new SKFont(typeface, size);
    return font.MeasureText(text);
}

static void DrawIcon(SKCanvas canvas, MaterialIconKind kind, SKColor color, float x, float y, float size)
{
    var pathData = MaterialIconDataProvider.GetData(kind);
    var hex = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    var svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='{pathData}' fill='{hex}'/></svg>";
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
    var source = new SKSvg();
    var picture = source.Load(stream)
        ?? throw new InvalidOperationException($"Не удалось разобрать иконку {kind}");

    canvas.Save();
    canvas.Translate(x, y);
    canvas.Scale(size / 24f);
    canvas.DrawPicture(picture);
    canvas.Restore();
}
