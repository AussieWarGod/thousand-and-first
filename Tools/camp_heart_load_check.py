#!/usr/bin/env python3
"""Paid camp save/load journal proof; also require closed profiles, source bindings and strict logs."""
import argparse
import json
from pathlib import Path
import re
from guest_save_check import field, one, require
from personas import persona_matrix


def equal_fields(detail, expected):
    for name, value in expected.items():
        require(field(detail, name) == value, 'camp invariant differs: ' + name)


def judge(source, loaded):
    for rows in (source, loaded):
        require(persona_matrix.terminal_row(rows) == 'SCRIPT-COMPLETE', 'session did not complete')
        require(all(outcome == 'OK' for _, outcome, _ in rows), 'session contains a refusal')
        terminal, _ = one(rows, 'SCRIPT-COMPLETE')
        require(terminal == len(rows) - 1, 'observations continue after completion')
    setup, _ = one(source, 'camp-heart-setup')
    saved, save = one(source, 'camp-heart-save')
    checks = [(i, detail) for i, (event, _, detail) in enumerate(source) if event == 'camp-heart-check']
    require(len(checks) == 3 and setup < checks[0][0] < checks[1][0] < checks[2][0] < saved,
            'source paid camp phases absent or out of order')
    require(checks[2][1].startswith('native-camp-heart cases=1 passed=1 failed=0;'),
            'source did not complete the real paid upgrade and next-day check')
    advances = [(i, detail) for i, (event, _, detail) in enumerate(source) if event == 'advance-complete']
    require(len(advances) == 3, 'source lacks its three ordinary waits')
    prior = setup
    for (at, detail), (checked, _), count in zip(advances, checks, (1200, 3600, 1200)):
        require(prior < at < checked and detail == f'{count} turn(s) elapsed of {count} requested',
                'source ordinary turn interval differs')
        prior = checked
    equal_fields(save, {'paid-camp-save': 'true', 'rung': '2', 'synthetic-next-job-timber': '1', 'brush': '21'})
    identities = {name: field(save, name) for name in ('heart', 'store', 'fire')}
    require(len(set(identities.values())) == 3, 'camp object identities collide')
    tent_job = field(save, 'tent-job')
    digest = field(save, 'snapshot-sha256')
    require(re.fullmatch('[0-9a-f]{64}', digest), 'camp snapshot digest malformed')
    game = field(save, 'save')
    require(re.fullmatch('[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}', game), 'saved game id malformed')
    names = ('LOAD-BEGIN', 'camp-heart-loaded', 'camp-heart-next', 'camp-heart-resume',
             'advance-complete', 'camp-heart-completed', 'SCRIPT-COMPLETE')
    observations = [one(loaded, name) for name in names]
    require([at for at, _ in observations] == sorted(at for at, _ in observations), 'loaded phases out of order')
    begin, restored, paid, resumed, elapsed, completed, terminal = [detail for _, detail in observations]
    require(not any(event in ('camp-heart-setup', 'camp-heart-check', 'camp-heart-save', 'stagedigest')
                    for event, _, _ in loaded), 'source script replayed in loaded process')
    equal_fields(begin, {'game-id': game, 'new-game': 'false', 'mod-restore': 'false'})
    equal_fields(restored, dict(identities, rung='2', basin='48', brush='21', timber='1', **{'snapshot-sha256': digest, 'tent-job': tent_job}))
    equal_fields(paid, {'water-debited': '2', 'timber-debited': '1', 'synthetic-materials-after-load': '0'})
    next_job, upgrade = field(paid, 'new-job'), field(paid, 'upgrade-job')
    require(len({next_job, upgrade, tent_job}) == 3, 'new job, upgrade and paid tent identities collide')
    equal_fields(resumed, {'vanilla-Continue': 'true', 'saved-script-considered': 'true', 'requested-turns': '3600'})
    require(elapsed == '3600 turn(s) elapsed of 3600 requested', 'loaded ordinary wait incomplete')
    equal_fields(completed, dict(identities, phase='Complete', brush='21', **{'new-job': next_job, 'effects-settled': 'true'}))
    output = field(completed, 'output')
    require(output not in identities.values(), 'completed next output reuses existing camp object')
    require(re.fullmatch('[1-9][0-9]*', field(completed, 'turns')), 'completed game clock absent')
    equal_fields(terminal, {'real-save-quit-load': 'true', 'next-paid-job-complete': 'true',
                           'new-game-script-replayed': 'false', 'ordinary-acceptance': 'false'})
    return dict(verdict='PASS', gameId=game, snapshotSha256=digest, newJobId=next_job, outputId=output,
                **identities, sourceTurns=6000, loadedTurns=3600, brush=21,
                scope='Synthetic paid camp journal proof only; require closed profiles, source/runtime/harness bindings, strict logs and owned stops.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('loaded', type=Path)
    parser.add_argument('--results', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = judge(*(persona_matrix.read_journal(p.read_text(encoding='utf-8-sig')) for p in (args.source, args.loaded)))
    except (OSError, ValueError) as error:
        result = dict(verdict='FAIL', reason=str(error))
    with args.results.open('x') as output:
        output.write(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))
    return 0 if result['verdict'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())
