"""Доработка: Кряжев А., 2026. Локальный просмотр неизменённых PNG до и после."""
from pathlib import Path
from html import escape

root = Path(__file__).resolve().parent.parent / 'docs/ui'
cases = [
    ('connection', 'Подключение', 'Скрытый ПИН, кнопка показа, выравнивание и фирменный значок.'),
    ('state', 'Состояние', 'Равная высота карточек, подпись сервера в одну строку, распределение высоты.'),
    ('settings', '485 и Wiegand', 'Адреса без стрелок, приглушённые пустые строки, единая нижняя панель.'),
    ('ids', 'Идентификаторы', 'Значок NAVISET и проверка компоновки.'),
    ('object', 'Объект', 'Значок NAVISET и проверка компоновки.'),
    ('firmware', 'Прошивка', 'Значок NAVISET и проверка компоновки.'),
    ('log', 'Журнал', 'Значок NAVISET и проверка компоновки.'),
    ('pin', 'Смена ПИН-кода', 'Фирменный значок окна; сохранены поля и действия.'),
    ('select', 'Выбор', 'Фирменный значок окна.'),
    ('text', 'Ввод текста', 'Фирменный значок окна.'),
    ('error', 'Сообщение', 'Фирменный значок окна.'),
    ('changes', 'Изменения перед записью', 'Значок окна и проверка нижних действий.'),
    ('fw', 'Окно загрузки прошивки', 'Фирменный значок окна; передача не запускалась.'),
]
sections = []
for name, title, description in cases:
    images = []
    for folder, caption in [('all', 'До · 8ea0481'), ('after', 'После')]:
        src = f'screenshots/{folder}/{name}-125.png'
        assert (root / src).is_file(), src
        images.append(f'<figure><figcaption>{caption}</figcaption><a href="{src}"><img loading="lazy" src="{src}" alt="{escape(title)} — {caption}"></a></figure>')
    sections.append(f'<section id="{name}"><h2>{escape(title)}</h2><p>{escape(description)}</p><div class="pair">{"".join(images)}</div></section>')
document = '''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>NextConfig — до и после</title><style>
body{margin:0;background:#f4f7fb;color:#202b3a;font:16px/1.5 system-ui,sans-serif}main{max-width:1600px;margin:auto;padding:32px}
h1{font-size:30px;margin-bottom:8px}h2{font-size:22px;margin:0}p{color:#526073}section{background:white;border:1px solid #dce4ee;border-radius:8px;padding:24px;margin:24px 0}
.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px;align-items:start}figure{margin:0;min-width:0}figcaption{font-weight:600;margin-bottom:12px}img{width:100%;height:auto;display:block;border:1px solid #dce4ee;box-sizing:border-box}a{color:#2563eb}
@media(max-width:800px){main{padding:16px}.pair{grid-template-columns:1fr}}
</style><main><h1>NextConfig — до и после</h1><p>Масштаб содержимого 125%. Исходные PNG сохранены. Сравнение включает все формы и вкладки; снимки открываются в полном размере по нажатию.</p>
<p>Чёрная техническая рамка прежнего PrintWindow удалена из нового захвата по координатам Windows. Это изменение снимка, а не содержимого приложения. <a href="POLISH_REVIEW.md">Проверки и подробности</a>.</p>
'''+''.join(sections)+'</main></html>'
(root/'comparison.html').write_text(document,encoding='utf-8')
