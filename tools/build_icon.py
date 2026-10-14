"""Иконка Windows из отдельной фирменной эмблемы, предоставленной владельцем."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent
source = Image.open(root / 'NextConfig/Assets/naviset-mark.jpg').convert('RGBA')
canvas = Image.new('RGBA', (256, 256), 'white')
mark = source.resize((232, 232), Image.Resampling.LANCZOS)
canvas.alpha_composite(mark, (12, 12))
canvas.save(root / 'NextConfig/naviset.ico', sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
