"""Required modern Quickstart housing observations; journal checks are not source authentication."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

ROW = 'quickstart-settlement'
FIELDS = {'stage', 'citizens', 'housed', 'shelters', 'beds', 'rooms', 'clear-floor',
          'turns', 'synthetic-residents', 'forced-housing'}
STEPS = {'startup': 'lifecycle-open', 'grown': 'lifecycle-grown', 'loaded': 'lifecycle-loaded'}


def assess(rows, cold_load=False):
    stages = ('startup', 'grown', 'loaded') if cold_load else ('startup', 'grown')
    found, problems = {}, []
    for index, (verb, outcome, message) in enumerate(rows):
        if verb != ROW:
            continue
        values = {}
        for field in message.split('; '):
            key, separator, value = field.partition('=')
            if not separator or not value or key in values:
                problems.append('housing observation has a malformed or duplicate field')
                break
            values[key] = value
        stage = values.get('stage')
        if stage not in stages or stage in found:
            problems.append('housing observation has an unexpected or duplicate stage: ' + str(stage))
            continue
        found[stage] = (index, values)
        if outcome != 'OK' or set(values) != FIELDS:
            problems.append(stage + ': housing observation refused or has incomplete fields')
            continue
        expected = {'citizens': '4', 'housed': '0' if stage == 'startup' else '4',
                    'shelters': '0' if stage == 'startup' else '2',
                    'rooms': '0' if stage == 'startup' else '2',
                    'clear-floor': '0' if stage == 'startup' else '34',
                    'synthetic-residents': 'false', 'forced-housing': 'false'}
        if any(values[key] != value for key, value in expected.items()):
            problems.append(stage + ': original founders or enclosed clear-floor housing differ')
        for key in ('beds', 'turns'):
            if not re.fullmatch(r'0|[1-9][0-9]*', values[key]):
                problems.append(stage + ': invalid ' + key)
        if re.fullmatch(r'0|[1-9][0-9]*', values['beds']) and (int(values['beds']) != 0 if stage == 'startup'
                                        else int(values['beds']) < 6):
            problems.append(stage + ': physical bed capacity differs')
        enclosing = [i for i, row in enumerate(rows) if row[0] == STEPS[stage]]
        if len(enclosing) != 1 or enclosing[0] <= index or rows[enclosing[0]][1] != 'OK':
            problems.append(stage + ': observation must precede its successful lifecycle step')
    missing = set(stages) - set(found)
    if missing:
        problems.append('missing housing observations: ' + ', '.join(sorted(missing)))
    if not missing:
        positions = [found[stage][0] for stage in stages]
        if positions != sorted(positions):
            problems.append('housing observations are out of order')
        try:
            turns = [int(found[stage][1]['turns']) for stage in stages]
            if turns[1] - turns[0] < 16800 or turns != sorted(turns):
                problems.append('housing observations do not span the ordinary construction wait and load')
        except (KeyError, ValueError):
            problems.append('housing observation turn interval is absent or malformed')
    return problems


def main():
    import persona_matrix
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('journals', nargs='+', type=Path)
    parser.add_argument('--cold-load', action='store_true')
    args = parser.parse_args()
    if len({p.resolve() for p in args.journals}) != len(args.journals):
        parser.error('each journal must name a distinct session')
    rows = []
    for path in args.journals:
        rows.extend(persona_matrix.read_journal(path.read_text(encoding='utf-8-sig')))
    problems = assess(rows, args.cold_load)
    print(json.dumps({'status': 'FAIL' if problems else 'PASS', 'problems': problems,
                      'coldLoad': args.cold_load, 'journalOnly': True}, indent=2))
    return 1 if problems else 0


if __name__ == '__main__':
    raise SystemExit(main())
