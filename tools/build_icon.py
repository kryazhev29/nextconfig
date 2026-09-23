"""Доработка: Кряжев А., 2026. Конвертация фирменной эмблемы из исходного логотипа."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent
source = Image.open(root / 'NextConfig/obj/naviset-source.png').convert('RGBA')
# Эмблема N со стрелкой из логотипа NAVISET; надпись не ужимаем до нечитаемых 16 px.
mark = source.crop((26, 18, 145, 137))
pixels = mark.load()
for y in range(mark.height):
    for x in range(mark.width):
        if pixels[x, y][:3] == (255, 255, 255):
            pixels[x, y] = (255, 255, 255, 0)
canvas = Image.new('RGBA', (256, 256))
mark = mark.resize((232, 232), Image.Resampling.LANCZOS)
canvas.alpha_composite(mark, (12, 12))
canvas.save(root / 'NextConfig/naviset.ico', sizes=[(16, 16), (32, 32), (48, 48), (256, 256)])
