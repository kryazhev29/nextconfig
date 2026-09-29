"""Проверка packed-раскладок по эталонным CP1251 исходникам, без компиляции прошивки."""
from pathlib import Path
import re
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
base = root / 'СофтЭксперт/От старой системы'
v5 = base / 'Финальный релиз только v5/Naviset Domovoi(Mini)_v3.0_IAR93/Naviset Domovoi(Mini)_v3.0_IAR93'
v1 = base / 'Старый релиз для v1-v3/v1-v3/Naviset Domovoi(Mini)_IAR75'
cfg = base / 'Финальный релиз только v5/Configurator Domovoi Mini/Configurator Domovoi Mini/Unit1.h'


def read(path):
    return re.sub(r'//[^\n]*|/\*.*?\*/', '', path.read_text(encoding='cp1251'), flags=re.S)


def layout(text, name, types):
    end = re.search(r'}\s*' + name + r'\s*;', text).start()
    start = text.rfind('typedef struct', 0, end)
    body = text[text.index('{', start) + 1:end]
    return fields(body, types)


def fields(body, types):
    result, offset, bits = {}, 0, 0
    while body.strip():
        body = body.strip()
        if body.startswith('struct'):
            assert bits == 0
            m = re.match(r'struct\s*{([^{}]*)}\s*(\w*)\s*;', body, re.S)
            assert m, body[:80]
            size, nested = fields(m[1], types)
            result.update({(m[2] + '.' if m[2] else '') + k: (offset + v[0], v[1]) for k, v in nested.items()})
            offset += size
        else:
            m = re.match(r'(char\*|\w+)\s+(\w+)(?:\[(\d+)\])?(?::(\d+))?\s*;', body)
            assert m, body[:80]
            typ, name, count, width = m.groups()
            if width:
                assert types[typ] == 1
                if bits == 0:
                    offset += 1
                result[name] = (offset - 1, bits)
                bits += int(width)
                assert bits <= 8
                if bits == 8:
                    bits = 0
            else:
                bits = 0
                result[name] = (offset, None)
                offset += types[typ] * int(count or 1)
        body = body[m.end():]
    return offset, result


def structures(path):
    text = read(path)
    types = {'char*': 4, 'char': 1, 'Int8U': 1, 'Int16U': 2, 'Int32U': 4,
             'uint8_t': 1, 'uint16_t': 2, 'uint32_t': 4, 'int8_t': 1, 'int16_t': 2}
    for name in ('connectstr', 'shedulestr'):
        types[name] = layout(text, name, types)[0]
    return {name: layout(text, name, types) for name in ('DevStr', 'ConfStr', 'phoneparams_t')}


fw, old, configurator = structures(v5 / 'structures.h'), structures(v1 / 'structures.h'), structures(cfg)
expected = {
    'ConfStr': {'changetime': 5, 'admin_pass': 9, 'user_pass': 11, 'gsmoperator': 14,
                'accesspoint': 15, 'simpin': 55, 'server': 60, 'NTPserver': 92,
                'NTPport': 116, 'simnumber': 118, 'outtype': 134, 'outpause': 136,
                'shedl': 138, 'imageresolution': 159, 'timezone': 160, 'resettime': 161,
                'actoutperiod': 162, 'actoutperiod_minutes': 165, 'actoutdays': 167,
                'ringcount': 168, 'inputfun': 169, 'fotodelay': 171, 'balance_limit': 172,
                'balance_getnumber': 174, 'balance_getNumberString': 176,
                'fromAllShedule': 192, 'RFIDAntennaCount': 193, 'readerType': 194,
                'reserv': 195, 'crc': 510},
    'DevStr': {'firmware': 16, 'hardware': 17, 'builddate': 18, 'buildtime': 20,
               'buildclient': 22, 'gsmcsq': 24, 'year': 25, 'callingnumber': 33,
               'worktime': 49, 'currentoperator': 53, 'numberscount': 54,
               'accesslevel': 60, 'temperature': 61, 'inputs': 62, 'localtime': 66,
               'logscount': 79, 'numberslimit': 160, 'balance_level': 166,
               'RFIDSCount': 237, 'RFIDAntennaState': 238, 'net_type': 269, 'pwrExt': 270},
    'phoneparams_t': {'index': 0, 'phone': 1, 'name': 12, 'usetime.endtime': 33,
                      'usetime.passages': 37, 'usetime.shedule': 38, 'used_passages': 39,
                      'reserved2': 40},
}
checks = 0
for name, offsets in expected.items():
    for source in (fw, old, configurator):
        for field, offset in offsets.items():
            assert source[name][1][field][0] == offset, (name, field, source[name][1][field])
            checks += 1
    assert fw[name][0] == old[name][0] == configurator[name][0]
    print(name, fw[name][0], 'bytes; v5 / v1-v3 / old configurator offsets agree')
for name, field, byte, bit in [('ConfStr', 'getpass', 13, 5), ('ConfStr', 'checkbalance', 164, 3),
                              ('DevStr', 'outst', 70, 2), ('DevStr', 'connectedtoserver', 32, 6),
                              ('phoneparams_t', 'params.code_type', 32, 4)]:
    for source in (fw, old, configurator):
        assert source[name][1][field] == (byte, bit)
        checks += 1
print('PASS:', checks, 'source offset/bitfield checks (not a firmware compatibility claim).')
