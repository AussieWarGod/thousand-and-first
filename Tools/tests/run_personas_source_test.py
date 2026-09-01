"""Lifecycle contracts for the native persona runner.

The licensed game is not available to portable CI. These source-order tests pin the shell runner's
evidence boundary; journal semantics remain executable in persona_matrix_test.py.
"""

from __future__ import annotations

import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = (ROOT / "Tools" / "run-personas.sh").read_text(encoding="utf-8")
START = SOURCE.index("run_persona() {")
END = SOURCE.index("# ---- the matrix", START)
RUN_PERSONA = SOURCE[START:END]


class PersonaRunnerEvidenceSourceTest(unittest.TestCase):
    def test_live_archive_and_assert_precede_capture_and_stop(self):
        archive = RUN_PERSONA.index('archive_file "$journal" "$archived_journal"')
        log_check = RUN_PERSONA.index('"$LOG_CHECK" "$archived_player_log"')
        assertion = RUN_PERSONA.index('python3 "$MATRIX" assert')
        pass_capture = RUN_PERSONA.index(
            'if [ "$VERDICT" = PASS ] && [ -n "$CAPTURE_DIR" ]'
        )
        capture = RUN_PERSONA.index('-File "$(wslpath -w "$CAPTURE")"', pass_capture)
        stop = RUN_PERSONA.index("\n\tstop_game", capture)
        self.assertLess(archive, assertion)
        self.assertLess(archive, log_check)
        self.assertLess(log_check, assertion)
        self.assertLess(assertion, pass_capture)
        self.assertLess(pass_capture, capture)
        self.assertLess(capture, stop)
        self.assertIn(
            'python3 "$MATRIX" assert "$(persona_path "$persona")" \\\n\t\t"$archived_journal"',
            RUN_PERSONA,
        )

    def test_every_persona_requires_an_archived_clean_taf_log(self):
        archive = RUN_PERSONA.index('archive_file "$player_log" "$archived_player_log"')
        checker = RUN_PERSONA.index('"$LOG_CHECK" "$archived_player_log"', archive)
        assertion = RUN_PERSONA.index('python3 "$MATRIX" assert', checker)
        self.assertLess(archive, checker)
        self.assertLess(checker, assertion)
        self.assertIn('live Player.log is absent', RUN_PERSONA)
        self.assertIn('Player.log rejected:', RUN_PERSONA)

    def test_failed_assertion_or_capture_cannot_replace_a_good_png(self):
        pass_capture = RUN_PERSONA.index(
            'if [ "$VERDICT" = PASS ] && [ -n "$CAPTURE_DIR" ]'
        )
        publish = RUN_PERSONA.index(
            'mv -f -- "$capture_temp" "$capture_target"', pass_capture
        )
        png_check = RUN_PERSONA.index("89504e470d0a1a0a", pass_capture)
        self.assertLess(png_check, publish)
        self.assertNotIn('rm -f -- "$capture_target"', RUN_PERSONA)
        self.assertNotIn('cp -f -- "$capture_temp" "$capture_target"', RUN_PERSONA)

    def test_seed_is_optional_prepare_argument_two(self):
        self.assertIn('prepare_args=("$root")', RUN_PERSONA)
        self.assertIn('prepare_args+=("$TAF_PERSONA_SEED")', RUN_PERSONA)
        self.assertIn('"$PREPARE" "${prepare_args[@]}"', RUN_PERSONA)

    def test_timeout_retry_keeps_distinct_archives_and_logs(self):
        self.assertIn('artifact="$persona-retry$attempt"', RUN_PERSONA)
        self.assertIn('journal-$artifact.tsv', RUN_PERSONA)
        self.assertIn('player-$artifact.log', RUN_PERSONA)
        matrix = SOURCE[END:]
        self.assertIn('run_persona "$persona" 1', matrix)
        self.assertIn('run_persona "$persona" 2', matrix)


if __name__ == "__main__":
    unittest.main()
