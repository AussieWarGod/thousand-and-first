// UploadSubmissionRecordRaceTests - the "race" group of UploadSubmissionRecordTests, kept in its own file
// because the sequential harness is already at its line ceiling. Bounded and deterministic in shape: every
// scenario starts two real LongRunning worker threads that meet at a Barrier(3) with the caller, so both
// hit the latch in the same instant, and every wait is capped at 5 s; there is no unbounded spinning, no
// clock, no file and no SDK. Four scenarios: settle/settle (root's oracle, 32 trials), callback/callback
// with DIFFERENT facts (32), callback/settle for both an Unknown and a Rejected settler (32 each), and a
// reader that hammers every getter 1000 times while a transition runs (16). Each scenario reports a small
// number of aggregate Checks over its trials, so a single interleaving failure fails the group.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ThousandAndFirst.WorkshopSteam.Evidence
{
    public static class UploadSubmissionRecordRaceTests
    {
        private const string AttemptSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string PlanSha = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
        private const string ReceiptSha = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
        private const ulong Item = 3794797472UL;
        private const string Version = "0.3.0";
        private const string Stamp = "2026-09-06T14:20:31.1234567Z", Later = "2026-09-06T14:25:00.0000000Z";
        private const string Quiet = "no_callback_requires_unknown_or_timedout";
        private const int Trials = 32, ReaderTrials = 16, Reads = 1000;
        /// <summary>Runs every race scenario into the shared "race" group of the sequential harness.</summary>
        public static void Race()
        {
            UploadSubmissionRecordTests.Group("race");
            SettleSettle();
            CallbackCallback();
            CallbackSettleUnknown();
            CallbackSettleRejected();
            Readers();
        }
        private static UploadSubmissionRecord Fresh()
        {
            UploadSubmissionRecord record; string refusal;
            if (!UploadSubmissionRecord.TryCreate(AttemptSha, null, Item, Version, PlanSha, ReceiptSha, Stamp, out record, out refusal))
                throw new InvalidOperationException("race fixture: " + refusal);
            return record;
        }
        /// <summary>Starts two LongRunning lanes that meet the caller at a Barrier(3) and then both call the
        /// latch. Returns false if the rendezvous or the join did not complete inside 5 s, so a stalled trial
        /// is reported rather than hanging the harness.</summary>
        private static bool Run2(Action<int> lane)
        {
            bool finished;
            using (Barrier start = new Barrier(3))
            {
                Task[] workers = new Task[2];
                for (int i = 0; i < workers.Length; i++)
                {
                    int slot = i;
                    workers[slot] = Task.Factory.StartNew(delegate
                    {
                        if (!start.SignalAndWait(TimeSpan.FromSeconds(5))) throw new TimeoutException("race barrier");
                        lane(slot);
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }
                finished = start.SignalAndWait(TimeSpan.FromSeconds(5)) && Task.WaitAll(workers, 5000);
            }
            return finished;
        }
        /// <summary>Root's oracle: two settles of the same Unknown outcome. Both must return true with the
        /// SAME observation instance, which must also be the record's, with one stable wire hash.</summary>
        private static void SettleSettle()
        {
            int conflicts = 0, refused = 0, stalled = 0;
            for (int trial = 0; trial < Trials; trial++)
            {
                UploadSubmissionRecord record = Fresh();
                ReleaseSubmissionObservation[] seen = new ReleaseSubmissionObservation[2];
                bool[] ok = new bool[2];
                string[] refusals = new string[2];
                if (!Run2(delegate(int slot)
                {
                    ok[slot] = record.TrySettle(UploadCompletion.Unknown, Stamp, "lane" + slot, out seen[slot], out refusals[slot]);
                })) { stalled++; continue; }
                if (!ok[0] || !ok[1] || refusals[0] != null || refusals[1] != null) refused++;
                ReleaseSubmissionObservation snap = record.Settlement;
                string sha = record.SettlementSha();
                if (snap == null || sha == null || !ReferenceEquals(seen[0], seen[1]) || !ReferenceEquals(seen[0], snap)
                    || sha != record.SettlementSha() || record.Completion != UploadCompletion.Unknown
                    || (snap.Note != "lane0" && snap.Note != "lane1")) conflicts++;
            }
            UploadSubmissionRecordTests.Check("settle-settle-one-instance", conflicts == 0,
                conflicts + " of " + Trials + " trials disagreed on the settlement instance or its hash");
            UploadSubmissionRecordTests.Check("settle-settle-both-true", refused == 0, refused + " trials refused a settle");
            UploadSubmissionRecordTests.Check("settle-settle-bounded", stalled == 0, stalled + " trials did not finish inside 5s");
        }
        /// <summary>Two callbacks with DIFFERENT facts. Exactly one wins; the loser is refused
        /// "callback_conflict" and the latched tuple is the winner's, whole, never a mixture of the two.</summary>
        private static void CallbackCallback()
        {
            int wrongWinners = 0, wrongRefusals = 0, mixed = 0, stalled = 0;
            for (int trial = 0; trial < Trials; trial++)
            {
                UploadSubmissionRecord record = Fresh();
                bool[] ok = new bool[2];
                string[] refusals = new string[2];
                if (!Run2(delegate(int slot)
                {
                    if (slot == 0) ok[0] = record.TryRecordCallback(1, Item, false, false, out refusals[0]);
                    else ok[1] = record.TryRecordCallback(2, 0UL, true, false, out refusals[1]);
                })) { stalled++; continue; }
                if ((ok[0] ? 1 : 0) + (ok[1] ? 1 : 0) != 1) wrongWinners++;
                int loser = ok[0] ? 1 : 0;
                if (refusals[loser] != "callback_conflict" || refusals[1 - loser] != null) wrongRefusals++;
                bool laneZero = record.CallbackObserved && record.RawEResult == 1 && record.ReturnedItem == Item
                    && record.IoFailure == false && record.LegalAgreementRequired == false;
                bool laneOne = record.CallbackObserved && record.RawEResult == 2 && record.ReturnedItem == 0UL
                    && record.IoFailure == true && record.LegalAgreementRequired == false;
                if (laneZero == laneOne || (ok[0] && !laneZero) || (ok[1] && !laneOne) || record.Settled) mixed++;
            }
            UploadSubmissionRecordTests.Check("callback-callback-one-winner", wrongWinners == 0, wrongWinners + " trials latched twice or not at all");
            UploadSubmissionRecordTests.Check("callback-callback-loser-conflict", wrongRefusals == 0, wrongRefusals + " trials refused with the wrong reason");
            UploadSubmissionRecordTests.Check("callback-callback-whole-tuple", mixed == 0, mixed + " trials show a mixed or non-winning tuple");
            UploadSubmissionRecordTests.Check("callback-callback-bounded", stalled == 0, stalled + " trials did not finish inside 5s");
        }
        /// <summary>A callback races an explicit pump-fault Unknown settle. Exactly two outcomes are legal:
        /// (a) the settle won, so the terminal snapshot is UNOBSERVED and the callback is refused
        /// "already_settled"; (b) the callback won, so the whole tuple is latched and the Unknown settle
        /// carries it. Nothing between the two - never observed with a null fact - may ever be seen.</summary>
        private static void CallbackSettleUnknown()
        {
            int illegal = 0, partials = 0, stalled = 0, settleFirstCount = 0, callbackFirstCount = 0;
            for (int trial = 0; trial < Trials; trial++)
            {
                UploadSubmissionRecord record = Fresh();
                bool callbackOk = false, settleOk = false;
                string callbackRefusal = null, settleRefusal = null;
                ReleaseSubmissionObservation observation = null;
                if (!Run2(delegate(int slot)
                {
                    if (slot == 0) callbackOk = record.TryRecordCallback(2, 0UL, false, false, out callbackRefusal);
                    else settleOk = record.TrySettle(UploadCompletion.Unknown, Stamp, "race", out observation, out settleRefusal);
                })) { stalled++; continue; }
                ReleaseSubmissionObservation snap = record.Settlement;
                bool settleFirst = !callbackOk && callbackRefusal == "already_settled" && settleOk && settleRefusal == null
                    && snap != null && ReferenceEquals(snap, observation) && !snap.CallbackObserved
                    && snap.RawEResult == null && !record.CallbackObserved && record.RawEResult == null
                    && record.Completion == UploadCompletion.Unknown;
                bool callbackFirst = callbackOk && callbackRefusal == null && settleOk && settleRefusal == null
                    && snap != null && ReferenceEquals(snap, observation) && snap.CallbackObserved
                    && snap.RawEResult == 2 && snap.ReturnedItem == "0"
                    && snap.IoFailure == false && snap.LegalAgreementRequired == false && record.CallbackObserved
                    && record.RawEResult == 2 && record.ReturnedItem == 0UL && record.IoFailure == false
                    && record.LegalAgreementRequired == false && record.Completion == UploadCompletion.Unknown;
                if (settleFirst == callbackFirst) illegal++;
                else if (settleFirst) settleFirstCount++;
                else callbackFirstCount++;
                if (record.CallbackObserved != (record.RawEResult != null)
                    || (record.RawEResult == null) != (record.ReturnedItem == null)
                    || (record.IoFailure == null) != (record.LegalAgreementRequired == null)
                    || (record.Settled && snap == null) || (record.Settled && record.Completion == null)) partials++;
            }
            UploadSubmissionRecordTests.Check("callback-settle-unknown-one-legal-outcome", illegal == 0,
                illegal + " trials matched neither legal outcome; settle-first=" + settleFirstCount + " callback-first=" + callbackFirstCount);
            UploadSubmissionRecordTests.Check("callback-settle-unknown-no-partial-tuple", partials == 0, partials + " trials exposed a partial tuple");
            UploadSubmissionRecordTests.Check("callback-settle-unknown-bounded", stalled == 0, stalled + " trials did not finish inside 5s");
        }
        /// <summary>The same race with a Rejected settler, where only one order can settle. If the callback
        /// won, the classifier agrees (raw 2 is Rejected) and the settle stands. If the settle ran first it
        /// is refused for having no callback and the record stays UNSETTLED; the callback then lands and a
        /// second Rejected settle succeeds. Either way the final state is one classified settlement.</summary>
        private static void CallbackSettleRejected()
        {
            int illegal = 0, stalled = 0, repaired = 0;
            for (int trial = 0; trial < Trials; trial++)
            {
                UploadSubmissionRecord record = Fresh();
                bool callbackOk = false, settleOk = false;
                string callbackRefusal = null, settleRefusal = null;
                ReleaseSubmissionObservation observation = null;
                if (!Run2(delegate(int slot)
                {
                    if (slot == 0) callbackOk = record.TryRecordCallback(2, Item, false, false, out callbackRefusal);
                    else settleOk = record.TrySettle(UploadCompletion.Rejected, Stamp, "race", out observation, out settleRefusal);
                })) { stalled++; continue; }
                bool consistent;
                if (settleOk)
                {
                    ReleaseSubmissionObservation snap = record.Settlement;
                    consistent = callbackOk && callbackRefusal == null && settleRefusal == null && record.Settled
                        && snap != null && ReferenceEquals(snap, observation) && snap.CallbackObserved
                        && snap.RawEResult == 2 && snap.ReturnedItem == "3794797472"
                        && record.Completion == UploadCompletion.Rejected;
                }
                else
                {
                    ReleaseSubmissionObservation second; string secondRefusal;
                    bool refusedQuietly = settleRefusal == Quiet && observation == null && !record.Settled
                        && callbackOk && callbackRefusal == null && record.RawEResult == 2 && record.ReturnedItem == Item;
                    bool again = record.TrySettle(UploadCompletion.Rejected, Later, "after the callback landed", out second, out secondRefusal);
                    consistent = refusedQuietly && again && secondRefusal == null && second != null && second.CallbackObserved
                        && second.RawEResult == 2 && record.Completion == UploadCompletion.Rejected
                        && ReferenceEquals(second, record.Settlement);
                    if (consistent) repaired++;
                }
                if (!consistent) illegal++;
            }
            UploadSubmissionRecordTests.Check("callback-settle-rejected-consistent", illegal == 0,
                illegal + " trials ended inconsistent; settle-refused-then-repaired=" + repaired);
            UploadSubmissionRecordTests.Check("callback-settle-rejected-bounded", stalled == 0, stalled + " trials did not finish inside 5s");
        }
        /// <summary>A reader hammers every getter while the other lane makes the one transition. It must
        /// never see CallbackObserved true with a null fact, nor Settled true with a null Settlement or
        /// Completion. Alternating trials race the reader against a settle and against a callback.</summary>
        private static void Readers()
        {
            int violations = 0, stalled = 0;
            for (int trial = 0; trial < ReaderTrials; trial++)
            {
                UploadSubmissionRecord record = Fresh();
                bool settleLane = (trial % 2) == 0;
                int[] bad = new int[1];
                if (!Run2(delegate(int slot)
                {
                    if (slot == 0)
                    {
                        ReleaseSubmissionObservation observation; string refusal;
                        if (settleLane) record.TrySettle(UploadCompletion.TimedOut, Stamp, "reader race", out observation, out refusal);
                        else record.TryRecordCallback(1, Item, false, false, out refusal);
                    }
                    else
                        for (int i = 0; i < Reads; i++)
                        {
                            bool observed = record.CallbackObserved;
                            int? raw = record.RawEResult;
                            ulong? returned = record.ReturnedItem;
                            bool? io = record.IoFailure;
                            bool? legal = record.LegalAgreementRequired;
                            bool settled = record.Settled;
                            ReleaseSubmissionObservation snap = record.Settlement;
                            UploadCompletion? completion = record.Completion;
                            if (observed && (raw == null || returned == null || io == null || legal == null)) bad[0] = 1;
                            if (settled && (snap == null || completion == null)) bad[0] = 1;
                        }
                })) { stalled++; continue; }
                if (bad[0] != 0) violations++;
            }
            UploadSubmissionRecordTests.Check("reader-never-sees-partial-state", violations == 0,
                violations + " of " + ReaderTrials + " reader trials saw an observed-but-empty or settled-but-null read");
            UploadSubmissionRecordTests.Check("reader-race-bounded", stalled == 0, stalled + " trials did not finish inside 5s");
        }
    }
}
