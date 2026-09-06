// UploadSubmissionRecordTests - standalone, SDK-free, in-memory harness for the single-attempt
// submission latch. No file, network, clock, Steam SDK or test framework: every stamp is a literal.
// Run() returns the failure count so a caller can host it; Main forwards it as a process code and
// ignores its arguments. Six groups: identity (construction refusals, and the three DISTINCT hashes
// landing in their own observation fields), callback (first-latch fidelity across the Int32 extremes,
// returned 0 and wrong ids, duplicate and conflicting replays, a callback after settlement), settle
// (no-callback outcomes, classifier agreement including legal-before-IO, the pump-fault Unknown, a
// refusal leaving the record unsettled, repeated settles returning the SAME instance), roundtrip (the
// settled wire decodes and its hash is stable), no-authority (no public member name or signature
// claiming archive, retry, delivery, release, publication, IO or clock authority) and race, which lives
// in UploadSubmissionRecordRaceTests.cs and is run from Run() below.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace ThousandAndFirst.WorkshopSteam.Evidence
{
    public static class UploadSubmissionRecordTests
    {
        private const string AttemptSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string PlanSha = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
        private const string ReceiptSha = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
        private const string PreviousSha = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        private const ulong Item = 3794797472UL;
        private const string ItemText = "3794797472", Version = "0.3.0";
        private const string Stamp = "2026-09-06T14:20:31.1234567Z", Later = "2026-09-06T14:25:00.0000000Z";
        private const string Quiet = "no_callback_requires_unknown_or_timedout", Says = "completion_mismatch_classifier_says_";
        private static readonly List<string> Names = new List<string>();
        private static readonly List<int> Counts = new List<int>(), Fails = new List<int>();
        internal static void Group(string name) { Names.Add(name); Counts.Add(0); Fails.Add(0); }
        internal static void Check(string name, bool ok, string detail)
        {
            int i = Names.Count - 1;
            Counts[i] = Counts[i] + 1;
            if (!ok) { Fails[i] = Fails[i] + 1; Console.Error.WriteLine("FAIL [" + Names[i] + "] " + name + ": " + detail); }
        }
        private static string Show(string refusal) { return refusal ?? "<null>"; }
        private static UploadSubmissionRecord Fresh()
        {
            UploadSubmissionRecord record; string refusal;
            if (!UploadSubmissionRecord.TryCreate(AttemptSha, null, Item, Version, PlanSha, ReceiptSha, Stamp, out record, out refusal))
                throw new InvalidOperationException("fixture: " + refusal);
            return record;
        }
        private static UploadSubmissionRecord Observed(int raw, ulong returned, bool io, bool legal)
        {
            UploadSubmissionRecord record = Fresh(); string refusal;
            if (!record.TryRecordCallback(raw, returned, io, legal, out refusal))
                throw new InvalidOperationException("fixture callback: " + refusal);
            return record;
        }
        private static void Identity()
        {
            Group("identity");
            object[][] rows =
            {
                new object[] { "good", AttemptSha, null, Item, Version, PlanSha, ReceiptSha, Stamp, null },
                new object[] { "previous-valid", AttemptSha, PreviousSha, Item, Version, PlanSha, ReceiptSha, Stamp, null },
                new object[] { "previous-not-a-hash", AttemptSha, "not-a-hash", Item, Version, PlanSha, ReceiptSha, Stamp, "identity_previousObservationSHA" },
                new object[] { "attempt-short", AttemptSha.Substring(1), null, Item, Version, PlanSha, ReceiptSha, Stamp, "identity_attemptSHA" },
                new object[] { "attempt-uppercase", AttemptSha.ToUpperInvariant(), null, Item, Version, PlanSha, ReceiptSha, Stamp, "identity_attemptSHA" },
                new object[] { "attempt-null", null, null, Item, Version, PlanSha, ReceiptSha, Stamp, "identity_attemptSHA" },
                new object[] { "item-zero", AttemptSha, null, 0UL, Version, PlanSha, ReceiptSha, Stamp, "identity_item_zero" },
                new object[] { "version-two-part", AttemptSha, null, Item, "1.2", PlanSha, ReceiptSha, Stamp, "identity_requestVersion" },
                new object[] { "version-leading-zero", AttemptSha, null, Item, "01.2.3", PlanSha, ReceiptSha, Stamp, "identity_requestVersion" },
                new object[] { "plan-non-hex", AttemptSha, null, Item, Version, new string('z', 64), ReceiptSha, Stamp, "identity_planSHA" },
                new object[] { "receipt-short", AttemptSha, null, Item, Version, PlanSha, ReceiptSha.Substring(0, 63), Stamp, "identity_receiptSHA" },
                new object[] { "created-no-fraction", AttemptSha, null, Item, Version, PlanSha, ReceiptSha, "2026-09-06T14:20:31Z", "identity_createdUtc" },
                new object[] { "created-with-offset", AttemptSha, null, Item, Version, PlanSha, ReceiptSha, "2026-09-06T14:20:31.1234567+00:00", "identity_createdUtc" },
                new object[] { "created-null", AttemptSha, null, Item, Version, PlanSha, ReceiptSha, null, "identity_createdUtc" }
            };
            foreach (object[] row in rows)
            {
                UploadSubmissionRecord record; string refusal; string expected = (string)row[8];
                bool ok = UploadSubmissionRecord.TryCreate((string)row[1], (string)row[2], (ulong)row[3], (string)row[4], (string)row[5], (string)row[6], (string)row[7], out record, out refusal);
                Check((string)row[0], ok == (expected == null) && refusal == expected && (record != null) == ok, "ok=" + ok + " refusal=" + Show(refusal) + " expected=" + Show(expected));
            }
            Check("hashes-distinct", AttemptSha != PlanSha && PlanSha != ReceiptSha && ReceiptSha != PreviousSha && AttemptSha != PreviousSha, "fixture hashes must differ");
            UploadSubmissionRecord linked; string linkRefusal;
            bool made = UploadSubmissionRecord.TryCreate(AttemptSha, PreviousSha, Item, Version, PlanSha, ReceiptSha, Stamp, out linked, out linkRefusal);
            Check("linked-record-made", made && linked != null, "refusal=" + Show(linkRefusal));
            Check("identities-held", made && linked.AttemptByteSha == AttemptSha && linked.PreviousObservationSha == PreviousSha && linked.Item == Item
                && linked.ItemText == ItemText && linked.RequestVersion == Version && linked.PlanSha == PlanSha
                && linked.PackageReceiptSha == ReceiptSha && linked.CreatedUtc == Stamp, "identity fields");
            ReleaseSubmissionObservation settled; string settleRefusal;
            bool done = made && linked.TrySettle(UploadCompletion.TimedOut, Later, "no callback arrived", out settled, out settleRefusal) && settled != null;
            string wire = done ? Encoding.UTF8.GetString(linked.SettlementUtf8()) : string.Empty;
            Check("attempt-sha-field", wire.Contains("\"attemptSHA\":\"" + AttemptSha + "\""), wire);
            Check("previous-sha-field", wire.Contains("\"previousObservationSHA\":\"" + PreviousSha + "\""), wire);
            Check("plan-sha-field", wire.Contains("\"planSHA\":\"" + PlanSha + "\""), wire);
            Check("receipt-sha-field", wire.Contains("\"receiptSHA\":\"" + ReceiptSha + "\""), wire);
            Check("item-field", wire.Contains("\"item\":\"" + ItemText + "\""), wire);
            Check("created-utc-is-not-observed-utc", done && linked.Settlement.ObservedUtc == Later && linked.CreatedUtc == Stamp, "the settlement stamp is the caller's");
        }
        private static void Callback()
        {
            Group("callback");
            int[] raws = { int.MinValue, -1, 0, 1, 65536, int.MaxValue };
            ulong[] returns = { 0UL, Item, Item + 1UL, ulong.MaxValue };
            foreach (int raw in raws)
            foreach (ulong returned in returns)
            foreach (bool io in new[] { false, true })
            foreach (bool legal in new[] { false, true })
            {
                UploadSubmissionRecord record = Fresh(); string refusal;
                bool ok = record.TryRecordCallback(raw, returned, io, legal, out refusal);
                Check("latch raw=" + raw + " returned=" + returned + " io=" + io + " legal=" + legal,
                    ok && refusal == null && record.CallbackObserved && record.RawEResult == raw && record.ReturnedItem == returned
                    && record.IoFailure == io && record.LegalAgreementRequired == legal && !record.Settled && record.Completion == null, "refusal=" + Show(refusal));
            }
            UploadSubmissionRecord quiet = Fresh();
            Check("unobserved-carries-nothing", !quiet.CallbackObserved && quiet.RawEResult == null && quiet.ReturnedItem == null && quiet.IoFailure == null
                && quiet.LegalAgreementRequired == null && !quiet.Settled && quiet.Settlement == null && quiet.Completion == null
                && quiet.SettlementSha() == null && quiet.SettlementUtf8() == null, "fresh record");
            UploadSubmissionRecord twice = Observed(7, 0UL, true, false); string duplicateRefusal;
            bool again = twice.TryRecordCallback(7, 0UL, true, false, out duplicateRefusal);
            Check("duplicate-refused", !again && duplicateRefusal == "callback_duplicate" && twice.RawEResult == 7 && twice.ReturnedItem == 0UL
                && twice.IoFailure == true && twice.LegalAgreementRequired == false, "refusal=" + Show(duplicateRefusal));
            object[][] conflicts =
            {
                new object[] { "raw", 8, 0UL, true, false }, new object[] { "returned", 7, Item, true, false },
                new object[] { "io", 7, 0UL, false, false }, new object[] { "legal", 7, 0UL, true, true },
                new object[] { "everything", 1, Item, false, true }
            };
            foreach (object[] row in conflicts)
            {
                UploadSubmissionRecord record = Observed(7, 0UL, true, false); string refusal;
                bool ok = record.TryRecordCallback((int)row[1], (ulong)row[2], (bool)row[3], (bool)row[4], out refusal);
                Check("conflict-" + row[0], !ok && refusal == "callback_conflict" && record.RawEResult == 7 && record.ReturnedItem == 0UL
                    && record.IoFailure == true && record.LegalAgreementRequired == false, "refusal=" + Show(refusal));
            }
            UploadSubmissionRecord timedOut = Fresh();
            ReleaseSubmissionObservation observation; string settleRefusal, lateRefusal;
            timedOut.TrySettle(UploadCompletion.TimedOut, Stamp, null, out observation, out settleRefusal);
            bool late = timedOut.TryRecordCallback(1, Item, false, false, out lateRefusal);
            Check("callback-after-settle", !late && lateRefusal == "already_settled" && !timedOut.CallbackObserved && observation != null
                && !timedOut.Settlement.CallbackObserved && timedOut.RawEResult == null, "refusal=" + Show(lateRefusal));
            UploadSubmissionRecord shipped = Observed(1, Item, false, false);
            ReleaseSubmissionObservation shippedObservation; string shippedRefusal, replayRefusal;
            shipped.TrySettle(UploadCompletion.Ok, Stamp, null, out shippedObservation, out shippedRefusal);
            bool replay = shipped.TryRecordCallback(2, 0UL, true, true, out replayRefusal);
            Check("replay-after-settle", !replay && replayRefusal == "already_settled" && shippedRefusal == null && shipped.RawEResult == 1
                && shipped.ReturnedItem == Item && shipped.IoFailure == false && shipped.LegalAgreementRequired == false, "refusal=" + Show(replayRefusal));
        }
        private static void Settle()
        {
            Group("settle");
            object[][] quiet =
            {
                new object[] { UploadCompletion.Unknown, null }, new object[] { UploadCompletion.TimedOut, null },
                new object[] { UploadCompletion.Ok, Quiet }, new object[] { UploadCompletion.Rejected, Quiet },
                new object[] { UploadCompletion.LegalAgreementRequired, Quiet }, new object[] { UploadCompletion.IoFailure, Quiet }
            };
            foreach (object[] row in quiet)
            {
                UploadCompletion requested = (UploadCompletion)row[0]; string expected = (string)row[1];
                UploadSubmissionRecord record = Fresh();
                ReleaseSubmissionObservation observation; string refusal;
                bool ok = record.TrySettle(requested, Stamp, "no callback within the window", out observation, out refusal);
                Check("no-callback-" + requested, ok == (expected == null) && refusal == expected && record.Settled == ok && (observation != null) == ok
                    && (!ok || (!observation.CallbackObserved && observation.RawEResult == null && observation.ReturnedItem == null
                        && observation.IoFailure == null && observation.LegalAgreementRequired == null && record.Completion == requested)), "refusal=" + Show(refusal));
            }
            object[][] observed =
            {
                new object[] { "clean-ok", 1, Item, false, false, UploadCompletion.Ok, null },
                new object[] { "ok-wrong-id", 1, Item + 1UL, false, false, UploadCompletion.Ok, Says + "Unknown" },
                new object[] { "ok-zero-id", 1, 0UL, false, false, UploadCompletion.Ok, Says + "Unknown" },
                new object[] { "ok-with-io", 1, Item, true, false, UploadCompletion.Ok, Says + "IoFailure" },
                new object[] { "ok-with-legal", 1, Item, false, true, UploadCompletion.Ok, Says + "LegalAgreementRequired" },
                new object[] { "ok-raw-zero", 0, Item, false, false, UploadCompletion.Ok, Says + "Rejected" },
                new object[] { "ok-raw-max", int.MaxValue, Item, false, false, UploadCompletion.Ok, Says + "Rejected" },
                new object[] { "rejected-raw-2", 2, Item, false, false, UploadCompletion.Rejected, null },
                new object[] { "rejected-raw-min", int.MinValue, 0UL, false, false, UploadCompletion.Rejected, null },
                new object[] { "io-failure", 1, 0UL, true, false, UploadCompletion.IoFailure, null },
                new object[] { "legal-required", 1, 0UL, false, true, UploadCompletion.LegalAgreementRequired, null },
                new object[] { "legal-before-io", 1, 0UL, true, true, UploadCompletion.LegalAgreementRequired, null },
                new object[] { "io-when-legal-too", 1, 0UL, true, true, UploadCompletion.IoFailure, Says + "LegalAgreementRequired" },
                new object[] { "rejected-when-legal", 2, Item, false, true, UploadCompletion.Rejected, Says + "LegalAgreementRequired" },
                new object[] { "timedout-observed", 1, Item, false, false, UploadCompletion.TimedOut, Says + "Ok" },
                new object[] { "pump-fault-unknown", 1, Item, false, false, UploadCompletion.Unknown, null },
                new object[] { "pump-fault-unknown-with-flags", int.MinValue, ulong.MaxValue, true, true, UploadCompletion.Unknown, null }
            };
            foreach (object[] row in observed)
            {
                int raw = (int)row[1]; ulong returned = (ulong)row[2]; bool io = (bool)row[3], legal = (bool)row[4];
                UploadCompletion requested = (UploadCompletion)row[5]; string expected = (string)row[6];
                UploadSubmissionRecord record = Observed(raw, returned, io, legal);
                ReleaseSubmissionObservation observation; string refusal;
                bool ok = record.TrySettle(requested, Stamp, "callback observed", out observation, out refusal);
                UploadCompletion classified = UploadCallbackRules.Classify(Item, raw, returned, io, legal);
                Check((string)row[0], ok == (expected == null) && refusal == expected && record.Settled == ok
                    && (!ok || requested == classified || requested == UploadCompletion.Unknown)
                    && (!ok || (observation.CallbackObserved && observation.RawEResult == raw && observation.IoFailure == io
                        && observation.ReturnedItem == returned.ToString(CultureInfo.InvariantCulture)
                        && observation.LegalAgreementRequired == legal && record.Completion == requested)), "refusal=" + Show(refusal) + " classifier=" + classified);
            }
            Check("classifier-legal-before-io", UploadCallbackRules.Classify(Item, 1, Item, true, true) == UploadCompletion.LegalAgreementRequired
                && UploadCallbackRules.Classify(Item, 1, Item, true, false) == UploadCompletion.IoFailure
                && UploadCallbackRules.Classify(Item, 2, Item, false, false) == UploadCompletion.Rejected
                && UploadCallbackRules.Classify(Item, 1, Item, false, false) == UploadCompletion.Ok, "legal outranks IO, IO outranks a non-OK raw result");
            UploadSubmissionRecord retry = Observed(1, Item, false, false);
            ReleaseSubmissionObservation none, repaired; string stampRefusal, noteRefusal, repairedRefusal;
            bool badStamp = retry.TrySettle(UploadCompletion.Ok, "2026-09-06T14:20:31Z", null, out none, out stampRefusal);
            Check("bad-observed-utc", !badStamp && stampRefusal == "settlement_observedUtc" && none == null && !retry.Settled && retry.Settlement == null
                && retry.Completion == null && retry.SettlementSha() == null, "refusal=" + Show(stampRefusal));
            bool badNote = retry.TrySettle(UploadCompletion.Ok, Stamp, string.Empty, out none, out noteRefusal);
            Check("empty-note", !badNote && noteRefusal == "settlement_note" && !retry.Settled, "refusal=" + Show(noteRefusal));
            bool repairedOk = retry.TrySettle(UploadCompletion.Ok, Stamp, "corrected stamp", out repaired, out repairedRefusal);
            Check("settlement-call-retryable", repairedOk && repairedRefusal == null && retry.Settled && repaired != null && repaired.ObservedUtc == Stamp, "refusal=" + Show(repairedRefusal));
            UploadSubmissionRecord once = Observed(1, Item, false, false);
            ReleaseSubmissionObservation first, second, third; string firstRefusal, secondRefusal, thirdRefusal;
            bool ok1 = once.TrySettle(UploadCompletion.Ok, Stamp, "first", out first, out firstRefusal);
            string sha = once.SettlementSha();
            bool ok2 = once.TrySettle(UploadCompletion.Rejected, Later, "second", out second, out secondRefusal);
            bool ok3 = once.TrySettle(UploadCompletion.Unknown, Later, null, out third, out thirdRefusal);
            Check("repeat-settle-is-the-same-instance", ok1 && ok2 && ok3 && firstRefusal == null && secondRefusal == null && thirdRefusal == null
                && ReferenceEquals(first, second) && ReferenceEquals(first, third) && ReferenceEquals(first, once.Settlement)
                && once.SettlementSha() == sha && once.Completion == UploadCompletion.Ok && first.ObservedUtc == Stamp
                && first.Note == "first", "a later settle must never rewrite the first");
        }
        private static void Roundtrip()
        {
            Group("roundtrip");
            UploadSubmissionRecord record = Observed(1, Item, false, false);
            ReleaseSubmissionObservation observation, decoded, quiet, quietDecoded;
            string refusal, decodeRefusal, quietRefusal, quietDecodeRefusal;
            record.TrySettle(UploadCompletion.Ok, Stamp, "shipped", out observation, out refusal);
            byte[] wire = record.SettlementUtf8();
            bool ok = ReleaseSubmissionObservationCodec.TryDecode(wire, out decoded, out decodeRefusal);
            Check("settlement-decodes", ok && decodeRefusal == null && refusal == null && observation != null, "refusal=" + Show(decodeRefusal));
            Check("fields-survive", ok && decoded.AttemptSHA == AttemptSha && decoded.PreviousObservationSHA == null && decoded.Item == ItemText
                && decoded.RequestVersion == Version && decoded.PlanSHA == PlanSha && decoded.ReceiptSHA == ReceiptSha && decoded.ObservedUtc == Stamp
                && decoded.CallbackObserved && decoded.RawEResult == 1 && decoded.ReturnedItem == ItemText && decoded.IoFailure == false
                && decoded.LegalAgreementRequired == false && decoded.Note == "shipped" && decoded.Completion == ReleaseSubmissionCompletion.Ok, "decoded fields differ");
            Check("sha-is-stable", record.SettlementSha() == record.SettlementSha() && record.SettlementSha() == ReleaseSubmissionObservationCodec.Sha256Hex(wire), "sha drifted");
            byte[] scribble = record.SettlementUtf8();
            scribble[0] = (byte)'x';
            Check("bytes-are-a-copy", record.SettlementUtf8()[0] == (byte)'{' && record.SettlementSha() == ReleaseSubmissionObservationCodec.Sha256Hex(record.SettlementUtf8()), "buffer escaped");
            UploadSubmissionRecord unobserved = Fresh();
            unobserved.TrySettle(UploadCompletion.TimedOut, Later, null, out quiet, out quietRefusal);
            bool quietOk = ReleaseSubmissionObservationCodec.TryDecode(unobserved.SettlementUtf8(), out quietDecoded, out quietDecodeRefusal);
            Check("timeout-wire-decodes", quietOk && quietRefusal == null && quiet != null && !quietDecoded.CallbackObserved && quietDecoded.RawEResult == null
                && quietDecoded.ReturnedItem == null && quietDecoded.IoFailure == null && quietDecoded.LegalAgreementRequired == null
                && quietDecoded.Note == null && quietDecoded.Completion == ReleaseSubmissionCompletion.TimedOut, "refusal=" + Show(quietDecodeRefusal));
        }
        private static void NoAuthority()
        {
            Group("no-authority");
            const BindingFlags Surface = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            MemberInfo[] members = typeof(UploadSubmissionRecord).GetMembers(Surface);
            Check("surface-found", members.Length > 0, "reflection found no public member");
            foreach (string word in new[] { "Archive", "Retry", "Deliver", "Release", "Publish", "Apply" })
            {
                string offender = null;
                foreach (MemberInfo member in members)
                    if (member.Name.IndexOf(word, StringComparison.Ordinal) >= 0) offender = member.Name;
                Check("no-" + word.ToLowerInvariant() + "-member", offender == null, "public member " + offender + " claims " + word + " authority");
            }
            string unsafeSignature = null;
            foreach (MethodInfo method in typeof(UploadSubmissionRecord).GetMethods(Surface))
            {
                if (!Safe(method.ReturnType)) unsafeSignature = method.Name;
                foreach (ParameterInfo parameter in method.GetParameters())
                    if (!Safe(parameter.ParameterType)) unsafeSignature = method.Name;
            }
            Check("no-io-clock-or-sdk-in-signatures", unsafeSignature == null, "member " + unsafeSignature + " names System.IO, DateTime or a Steam type");
        }
        private static bool Safe(Type type)
        {
            string name = type.FullName ?? type.Name;
            return name.IndexOf("System.IO", StringComparison.Ordinal) < 0 && name.IndexOf("DateTime", StringComparison.Ordinal) < 0 && name.IndexOf("Steamworks", StringComparison.Ordinal) < 0;
        }
        public static int Run()
        {
            int harness = 0;
            try { Identity(); Callback(); Settle(); Roundtrip(); NoAuthority(); UploadSubmissionRecordRaceTests.Race(); }
            catch (Exception error) { harness = 1; Console.Error.WriteLine("FAIL harness: " + error.Message); }
            int cases = 0, failed = harness;
            for (int i = 0; i < Names.Count; i++)
            {
                cases += Counts[i]; failed += Fails[i];
                Console.WriteLine("  " + Names[i] + ": " + Counts[i] + " cases, " + Fails[i] + " failed");
            }
            Console.WriteLine("UploadSubmissionRecord: " + cases + " cases, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
        public static int Main(string[] args)
        {
            if (args != null && args.Length > 0) Console.WriteLine("arguments ignored: " + args.Length);
            return Run();
        }
    }
}
