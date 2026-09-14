"""Check guarded wait accounting; separately require persona, source/seal and strict log proofs."""
import argparse
import json
from pathlib import Path
import re
from guest_save_check import field, one, require
from personas import persona_matrix


def judge(rows, requested):
    require(requested and all(type(n) is int and 1 <= n <= 10000 for n in requested),
            'expected wait counts must be within 1..10000')
    waits = []
    phase, current, progress = 'idle', None, 0
    for index, (event, outcome, detail) in enumerate(rows):
        require(outcome == 'OK', 'journal contains a refusal')
        if event not in ('advance-guard', 'advance', 'advance-progress', 'advance-complete'):
            continue
        if event == 'advance-guard' and detail.startswith('start; '):
            require(phase == 'idle' and len(waits) < len(requested), 'extra or overlapping wait')
            require(field(detail, 'guard') == 'ignoreme-armed' and field(detail, 'ignoreMe') == 'True',
                    'advance guard did not arm')
            require(field(detail, 'walk') == 'none' and field(detail, 'scope') == 'every-scripted-advance',
                    'advance guard scope differs')
            current = dict(start=index, requested=requested[len(waits)],
                           cell=field(detail, 'founderCell'), zone=field(detail, 'zone'))
            require(current['cell'] != 'absent' and current['zone'] != 'absent', 'guard lacks a live founder')
            phase, progress = 'armed', 0
        elif event == 'advance':
            require(phase == 'armed', 'advance lacks its single start guard')
            match = re.fullmatch(r'Advancing ([1-9][0-9]*) game turn\(s\) with no player input\. '
                                 r'A advance-progress row lands every 100 turns and the script resumes '
                                 r'at its next verb when the wait completes\.', detail)
            require(match and int(match[1]) == current['requested'], 'requested wait sequence differs')
            phase = 'running'
        elif event == 'advance-progress':
            require(phase == 'running', 'progress outside an active wait')
            progress += 100
            require(progress <= current['requested'] and
                    detail == f"{progress} of {current['requested']} turn(s) elapsed",
                    'missing, repeated or malformed progress')
        elif event == 'advance-guard':
            require(phase == 'running' and detail.startswith('end; '), 'guard release out of order')
            require(progress == current['requested'] // 100 * 100, 'wait progress incomplete')
            expected = dict(founderCell=current['cell'], zone=current['zone'], guard='ignoreme-released',
                            ignoreMe='False', walk='none', scope='every-scripted-advance')
            require(all(field(detail, key) == value for key, value in expected.items()),
                    'founder changed cell/zone or guard was not restored to this profile default')
            phase = 'released'
        else:
            require(phase == 'released', 'completion lacks its single released guard')
            match = re.fullmatch(r'([1-9][0-9]*) turn\(s\) elapsed of ([1-9][0-9]*) requested', detail)
            require(match and int(match[2]) == current['requested'] and int(match[1]) >= int(match[2]),
                    'ordinary wait was shortened or malformed')
            current.update(complete=index, elapsed=int(match[1]))
            waits.append(current)
            phase = 'idle'
    require(phase == 'idle' and len(waits) == len(requested), 'ordinary waits incomplete')
    return waits


def bind_chain_clocks(rows, waits):
    """The chain observes Game.Turns between each wait; setup is the first clock anchor."""
    anchors = [(index, int(field(detail, 'turns'))) for index, (event, _, detail) in enumerate(rows)
               if event in ('camp-heart-chain-setup', 'camp-heart-chain-supply', 'camp-heart-chain-check')]
    require(len(anchors) == 8, 'chain lacks its eight world clock anchors')
    for (before, start), (after, end) in zip(anchors, anchors[1:]):
        elapsed = sum(wait['elapsed'] for wait in waits if before < wait['complete'] < after)
        require(end - start == elapsed, 'chain world clock differs from actual elapsed ordinary waits')
    return dict(requestedTurns=sum(w['requested'] for w in waits),
                elapsedTurns=sum(w['elapsed'] for w in waits), waits=waits,
                chainStartTurns=anchors[0][1], chainEndTurns=anchors[-1][1])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('journal', type=Path)
    parser.add_argument('--requested', type=int, nargs='+', required=True)
    parser.add_argument('--chain-clocks', action='store_true')
    parser.add_argument('--results', type=Path, required=True)
    args = parser.parse_args()
    try:
        rows = persona_matrix.read_journal(args.journal.read_text(encoding='utf-8-sig'))
        require(persona_matrix.terminal_row(rows) == 'SCRIPT-COMPLETE', 'script did not complete')
        terminal, _ = one(rows, 'SCRIPT-COMPLETE')
        require(terminal == len(rows) - 1, 'observations continue after completion')
        waits = judge(rows, args.requested)
        proof = bind_chain_clocks(rows, waits) if args.chain_clocks else dict(waits=waits)
        result = dict(verdict='PASS', **proof,
                      scope='Wait accounting only; separately require persona, closed profiles, source bindings and strict logs.')
    except (OSError, ValueError) as error:
        result = dict(verdict='FAIL', reason=str(error))
    with args.results.open('x') as output:
        output.write(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))
    return 0 if result['verdict'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())
