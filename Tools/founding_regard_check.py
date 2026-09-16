#!/usr/bin/env python3
"""Check the native founding-reputation witness in a paired Quickstart run.

Supplemental only: lifecycle, profile recipe, strict logs and owned-stop checks remain required.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

CHANGE = re.compile(
    r'founding regard native change: baseline=(-?\d+); city-delta=73; '
    r'personal-before=(-?\d+); personal-after=(-?\d+); standing=(-?\d+); carry=(-?\d+); '
    r'compared=(\d+); synthetic-reputation=true; synthetic-residents=false')
WITNESS = re.compile(
    r'founding regard native witness: stage=(startup|grown|loaded); entries=(\d+); '
    r'sha256=([0-9a-f]{64}); preserved=true')
FREEZE = re.compile(r'founding regard freeze boundary: result=True; version=2; count=(\d+); '
                    r'inanimate=0; personal=0; eligible=True; identity=True; polity=True;')


def check(source: str, loaded: str) -> dict:
    changes = CHANGE.findall(source)
    if len(changes) != 1 or CHANGE.search(loaded):
        raise ValueError('expected one source reputation change, never replayed after load')
    baseline, before, after, standing, carry, count = map(int, changes[0])
    if not 10 < count <= 512 or after - before != 40:
        raise ValueError('wrong initial snapshot size or native personal reputation delta')
    scaled = (baseline + 73) * 100 + (after - before) * 50  # real Quickstart starts at Camp
    expected = (abs(scaled) // 100) * (1 if scaled >= 0 else -1)
    if standing != expected or carry != scaled - expected * 100:
        raise ValueError('independent city delta and native Camp spillover do not compose')
    freezes = FREEZE.findall(source)
    if len(freezes) != 1 or int(freezes[0]) != count or FREEZE.search(loaded):
        raise ValueError('missing populated first-founding freeze, or freeze replayed on load')
    warm = WITNESS.findall(source)
    cold = WITNESS.findall(loaded)
    if [row[0] for row in warm] != ['startup', 'grown'] or [row[0] for row in cold] != ['loaded']:
        raise ValueError('missing, duplicate, misplaced or reordered native witnesses')
    values = warm + cold
    if any(int(row[1]) != count for row in values) or len({row[2] for row in values}) != 1:
        raise ValueError('city reputation snapshot differs across construction or cold load')
    return {'verdict': 'PASS', 'entries': count, 'snapshotSha256': values[0][2],
            'baseline': baseline, 'cityDelta': 73, 'personalBefore': before,
            'personalAfter': after, 'standing': standing, 'carry': carry,
            'scope': 'source founding, controlled native reputation changes, construction, cold load; '
                     'supplements lifecycle, exact profile, strict log and owned-stop evidence'}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('loaded', type=Path)
    parser.add_argument('--results', required=True, type=Path)
    args = parser.parse_args()
    raw = [p.read_bytes() for p in (args.source, args.loaded)]
    try:
        result = check(*(value.decode('utf-8-sig') for value in raw))
    except (ValueError, UnicodeError) as error:
        result = {'verdict': 'FAIL', 'reason': str(error)}
    result['inputSha256'] = [hashlib.sha256(value).hexdigest() for value in raw]
    args.results.write_text(json.dumps(result, indent=2) + '\n')
    print(result['verdict'] + ': founding reputation native witness')
    return 0 if result['verdict'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())
