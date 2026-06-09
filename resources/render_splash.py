"""Render Icon.svg to Android splash screen drawables."""

from __future__ import annotations

import io
from pathlib import Path

import cairosvg
from PIL import Image

ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "Icon.svg"
OUTPUT_DIR = ROOT.parent / "src" / "FamilyMoney.Android" / "Resources" / "drawable"

# Android 12+ masks splash icons to a circle (~66% of canvas).
# Square artwork must fit inside that circle, so we inset the icon.
SAFE_ZONE_RATIO = 0.50


def render(size: int, filename: str) -> None:
    output_path = OUTPUT_DIR / filename
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    icon_size = max(1, int(size * SAFE_ZONE_RATIO))
    png_bytes = cairosvg.svg2png(
        url=str(SOURCE),
        output_width=icon_size,
        output_height=icon_size,
    )
    icon = Image.open(io.BytesIO(png_bytes)).convert("RGBA")

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    offset = (size - icon_size) // 2
    canvas.paste(icon, (offset, offset), icon)
    canvas.save(output_path, format="PNG", optimize=True)
    print(f"Saved {output_path.name}: {size}x{size} (icon {icon_size}x{icon_size})")


def main() -> None:
    render(288, "splash_icon.png")
    render(512, "icon.png")


if __name__ == "__main__":
    main()
