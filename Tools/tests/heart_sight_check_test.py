import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import heart_sight_check as check


class HeartSightCheckTests(unittest.TestCase):
    def fixture(self):
        physical = ("completed=true; canvas=7; canvas-cells=1,0|2,0|3,0|4,0|0,1|5,1|0,2; layout=6x4; "
                    "roof=open; enclosed=false; realized-sha256=" + "a" * 64
                    + "; heart=123; synthetic-completion=false; world-repair=false")
        sight = ("whole-zone-drawn=true; cells=2000; frames=4; naturally-hidden-cells=810; "
                 "gameplay-sight-restored=true; synthetic-visibility=false")
        stamp = "; same-physical-heart=true; receipt-sha256=" + "b" * 64
        source = [("heart-site-open", "OK", "initial-canvas=4; synthetic-completion=false"),
                  ("advance", "OK", "8400"), ("lifecycle-grown", "OK", "grown"),
                  ("heart-sight-inspect", "OK", physical), ("yield-frames-complete", "OK", "4"),
                  ("heart-sight-check", "OK", sight + "; witness=recorded; receipt-sha256=" + "b" * 64),
                  ("lifecycle-save", "OK", "saved"), ("SCRIPT-COMPLETE", "OK", "done")]
        loaded = [("heart-load-preactivation", "OK", physical + stamp + "; before-AfterGameLoaded=true"),
                  ("heart-load-verified", "OK", physical + stamp), ("lifecycle-loaded", "OK", "loaded"),
                  ("lifecycle-next", "OK", "paid"), ("heart-load-resume", "OK",
                   "vanilla-Continue=true; session-backup=false; saved-script-considered=true; requested-frames=3"),
                  ("yield-frames-complete", "OK", "4"),
                  ("heart-load-rendered", "OK", sight + "; " + physical + stamp + "; new-game-script-replayed=false"),
                  ("SCRIPT-COMPLETE", "OK", "real-loaded-frames=true")]
        return source, loaded

    def test_complete_source_and_loaded_frames(self):
        result = check.judge(*self.fixture())
        self.assertEqual(("PASS", "123", 4, 4, False),
                         tuple(result[k] for k in ("verdict", "heartId", "sourceFrames", "loadedFrames", "enclosed")))

    def test_missing_repeated_or_reordered_observations_refuse(self):
        for side in (0, 1):
            for index in range(len(self.fixture()[side])):
                for mode in ("remove", "duplicate", "reorder"):
                    rows = self.fixture()
                    if mode == "remove": del rows[side][index]
                    elif mode == "duplicate": rows[side].insert(index, rows[side][index])
                    elif index == 0: continue
                    else: rows[side][index - 1], rows[side][index] = rows[side][index], rows[side][index - 1]
                    with self.subTest(side=side, index=index, mode=mode), self.assertRaises(ValueError):
                        check.judge(*rows)

    def test_physical_and_render_drift_refuses_at_each_loaded_phase(self):
        for index in (0, 1, 6):
            for old, new in (("canvas=7", "canvas=4"), ("0,2", "5,1"), ("heart=123", "heart=124"),
                             ("a" * 64, "c" * 64), ("b" * 64, "c" * 64), ("layout=6x4", "layout=4x4"),
                             ("enclosed=false", "enclosed=true"), ("world-repair=false", "world-repair=true"),
                             ("synthetic-completion=false", "synthetic-completion=true")):
                source, loaded = self.fixture()
                event, outcome, detail = loaded[index]
                loaded[index] = event, outcome, detail.replace(old, new)
                with self.subTest(index=index, old=old), self.assertRaises(ValueError): check.judge(source, loaded)

    def test_frames_must_be_real_whole_zone_occluded_and_restore_gameplay(self):
        for side, index in ((0, 5), (1, 6)):
            for old, new in (("frames=4", "frames=2"), ("frames=4", "frames=-4"), ("cells=2000", "cells=1999"),
                             ("naturally-hidden-cells=810", "naturally-hidden-cells=0"),
                             ("synthetic-visibility=false", "synthetic-visibility=true"),
                             ("gameplay-sight-restored=true", "gameplay-sight-restored=false"),
                             ("whole-zone-drawn=true", "whole-zone-drawn=false")):
                rows = self.fixture()
                event, outcome, detail = rows[side][index]
                rows[side][index] = event, outcome, detail.replace(old, new)
                with self.subTest(side=side, old=old), self.assertRaises(ValueError): check.judge(*rows)

    def test_parked_or_replayed_load_cannot_pass(self):
        for index, old, new in ((4, "saved-script-considered=true", "saved-script-considered=false"),
                                (4, "vanilla-Continue=true", "vanilla-Continue=false"),
                                (6, "new-game-script-replayed=false", "new-game-script-replayed=true"),
                                (7, "real-loaded-frames=true", "real-loaded-frames=false")):
            source, loaded = self.fixture()
            event, outcome, detail = loaded[index]
            loaded[index] = event, outcome, detail.replace(old, new)
            with self.subTest(old=old), self.assertRaises(ValueError): check.judge(source, loaded)

    def test_any_refusal_or_duplicate_field_refuses(self):
        for side in (0, 1):
            rows = self.fixture()
            rows[side].insert(0, ("unexpected", "REFUSED", "failed"))
            with self.assertRaises(ValueError): check.judge(*rows)
        source, loaded = self.fixture()
        event, outcome, detail = loaded[6]
        loaded[6] = event, outcome, detail + "; frames=4"
        with self.assertRaises(ValueError): check.judge(source, loaded)


if __name__ == "__main__": unittest.main()
