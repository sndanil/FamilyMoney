"""Render Icon.svg to standard PNG sizes and build wallet.ico."""

from __future__ import annotations

import io
import re
import xml.etree.ElementTree as ET
from pathlib import Path

import cairosvg
from PIL import Image

ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "Icon_flat.svg"
OUTPUT_DIR = ROOT

PNG_SIZES = (16, 32, 48, 64, 128, 256, 512)
ICO_SIZES = (16, 32, 48, 256)
PADDING_RATIO = 0.08


def parse_path_numbers(path_data: str) -> list[float]:
    return [float(value) for value in re.findall(r"[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?", path_data)]


def compute_content_bounds(svg_text: str) -> tuple[float, float, float, float]:
    numbers: list[float] = []
    for match in re.finditer(r'\bd="([^"]+)"', svg_text):
        numbers.extend(parse_path_numbers(match.group(1)))

    xs = numbers[0::2]
    ys = numbers[1::2]
    return min(xs), min(ys), max(xs), max(ys)


def build_cropped_svg(svg_text: str) -> str:
    min_x, min_y, max_x, max_y = compute_content_bounds(svg_text)
    width = max_x - min_x
    height = max_y - min_y
    pad_x = width * PADDING_RATIO
    pad_y = height * PADDING_RATIO

    view_box = (
        0,
        0,
        512,
        512,
    )

    root = ET.fromstring(svg_text)
    svg_ns = "http://www.w3.org/2000/svg"
    svg_element = root if root.tag.endswith("svg") else root.find(f"{{{svg_ns}}}svg")
    if svg_element is None:
        raise RuntimeError("SVG root element not found")

    # svg_element.set("viewBox", " ".join(f"{value:.4f}" for value in view_box))
    #svg_element.set("width", f"{view_box[2]:.4f}")
    # svg_element.set("height", f"{view_box[3]:.4f}")

    return ET.tostring(svg_element, encoding="unicode")


def render_png(svg_text: str, size: int) -> Image.Image:
    png_bytes = cairosvg.svg2png(
        bytestring=svg_text.encode("utf-8"),
        output_width=size,
        output_height=size,
    )
    return Image.open(io.BytesIO(png_bytes)).convert("RGBA")


def main() -> None:
    source_text = SOURCE.read_text(encoding="utf-8")
    cropped_svg = build_cropped_svg(source_text)
    rendered: dict[int, Image.Image] = {}

    for size in PNG_SIZES:
        icon = render_png(cropped_svg, size)
        output_path = OUTPUT_DIR / f"icon-{size}.png"
        icon.save(output_path, format="PNG", optimize=True)
        rendered[size] = icon
        print(f"Saved {output_path.name}: {size}x{size}")

    ico_path = OUTPUT_DIR / "wallet.ico"
    ico_images = [rendered[size] for size in ICO_SIZES]
    
    icon_16 = Image.open(OUTPUT_DIR / f"icon-16.png").convert("RGBA")
    other_sizes = [
        Image.open(OUTPUT_DIR / f"icon-32.png").convert("RGBA"),
        Image.open(OUTPUT_DIR / f"icon-48.png").convert("RGBA"),
        Image.open(OUTPUT_DIR / f"icon-256.png").convert("RGBA")
    ]
   
    #icon_16.save(
    #    ico_path,
    #    format="ICO",        
    #    append_images=other_sizes,
    #)
    print(f"Saved {ico_path.name}: {', '.join(f'{size}x{size}' for size in ICO_SIZES)}")


if __name__ == "__main__":
    main()
