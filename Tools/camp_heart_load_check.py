#!/usr/bin/env python3
"""Paid camp save/load journal proof; also require closed profiles, source bindings and strict logs."""
import argparse
import json
from pathlib import Path
import re
from xml.etree import ElementTree
from guest_save_check import field, one, require
from personas import persona_matrix

CATALOGUE = Path(__file__).resolve().parents[1] / 'RuntimeData' / 'KingdomBuildings.xml'


def catalogue_brush(text):
    """Brush units in one authored bill; the catalogue spells brush 'canvas' (KingdomMaterialRules)."""
    total = 0
    for term in (text or '').split(','):
        name, _, units = term.partition(':')
        if name.strip().lower() in ('canvas', 'brush'):
            total += int(units)
    return total


def saved_brush(catalogue=CATALOGUE):
    """Sentinel brush the paid camp tent leaves (#282): one fewer than the tent's own upgrade asks
    for, as Harness/KingdomCampHeartTentRules.cs derives it, read from the catalogue."""
    try:
        root = ElementTree.parse(catalogue).getroot()
    except ElementTree.ParseError as error:
        raise ValueError('catalogue unreadable: ' + str(error)) from error
    tents = [b for b in root.iter('building') if b.get('Key') == 'tent']
    require(len(tents) == 1, 'catalogue lacks exactly one tent design')
    tent_brush = catalogue_brush(tents[0].get('Materials'))
    upgrade_brush = catalogue_brush(tents[0].get('UpgradeMaterials'))
    require(tent_brush > 0 and upgrade_brush >= 2, 'catalogue tent bill leaves no lawful sentinel brush')
    return upgrade_brush - 1


def equal_fields(detail, expected):
    for name, value in expected.items():
        require(field(detail, name) == value, 'camp invariant differs: ' + name)


def judge(source, loaded, brush=None):
    brush = str(saved_brush() if brush is None else brush)
    for rows in (source, loaded):
        require(persona_matrix.terminal_row(rows) == 'SCRIPT-COMPLETE', 'session did not complete')
        require(all(outcome == 'OK' for _, outcome, _ in rows), 'session contains a refusal')
        terminal, _ = one(rows, 'SCRIPT-COMPLETE')
        require(terminal == len(rows) - 1, 'observations continue after completion')
    setup, _ = one(source, 'camp-heart-setup')
    custody_at, custody = one(source, 'camp-heart-save-custody')
    saved, save = one(source, 'camp-heart-save')
    checks = [(i, detail) for i, (event, _, detail) in enumerate(source) if event == 'camp-heart-check']
    require(len(checks) == 3 and setup < checks[0][0] < checks[1][0] < checks[2][0] < custody_at < saved,
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
    equal_fields(save, {'paid-camp-save': 'true', 'rung': '2', 'synthetic-next-job-timber': '1', 'brush': brush})
    identities = {name: field(save, name) for name in ('heart', 'store', 'fire')}
    equal_fields(custody, {'store': identities['store'], 'before': 'r_KingdomBrush=' + brush,
                           'after': 'r_KingdomBrush=' + brush + ',r_KingdomTimber=1'})
    require(field(custody, 'added-timber') not in identities.values(), 'new timber reuses a camp object')
    require(len(set(identities.values())) == 3, 'camp object identities collide')
    tent_job = field(save, 'tent-job')
    ticks = field(save, 'time-ticks')
    require(re.fullmatch('0|[1-9][0-9]*', ticks), 'saved world clock malformed')
    digest = field(save, 'snapshot-sha256')
    require(re.fullmatch('[0-9a-f]{64}', digest), 'camp snapshot digest malformed')
    game = field(save, 'save')
    require(re.fullmatch('[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}', game), 'saved game id malformed')
    names = ('LOAD-BEGIN', 'camp-heart-preactivation', 'camp-heart-loaded', 'camp-heart-next', 'camp-heart-resume',
             'advance-complete', 'camp-heart-completed', 'SCRIPT-COMPLETE')
    observations = [one(loaded, name) for name in names]
    require([at for at, _ in observations] == sorted(at for at, _ in observations), 'loaded phases out of order')
    begin, preactivation, restored, paid, resumed, elapsed, completed, terminal = [detail for _, detail in observations]
    require(not any(event in ('camp-heart-setup', 'camp-heart-check', 'camp-heart-save', 'stagedigest')
                    for event, _, _ in loaded), 'source script replayed in loaded process')
    equal_fields(begin, {'game-id': game, 'new-game': 'false', 'mod-restore': 'false'})
    saved_fields = dict(identities, **{'snapshot-sha256': digest, 'tent-job': tent_job, 'time-ticks': ticks})
    equal_fields(preactivation, dict(saved_fields, **{'before-AfterGameLoaded': 'true'}))
    equal_fields(restored, dict(saved_fields, rung='2', basin='48', brush=brush, timber='1'))
    equal_fields(paid, {'water-debited': '2', 'timber-debited': '1', 'synthetic-materials-after-load': '0'})
    next_job, upgrade = field(paid, 'new-job'), field(paid, 'upgrade-job')
    require(len({next_job, upgrade, tent_job}) == 3, 'new job, upgrade and paid tent identities collide')
    equal_fields(resumed, {'vanilla-Continue': 'true', 'saved-script-considered': 'true', 'requested-turns': '3600'})
    require(elapsed == '3600 turn(s) elapsed of 3600 requested', 'loaded ordinary wait incomplete')
    equal_fields(completed, dict(identities, phase='Complete', brush=brush, **{'new-job': next_job, 'effects-settled': 'true'}))
    output = field(completed, 'output')
    require(output not in identities.values(), 'completed next output reuses existing camp object')
    require(re.fullmatch('[1-9][0-9]*', field(completed, 'turns')), 'completed game clock absent')
    equal_fields(terminal, {'real-save-quit-load': 'true', 'next-paid-job-complete': 'true',
                           'new-game-script-replayed': 'false', 'ordinary-acceptance': 'false'})
    return dict(verdict='PASS', gameId=game, snapshotSha256=digest, newJobId=next_job, outputId=output,
                **identities, sourceTurns=6000, loadedTurns=3600, brush=int(brush),
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
