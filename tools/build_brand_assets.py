from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
IMAGES = ROOT / "docs" / "images"

NAVY = "#151d35"
PANEL = "#202b49"
BLUE = "#7899df"
PALE = "#eef2ff"
CYAN = "#55d6d2"
GOLD = "#ffd45f"
CORAL = "#ff7657"


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    name = "segoeuib.ttf" if bold else "segoeui.ttf"
    return ImageFont.truetype(str(Path("C:/Windows/Fonts") / name), size)


def draw_pixel_letters(draw: ImageDraw.ImageDraw, origin: tuple[int, int], scale: int) -> None:
    glyphs = {
        "I": ["111", "010", "010", "010", "010", "010", "111"],
        "L": ["100", "100", "100", "100", "100", "100", "111"],
    }
    x0, y0 = origin
    cursor = x0
    for letter in "IL":
        rows = glyphs[letter]
        for y, row in enumerate(rows):
            for x, value in enumerate(row):
                if value == "1":
                    draw.rectangle(
                        (
                            cursor + x * scale,
                            y0 + y * scale,
                            cursor + (x + 1) * scale - 1,
                            y0 + (y + 1) * scale - 1,
                        ),
                        fill=PALE,
                    )
        cursor += 4 * scale


def build_icon() -> Image.Image:
    icon = Image.new("RGB", (80, 80), NAVY)
    draw = ImageDraw.Draw(icon)
    draw.rectangle((2, 2, 77, 77), outline="#0a1020", width=3)
    draw.rectangle((6, 6, 73, 73), fill=PANEL, outline=BLUE, width=3)

    colours = [CYAN, BLUE, "#9b72cf", CORAL, GOLD]
    for index, colour in enumerate(colours):
        x = 10 + index * 12
        draw.rectangle((x, 11, x + 8, 19), fill=colour)

    draw.rectangle((12, 26, 67, 66), fill="#11192e")
    for y in range(26, 67, 8):
        for x in range(12, 68, 8):
            if ((x - 12) // 8 + (y - 26) // 8) % 2 == 0:
                draw.rectangle((x, y, min(x + 7, 67), min(y + 7, 66)), fill="#27365b")

    draw.rectangle((20, 42, 58, 65), fill="#263f76")
    draw.polygon(((20, 52), (31, 40), (40, 49), (48, 38), (58, 51), (58, 65), (20, 65)), fill=CYAN)
    draw.rectangle((54, 31, 61, 38), fill=GOLD)

    draw_pixel_letters(draw, (24, 31), 5)
    return icon


def fit_crop(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    return ImageOps.fit(image, size, method=Image.Resampling.LANCZOS, centering=(0.5, 0.48))


def build_banner(icon: Image.Image) -> Image.Image:
    menu = Image.open(IMAGES / "menu.png").convert("RGB")
    exact = Image.open(IMAGES / "exact-rgb-source.png").convert("RGB")

    banner = Image.new("RGB", (1400, 420), NAVY)

    menu_texture = fit_crop(menu.crop((0, 0, menu.width, min(menu.height, 560))), (760, 420))
    menu_texture = ImageEnhance.Brightness(menu_texture).enhance(0.46)
    banner.paste(menu_texture, (0, 0))

    duck_crop = exact.crop(
        (
            int(exact.width * 0.28),
            int(exact.height * 0.03),
            int(exact.width * 0.74),
            int(exact.height * 0.92),
        )
    )
    duck_panel = fit_crop(duck_crop, (650, 400))
    banner.paste(duck_panel, (742, 10))

    draw = ImageDraw.Draw(banner)

    # A hard pixel staircase separates the product panel from the in-game art.
    steps = [(690, 0), (750, 0), (750, 52), (738, 52), (738, 104), (726, 104),
             (726, 156), (714, 156), (714, 208), (702, 208), (702, 260),
             (690, 260), (690, 420), (0, 420), (0, 0)]
    draw.polygon(steps, fill=NAVY)
    for offset in (0, 5):
        draw.line([(750 - offset, 0), (750 - offset, 52), (738 - offset, 52),
                   (738 - offset, 104), (726 - offset, 104), (726 - offset, 156),
                   (714 - offset, 156), (714 - offset, 208), (702 - offset, 208),
                   (702 - offset, 260), (690 - offset, 260), (690 - offset, 420)],
                  fill=BLUE if offset == 0 else "#2b3a60", width=3)

    title = menu.crop((205, 2, 595, 72))
    title = title.resize((552, 99), Image.Resampling.LANCZOS)
    title_cleanup = ImageDraw.Draw(title)
    title_cleanup.rectangle((0, 87, 38, 98), fill=title.getpixel((500, 90)))
    banner.paste(title, (66, 43))

    draw.text((72, 166), "LOAD  •  CONVERT  •  PLACE", font=font(26, True), fill=PALE)
    draw.text((72, 207), "URL images become Terraria canvases.", font=font(23), fill="#cdd8f5")

    chip_x = 72
    for label, colour in (("VANILLA BLOCKS", BLUE), ("EXACT RGB", CORAL), ("GALLERY MODE", CYAN)):
        box = draw.textbbox((0, 0), label, font=font(16, True))
        width = box[2] - box[0] + 28
        draw.rectangle((chip_x, 258, chip_x + width, 294), fill=PANEL, outline=colour, width=2)
        draw.text((chip_x + 14, 265), label, font=font(16, True), fill=PALE)
        chip_x += width + 12

    banner.paste(icon.resize((72, 72), Image.Resampling.NEAREST), (72, 322))
    draw.text((164, 329), "IMAGE LOADER", font=font(21, True), fill=PALE)
    draw.text((164, 359), "tModLoader • Terraria 1.4.4", font=font(17), fill="#aebfe9")

    draw.rectangle((0, 0, 1399, 419), outline="#0a1020", width=8)
    draw.rectangle((8, 8, 1391, 411), outline=BLUE, width=2)
    return banner


def main() -> None:
    gallery_source = Image.open(IMAGES / "gallery-source.png").convert("RGB")
    gallery_source.crop((422, 0, gallery_source.width, 835)).save(
        IMAGES / "gallery.png", optimize=True
    )

    exact_source = Image.open(IMAGES / "exact-rgb-source.png").convert("RGB")
    exact_source.crop((408, 0, exact_source.width, 900)).save(
        IMAGES / "exact-rgb-placement.png", optimize=True
    )

    icon = build_icon()
    icon.save(ROOT / "icon.png", optimize=True)
    icon.resize((30, 30), Image.Resampling.NEAREST).save(ROOT / "icon_small.png", optimize=True)
    build_banner(icon).save(IMAGES / "banner.png", optimize=True)


if __name__ == "__main__":
    main()
