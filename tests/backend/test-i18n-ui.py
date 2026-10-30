"""Checks that the C# interface strings are complete in English and Arabic.

Run with any Python 3 (no packages needed):  python tests/backend/test-i18n-ui.py

Every text shown by the app goes through L.T("key") or L.F("key", ...). The keys are the original Chinese texts
(a later phase renames them); StringsEn.cs and StringsAr.cs map each key to its English and Arabic text. The check fails when
  - a key used in the code has no English or no Arabic text,
  - a key appears twice in StringsAr.cs or StringsEn.cs,
  - an English key has no Arabic text, or an Arabic key has no English text,
  - an English or Arabic text still contains Chinese characters,
  - an Arabic text lost or changed a placeholder such as {0} or {1:0.00}.
"""
from pathlib import Path
import collections
import glob
import re
import sys

SRC = Path(__file__).resolve().parents[2] / 'src' / 'KwekDrop'
STR = r'"((?:[^"\\]|\\.)*)"'
ENTRY = re.compile(r'\[' + STR + r'\]\s*=\s*' + STR)
CJK = re.compile('[一-鿿]')
HOLE = re.compile(r'\{\d+(?::[^}]*)?\}')


def entries(name):
    target = SRC / name
    if not target.exists():
        target = SRC / 'Localization' / name
    return ENTRY.findall(target.read_text(encoding='utf-8-sig'))


en_list, ar_list = entries('StringsEn.cs'), entries('StringsAr.cs')
en, ar = dict(en_list), dict(ar_list)
count_en = collections.Counter(k for k, _ in en_list)
count_ar = collections.Counter(k for k, _ in ar_list)

used = set()
for f in glob.glob(str(SRC / '**' / '*.cs'), recursive=True):
    parts = Path(f).parts
    if 'bin' in parts or 'obj' in parts:
        continue
    if f.endswith(('StringsEn.cs', 'StringsAr.cs')):
        continue
    for m in re.finditer(r'L\.(?:T|F)\(\s*' + STR, Path(f).read_text(encoding='utf-8-sig')):
        used.add(m.group(1))

problems = {
    'used in code but missing in English': sorted(k for k in used if k not in en),
    'used in code but missing in Arabic': sorted(k for k in used if k not in ar),
    'duplicate keys in StringsEn.cs': sorted(k for k, c in count_en.items() if c > 1),
    'duplicate keys in StringsAr.cs': sorted(k for k, c in count_ar.items() if c > 1),
    'English key without Arabic text': sorted(k for k in en if k not in ar),
    'Arabic key without English text': sorted(k for k in ar if k not in en),
    'Chinese characters left in an English text': sorted(k for k, v in en.items() if CJK.search(v)),
    'Chinese characters left in an Arabic text': sorted(k for k, v in ar.items() if CJK.search(v)),
    'placeholders differ between English and Arabic': sorted(
        k for k in ar if k in en and sorted(HOLE.findall(en[k])) != sorted(HOLE.findall(ar[k]))),
}

bad = 0
for name, keys in problems.items():
    print(f'{len(keys):4d}  {name}')
    for k in keys[:5]:
        print('        ', k.encode('unicode_escape').decode('ascii')[:80])
    bad += len(keys)
print(f'English keys {len(en)}, Arabic keys {len(ar)}, keys used in code {len(used)}')
sys.exit(1 if bad else 0)
