// UploadSubmissionRecord - the single-attempt latch for one Workshop submission. It holds the exact
// requested identities as separate immutable facts, takes the FIRST completion callback verbatim and
// refuses every later one, and settles ONCE into an immutable ReleaseSubmissionObservation. API:
// TryCreate, TryRecordCallback, TrySettle (a repeat settle returns the SAME instance), SettlementSha,
// SettlementUtf8. THE GATE RULE: one private lock guards every read AND every transition of the mutable
// state (callback facts, settlement, completion), so a racing caller sees one whole tuple or none and
// exactly one settlement; the readonly identity fields never change after construction and are read
// without the gate. No clock, file, console, network or SDK; outcomes are UploadCallbackRules' alone.
using System;
using System.Globalization;

namespace ThousandAndFirst.WorkshopSteam.Evidence
{
    /// <summary>One submission attempt, latched once. Identities are fixed at construction and never
    /// re-derived; the first callback's four facts are kept exactly as the pump handed them over, and a
    /// duplicate or conflicting callback is refused rather than allowed to rewrite them. Settlement is a
    /// single immutable observation: it records what was seen, never permission to submit again. Concurrent
    /// callers are serialised by one gate, so a callback and a settlement racing each other yield exactly
    /// one of two legal outcomes: (a) the settlement wins and the attempt terminates UNOBSERVED - Unknown
    /// or TimedOut, Settlement.CallbackObserved false - and the later callback is refused "already_settled";
    /// (b) the callback wins, the full four-fact tuple is latched, and the settlement then classifies it.
    /// No third state exists: no partial tuple, and no fact changes after the terminal snapshot.</summary>
    public sealed class UploadSubmissionRecord
    {
        /// <summary>Hash of the exact pre-submit attempt receipt bytes (SteamUploadPort.cs:170-194),
        /// distinct from the PACKAGE plan and receipt hashes (UploadPackage.cs) carried alongside it.</summary>
        public readonly string AttemptByteSha;
        /// <summary>Prior observation wire's hash, chaining this attempt to the last, or null for a first.</summary>
        public readonly string PreviousObservationSha;
        /// <summary>The requested published file id, as a number and as the canonical decimal text the wire carries.</summary>
        public readonly ulong Item;
        public readonly string ItemText, RequestVersion, PlanSha, PackageReceiptSha, CreatedUtc;
        /// <summary>The first callback's four facts as ONE immutable value, published by a single reference
        /// write under the gate, so no reader can ever see a half-written tuple: the holder is null (nothing
        /// observed) or complete. It is assigned once and never replaced.</summary>
        private sealed class CallbackFacts
        {
            public readonly int Raw;
            public readonly ulong Returned;
            public readonly bool Io, Legal;
            public CallbackFacts(int raw, ulong returned, bool io, bool legal)
            { Raw = raw; Returned = returned; Io = io; Legal = legal; }
        }
        /// <summary>The one gate. Every read and every transition of the three mutable fields below runs
        /// inside it. The readonly identity fields above are set in the constructor and never change, so
        /// they are safely published by construction and are read without the gate.</summary>
        private readonly object gate = new object();
        private CallbackFacts _facts;
        private ReleaseSubmissionObservation _settlement;
        private UploadCompletion? _completion;
        private UploadSubmissionRecord(string attemptByteSha, string previousObservationSha, ulong item,
            string itemText, string requestVersion, string planSha, string packageReceiptSha, string createdUtc)
        {
            AttemptByteSha = attemptByteSha; PreviousObservationSha = previousObservationSha; Item = item;
            ItemText = itemText; RequestVersion = requestVersion; PlanSha = planSha;
            PackageReceiptSha = packageReceiptSha; CreatedUtc = createdUtc;
        }
        /// <summary>Validates every identity BEFORE any submission could be made, by building the
        /// unobserved Unknown observation this attempt would settle to if no callback ever arrived and
        /// round-tripping it through the canonical codec. The codec owns the hash, item, version and
        /// stamp rules, so nothing is re-implemented here; only item == 0 is refused up front, because a
        /// zero id is a request that never named a Workshop item. Refusals are the codec's reason under
        /// an "identity_" prefix ("observedUtc" reported as "identity_createdUtc"), or "identity_wire_*"
        /// if the round-trip itself fails. createdUtc is the caller's stamp: no clock is read here. This
        /// touches no mutable state - the record does not exist yet - so it needs no gate.</summary>
        public static bool TryCreate(string attemptByteSha, string previousObservationSha, ulong item,
            string requestVersion, string planSha, string packageReceiptSha, string createdUtc,
            out UploadSubmissionRecord record, out string refusal)
        {
            record = null;
            if (item == 0UL) { refusal = "identity_item_zero"; return false; }
            string itemText = item.ToString(CultureInfo.InvariantCulture);
            ReleaseSubmissionObservation probe;
            string reason;
            if (!ReleaseSubmissionObservation.TryCreate(attemptByteSha, previousObservationSha, itemText,
                requestVersion, planSha, packageReceiptSha, createdUtc, false, null, null, null, null,
                ReleaseSubmissionCompletion.Unknown, null, out probe, out reason))
            {
                refusal = "identity_" + (string.Equals(reason, "observedUtc", StringComparison.Ordinal)
                    ? "createdUtc" : reason);
                return false;
            }
            string wireRefusal;
            if (!ReleaseSubmissionObservationCodec.TryDecode(
                ReleaseSubmissionObservationCodec.EncodeUtf8(probe), out _, out wireRefusal))
            { refusal = "identity_wire_" + wireRefusal; return false; }
            record = new UploadSubmissionRecord(attemptByteSha, previousObservationSha, item, itemText,
                requestVersion, planSha, packageReceiptSha, createdUtc);
            refusal = null;
            return true;
        }
        /// <summary>True only once a callback has been latched; false is absence of evidence, never proof
        /// that nothing reached Steam. Read under the gate, like every observed fact below.</summary>
        public bool CallbackObserved { get { lock (gate) { return _facts != null; } } }
        /// <summary>The opaque EResult exactly as delivered, anywhere in Int32; null while unobserved.</summary>
        public int? RawEResult { get { lock (gate) { return _facts != null ? (int?)_facts.Raw : null; } } }
        /// <summary>The returned published file id as delivered, including 0 and a wrong id; null while unobserved.</summary>
        public ulong? ReturnedItem { get { lock (gate) { return _facts != null ? (ulong?)_facts.Returned : null; } } }
        public bool? IoFailure { get { lock (gate) { return _facts != null ? (bool?)_facts.Io : null; } } }
        public bool? LegalAgreementRequired { get { lock (gate) { return _facts != null ? (bool?)_facts.Legal : null; } } }
        public bool Settled { get { lock (gate) { return _settlement != null; } } }
        /// <summary>The one immutable observation this attempt settled to, or null while unsettled.</summary>
        public ReleaseSubmissionObservation Settlement { get { lock (gate) { return _settlement; } } }
        /// <summary>The settled outcome, or null while unsettled.</summary>
        public UploadCompletion? Completion { get { lock (gate) { return _completion; } } }
        /// <summary>Latches the FIRST callback's four facts verbatim - the full Int32 result, the returned
        /// ulong including 0 or a wrong id, and both flags - and returns true. Every later call is refused
        /// and changes nothing at all: "callback_duplicate" when the facts are identical, "callback_conflict"
        /// when they differ (both mean already recorded), and "already_settled" once the attempt has settled.
        /// A late or replayed callback is evidence about the pump, never a correction of the first. The whole
        /// test-and-latch runs inside the gate, so exactly one of two racing callbacks wins and the loser is
        /// refused; the winning tuple is published by one reference write, never field by field.</summary>
        public bool TryRecordCallback(int rawEResult, ulong returnedItem, bool ioFailure,
            bool legalAgreementRequired, out string refusal)
        {
            lock (gate)
            {
                if (_settlement != null) { refusal = "already_settled"; return false; }
                CallbackFacts held = _facts;
                if (held != null)
                {
                    bool same = held.Raw == rawEResult && held.Returned == returnedItem
                        && held.Io == ioFailure && held.Legal == legalAgreementRequired;
                    refusal = same ? "callback_duplicate" : "callback_conflict";
                    return false;
                }
                _facts = new CallbackFacts(rawEResult, returnedItem, ioFailure, legalAgreementRequired);
                refusal = null;
                return true;
            }
        }
        /// <summary>The one terminal settlement. The first success builds the immutable observation from the
        /// latched identities and facts, with the caller's observedUtc and note, and keeps it; every later
        /// call returns that SAME instance with true and a null refusal, ignoring its arguments - repeat
        /// settles are idempotent by design, so a retried settlement call can never mint a second account.
        /// The already-settled test is the FIRST thing inside the gate, so two racing settles can never both
        /// build: the loser returns the winner's instance. Everything is validated BEFORE anything is latched,
        /// so a refused settle leaves the record unsettled and the settlement CALL can be retried with
        /// corrected inputs; that is not a retry of the submission, which no member here can perform. With no
        /// callback only Unknown or TimedOut are accepted ("no_callback_requires_unknown_or_timedout"). With a
        /// callback the completion must equal UploadCallbackRules.Classify (UploadCallbackRules.cs:6-15: zero
        /// expected id, then legal, then IO, then a raw result other than 1, then a returned id that is not the
        /// requested one, then Ok), which is where legal-before-IO priority lives; the sole exception is an
        /// explicit Unknown, which records a pump fault while the observation still carries the raw result,
        /// returned id and both flags. Any other value, TimedOut included, is refused as
        /// "completion_mismatch_classifier_says_&lt;Name&gt;". A codec refusal is returned under a
        /// "settlement_" prefix. The facts are read once into a local under the gate and the observation is
        /// built from that local, so a callback racing this call is either wholly included or wholly absent.</summary>
        public bool TrySettle(UploadCompletion completion, string observedUtc, string note,
            out ReleaseSubmissionObservation observation, out string refusal)
        {
            lock (gate)
            {
                if (_settlement != null) { observation = _settlement; refusal = null; return true; }
                observation = null;
                ReleaseSubmissionCompletion mapped;
                if (!TryMap(completion, out mapped)) { refusal = "completion_undefined"; return false; }
                CallbackFacts facts = _facts;
                if (facts == null)
                {
                    if (completion != UploadCompletion.Unknown && completion != UploadCompletion.TimedOut)
                    { refusal = "no_callback_requires_unknown_or_timedout"; return false; }
                }
                else if (completion != UploadCompletion.Unknown)
                {
                    UploadCompletion classified = UploadCallbackRules.Classify(Item, facts.Raw, facts.Returned,
                        facts.Io, facts.Legal);
                    if (completion != classified)
                    { refusal = "completion_mismatch_classifier_says_" + classified.ToString(); return false; }
                }
                ReleaseSubmissionObservation built;
                string reason;
                if (!ReleaseSubmissionObservation.TryCreate(AttemptByteSha, PreviousObservationSha, ItemText,
                    RequestVersion, PlanSha, PackageReceiptSha, observedUtc, facts != null,
                    facts != null ? (int?)facts.Raw : null,
                    facts != null ? facts.Returned.ToString(CultureInfo.InvariantCulture) : null,
                    facts != null ? (bool?)facts.Io : null,
                    facts != null ? (bool?)facts.Legal : null,
                    mapped, note, out built, out reason))
                { refusal = "settlement_" + reason; return false; }
                _settlement = built; _completion = completion; observation = built; refusal = null;
                return true;
            }
        }
        /// <summary>Hash of the settled wire, or null while unsettled. Encoded fresh on every call, inside
        /// the gate, so a settlement racing this call is either wholly visible or not yet visible.</summary>
        public string SettlementSha()
        {
            lock (gate)
            {
                return _settlement == null ? null
                    : ReleaseSubmissionObservationCodec.Sha256Hex(ReleaseSubmissionObservationCodec.EncodeUtf8(_settlement));
            }
        }
        /// <summary>The settled wire's bytes, or null while unsettled. A new array every call, so a caller
        /// that mutates what it is handed cannot reach the latched observation.</summary>
        public byte[] SettlementUtf8()
        {
            lock (gate)
            {
                return _settlement == null ? null : ReleaseSubmissionObservationCodec.EncodeUtf8(_settlement);
            }
        }
        /// <summary>Maps the port's outcome onto the evidence enum by NAME, exhaustively; an undefined
        /// value is refused rather than folded onto a neighbouring outcome. The name check keeps the two
        /// enums bound even if either one's member order ever moves. Pure: it reads no state and needs no
        /// gate, and is only ever called from inside one.</summary>
        private static bool TryMap(UploadCompletion completion, out ReleaseSubmissionCompletion mapped)
        {
            switch (completion)
            {
                case UploadCompletion.Unknown: mapped = ReleaseSubmissionCompletion.Unknown; break;
                case UploadCompletion.Ok: mapped = ReleaseSubmissionCompletion.Ok; break;
                case UploadCompletion.Rejected: mapped = ReleaseSubmissionCompletion.Rejected; break;
                case UploadCompletion.LegalAgreementRequired:
                    mapped = ReleaseSubmissionCompletion.LegalAgreementRequired; break;
                case UploadCompletion.TimedOut: mapped = ReleaseSubmissionCompletion.TimedOut; break;
                case UploadCompletion.IoFailure: mapped = ReleaseSubmissionCompletion.IoFailure; break;
                default: mapped = ReleaseSubmissionCompletion.Unknown; return false;
            }
            return string.Equals(mapped.ToString(), completion.ToString(), StringComparison.Ordinal);
        }
    }
}
