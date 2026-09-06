// ReleaseSubmissionObservation - one immutable, canonical-JSON account of what a single Workshop
// submission's completion callback showed, chained to the exact pre-submit attempt receipt bytes.
// Wire: one object, fixed field order, no whitespace, explicit nulls, <=4096 UTF-8 bytes, strings
// escaped by System.Text.Json's default encoder, fields ordered schema attemptSHA
// previousObservationSHA item requestVersion planSHA receiptSHA observedUtc callbackObserved
// rawEResult returnedItem ioFailure legalAgreementRequired completion note. Hashes are 64 lowercase
// hex; item is a canonical decimal u64 > 0, returnedItem that or canonical "0"; rawEResult is any
// Int32 - an opaque EResult copied unread (k_EResultOK == 1); requestVersion is major.minor.patch
// with no leading zeros; observedUtc is exactly yyyy-MM-ddTHH:mm:ss.fffffffZ. No callback observed =
// TimedOut/Unknown with every observed-only field null: Uncertain evidence, NEVER proof nothing was
// applied and never consent to submit again. A wrong, unknown or zero returned value is recorded as
// given under a non-Ok completion, never corrected, never dropped. No file, clock, network or SDK is
// touched here, and a parsed wire proves nothing about the Steam server.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ThousandAndFirst.WorkshopSteam.Evidence
{
    /// <summary>Mirror of UploadProtocol.UploadCompletion (Tools/WorkshopSteam/UploadProtocol.cs:38):
    /// same six members, order and implicit values, so no Steam SDK reference is needed.</summary>
    public enum ReleaseSubmissionCompletion
    { Unknown, Ok, Rejected, LegalAgreementRequired, TimedOut, IoFailure }
    /// <summary>What one submission's callback showed, and nothing else. Every value is stored exactly
    /// as given; nothing trims, repairs or reinterprets one, and no member expresses archive, retry,
    /// delivery or publication authority. A valid instance proves only well-formed bytes.</summary>
    public sealed class ReleaseSubmissionObservation
    {
        /// <summary>Fixed literal carried as the first field of every wire.</summary>
        public const string Schema = "taf-release-observation-v1";
        internal const string StampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
        internal const int StampChars = 28, HashChars = 64, MaxNoteChars = 512, OkRawResult = 1;
        /// <summary>AttemptSHA hashes the exact pre-submit receipt bytes; PreviousObservationSHA is the prior wire's hash, or null.</summary>
        public readonly string AttemptSHA, PreviousObservationSHA, Item, RequestVersion, PlanSHA, ReceiptSHA, ObservedUtc;
        /// <summary>True only when a callback was seen; false is absence of evidence, not proof.</summary>
        public readonly bool CallbackObserved;
        /// <summary>The opaque EResult as copied, anywhere in Int32, and the returned file id as carried.</summary>
        public readonly int? RawEResult;
        public readonly string ReturnedItem, Note;
        public readonly bool? IoFailure, LegalAgreementRequired;
        public readonly ReleaseSubmissionCompletion Completion;
        private ReleaseSubmissionObservation(string attemptSHA, string previousObservationSHA, string item,
            string requestVersion, string planSHA, string receiptSHA, string observedUtc, bool callbackObserved,
            int? rawEResult, string returnedItem, bool? ioFailure, bool? legalAgreementRequired,
            ReleaseSubmissionCompletion completion, string note)
        {
            AttemptSHA = attemptSHA; PreviousObservationSHA = previousObservationSHA; Item = item;
            RequestVersion = requestVersion; PlanSHA = planSHA; ReceiptSHA = receiptSHA; Note = note;
            ObservedUtc = observedUtc; CallbackObserved = callbackObserved; RawEResult = rawEResult;
            ReturnedItem = returnedItem; IoFailure = ioFailure; Completion = completion; LegalAgreementRequired = legalAgreementRequired;
        }
        /// <summary>Total and fail-closed. No callback means no observed-only field and an Uncertain
        /// outcome. Ok is the single clean outcome and demands CallbackObserved, RawEResult == 1
        /// (EResult.k_EResultOK), ioFailure false, legalAgreementRequired false and ReturnedItem == Item;
        /// any other raw value is recordable only under a non-Ok completion, never dressed up as success.</summary>
        public static bool TryCreate(string attemptSHA, string previousObservationSHA, string item,
            string requestVersion, string planSHA, string receiptSHA, string observedUtc, bool callbackObserved,
            int? rawEResult, string returnedItem, bool? ioFailure, bool? legalAgreementRequired,
            ReleaseSubmissionCompletion completion, string note,
            out ReleaseSubmissionObservation observation, out string refusal)
        {
            observation = null; refusal = null;
            if (!Hash(attemptSHA)) refusal = "attemptSHA";
            else if (previousObservationSHA != null && !Hash(previousObservationSHA)) refusal = "previousObservationSHA";
            else if (!ItemId(item, false)) refusal = "item";
            else if (!VersionText(requestVersion)) refusal = "requestVersion";
            else if (!Hash(planSHA)) refusal = "planSHA";
            else if (!Hash(receiptSHA)) refusal = "receiptSHA";
            else if (!Stamp(observedUtc)) refusal = "observedUtc";
            else if (returnedItem != null && !ItemId(returnedItem, true)) refusal = "returnedItem";
            else if (note != null && !NoteText(note)) refusal = "note";
            else if (CompletionName(completion) == null) refusal = "completion";
            else if (!callbackObserved && (rawEResult.HasValue || returnedItem != null
                || ioFailure.HasValue || legalAgreementRequired.HasValue)) refusal = "unobserved_fields_present";
            else if (!callbackObserved && completion != ReleaseSubmissionCompletion.TimedOut
                && completion != ReleaseSubmissionCompletion.Unknown) refusal = "unobserved_completion";
            else if (callbackObserved && completion == ReleaseSubmissionCompletion.TimedOut) refusal = "observed_timeout";
            else if (callbackObserved && !rawEResult.HasValue) refusal = "rawEResult_absent";
            else if (completion == ReleaseSubmissionCompletion.Ok
                && (!rawEResult.HasValue || rawEResult.Value != OkRawResult)) refusal = "ok_requires_raw_1";
            else if (completion == ReleaseSubmissionCompletion.Ok
                && (!ioFailure.HasValue || ioFailure.Value || !legalAgreementRequired.HasValue || legalAgreementRequired.Value
                    || returnedItem == null || !string.Equals(returnedItem, item, StringComparison.Ordinal))) refusal = "ok_without_clean_callback";
            else if (completion == ReleaseSubmissionCompletion.LegalAgreementRequired
                && (!legalAgreementRequired.HasValue || !legalAgreementRequired.Value)) refusal = "legal_flag_absent";
            else if (completion == ReleaseSubmissionCompletion.IoFailure
                && (!ioFailure.HasValue || !ioFailure.Value)) refusal = "io_flag_absent";
            if (refusal != null) return false;
            observation = new ReleaseSubmissionObservation(attemptSHA, previousObservationSHA, item,
                requestVersion, planSHA, receiptSHA, observedUtc, callbackObserved, rawEResult,
                returnedItem, ioFailure, legalAgreementRequired, completion, note);
            return true;
        }
        private static readonly string[] Names = { "Unknown", "Ok", "Rejected", "LegalAgreementRequired", "TimedOut", "IoFailure" };
        /// <summary>Exact defined-member name and lookup; a number, a case variant or an undefined
        /// member is refused rather than coerced onto a neighbouring outcome.</summary>
        internal static string CompletionName(ReleaseSubmissionCompletion value)
        { int i = (int)value; return i >= 0 && i < Names.Length ? Names[i] : null; }
        internal static bool TryCompletion(string name, out ReleaseSubmissionCompletion value)
        {
            value = ReleaseSubmissionCompletion.Unknown;
            for (int i = 0; name != null && i < Names.Length; i++)
                if (string.Equals(Names[i], name, StringComparison.Ordinal)) { value = (ReleaseSubmissionCompletion)i; return true; }
            return false;
        }
        private static bool Hash(string value)
        {
            if (value == null || value.Length != HashChars) return false;
            for (int i = 0; i < value.Length; i++) if ((value[i] < '0' || value[i] > '9') && (value[i] < 'a' || value[i] > 'f')) return false;
            return true;
        }
        /// <summary>Canonical decimal u64, no leading zeros; only a returned id may be the "0" a failed callback hands back.</summary>
        private static bool ItemId(string value, bool zeroAllowed)
        {
            ulong parsed;
            if (value == null || value.Length < 1 || value.Length > 20) return false;
            for (int i = 0; i < value.Length; i++) if (value[i] < '0' || value[i] > '9') return false;
            return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)
                && (parsed > 0UL || zeroAllowed) && parsed.ToString(CultureInfo.InvariantCulture) == value;
        }
        private static bool VersionText(string value)
        {
            if (value == null || value.Length < 5 || value.Length > 29) return false;
            int start = 0, parts = 0;
            for (int i = 0; i <= value.Length; i++)
            {
                if (i != value.Length && value[i] != '.') continue;
                if (i - start < 1 || i - start > 9 || (i - start > 1 && value[start] == '0')) return false;
                for (int j = start; j < i; j++) if (value[j] < '0' || value[j] > '9') return false;
                parts++; start = i + 1;
            }
            return parts == 3;
        }
        private static bool Stamp(string value)
        {
            DateTime parsed;
            return value != null && value.Length == StampChars
                && DateTime.TryParseExact(value, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed)
                && parsed.ToString(StampFormat, CultureInfo.InvariantCulture) == value;
        }
        private static bool NoteText(string value)
        { return value.Length >= 1 && value.Length <= MaxNoteChars && WellFormedText(value); }
        /// <summary>No control char and no lone UTF-16 surrogate: a high one must be followed by a low one, and an orphan low is refused.</summary>
        internal static bool WellFormedText(string value)
        {
            if (value == null) return true;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c) || char.IsLowSurrogate(c)
                    || (char.IsHighSurrogate(c) && (i + 1 == value.Length || !char.IsLowSurrogate(value[++i])))) return false;
            }
            return true;
        }
    }
    /// <summary>Pure canonical codec. Encoding is deterministic; decoding accepts a wire only when re-encoding
    /// the decoded observation reproduces it byte for byte, so an uppercase hash, a padded item, an escaped
    /// property name, reordered fields or stray whitespace is refused, never quietly repaired.</summary>
    public static class ReleaseSubmissionObservationCodec
    {
        public const string Schema = ReleaseSubmissionObservation.Schema;
        public const int MaxWireBytes = 4096;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly byte[] Claim = Encoding.UTF8.GetBytes("\"" + Schema + "\"");
        /// <summary>True for any wire CLAIMING this schema, whatever its shape; the length gate lives in TryDecode.</summary>
        public static bool MatchesSchema(byte[] utf8)
        {
            for (int start = 0; utf8 != null && start + Claim.Length <= utf8.Length; start++)
            {
                int i = 0;
                while (i < Claim.Length && utf8[start + i] == Claim[i]) i++;
                if (i == Claim.Length) return true;
            }
            return false;
        }
        public static string Encode(ReleaseSubmissionObservation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            StringBuilder wire = new StringBuilder(640).Append('{');
            Text(wire, "schema", Schema); Text(wire, "attemptSHA", observation.AttemptSHA);
            Text(wire, "previousObservationSHA", observation.PreviousObservationSHA); Text(wire, "item", observation.Item);
            Text(wire, "requestVersion", observation.RequestVersion); Text(wire, "planSHA", observation.PlanSHA);
            Text(wire, "receiptSHA", observation.ReceiptSHA); Text(wire, "observedUtc", observation.ObservedUtc);
            Flag(wire, "callbackObserved", observation.CallbackObserved); Number(wire, "rawEResult", observation.RawEResult);
            Text(wire, "returnedItem", observation.ReturnedItem); Flag(wire, "ioFailure", observation.IoFailure);
            Flag(wire, "legalAgreementRequired", observation.LegalAgreementRequired);
            Text(wire, "completion", ReleaseSubmissionObservation.CompletionName(observation.Completion));
            Text(wire, "note", observation.Note);
            return wire.Append('}').ToString();
        }
        public static byte[] EncodeUtf8(ReleaseSubmissionObservation observation) { return Utf8.GetBytes(Encode(observation)); }
        public static string Sha256Hex(byte[] utf8)
        {
            if (utf8 == null) throw new ArgumentNullException(nameof(utf8));
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(utf8)).Replace("-", string.Empty).ToLowerInvariant();
        }
        /// <summary>Total: a hostile wire yields false with a null observation and a reason, never an exception.
        /// Only the read/parse phase is guarded, and only against JsonException (malformed grammar) and
        /// InvalidOperationException (text System.Text.Json will not materialise, such as an escaped lone
        /// surrogate); invariant checks stay outside it so a defect there can never be masked.</summary>
        public static bool TryDecode(byte[] utf8, out ReleaseSubmissionObservation observation, out string refusal)
        {
            observation = null;
            if (utf8 == null) { refusal = "wire_absent"; return false; }
            if (utf8.Length < 2 || utf8.Length > MaxWireBytes) { refusal = "wire_bounds"; return false; }
            if (utf8[0] == 0xEF) { refusal = "wire_byte_order_mark"; return false; }
            try { Utf8.GetString(utf8); } catch (DecoderFallbackException) { refusal = "invalid_utf8"; return false; }
            if (!MatchesSchema(utf8)) { refusal = "wire_not_claimed"; return false; }
            string attempt = null, previous = null, item = null, version = null, plan = null;
            string receipt = null, stamp = null, returned = null, done = null, note = null;
            bool observed = false; int? raw = null; bool? io = null, legal = null; int seen = 0;
            try
            {
                JsonReaderOptions options = new JsonReaderOptions
                { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 2 };
                Utf8JsonReader reader = new Utf8JsonReader(utf8, options);
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                { refusal = "wire_not_object"; return false; }
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    string name = reader.GetString();
                    if (!ReleaseSubmissionObservation.WellFormedText(name)) { refusal = "malformed_text"; return false; }
                    if (!reader.Read()) { refusal = "field_value_absent"; return false; }
                    int bit; bool ok;
                    switch (name)
                    {
                        case "schema": bit = 1 << 0; ok = ReadText(ref reader, false, out string claimed)
                            && string.Equals(claimed, Schema, StringComparison.Ordinal); break;
                        case "attemptSHA": bit = 1 << 1; ok = ReadText(ref reader, false, out attempt); break;
                        case "previousObservationSHA": bit = 1 << 2; ok = ReadText(ref reader, true, out previous); break;
                        case "item": bit = 1 << 3; ok = ReadText(ref reader, false, out item); break;
                        case "requestVersion": bit = 1 << 4; ok = ReadText(ref reader, false, out version); break;
                        case "planSHA": bit = 1 << 5; ok = ReadText(ref reader, false, out plan); break;
                        case "receiptSHA": bit = 1 << 6; ok = ReadText(ref reader, false, out receipt); break;
                        case "observedUtc": bit = 1 << 7; ok = ReadText(ref reader, false, out stamp); break;
                        case "callbackObserved": bit = 1 << 8;
                            ok = ReadFlag(ref reader, false, out bool? present); observed = ok && present.Value; break;
                        case "rawEResult": bit = 1 << 9; ok = ReadNumber(ref reader, out raw); break;
                        case "returnedItem": bit = 1 << 10; ok = ReadText(ref reader, true, out returned); break;
                        case "ioFailure": bit = 1 << 11; ok = ReadFlag(ref reader, true, out io); break;
                        case "legalAgreementRequired": bit = 1 << 12; ok = ReadFlag(ref reader, true, out legal); break;
                        case "completion": bit = 1 << 13; ok = ReadText(ref reader, false, out done); break;
                        case "note": bit = 1 << 14; ok = ReadText(ref reader, true, out note); break;
                        default: refusal = "unknown_field"; return false;
                    }
                    if (!ok) { refusal = "field_type"; return false; }
                    if ((seen & bit) != 0) { refusal = "duplicate_field"; return false; }
                    seen |= bit;
                }
                if (reader.TokenType != JsonTokenType.EndObject) { refusal = "wire_unterminated"; return false; }
                if (reader.Read()) { refusal = "trailing_token"; return false; }
            }
            catch (JsonException) { refusal = "malformed_json"; return false; }
            catch (InvalidOperationException) { refusal = "malformed_text"; return false; }
            if (seen != (1 << 15) - 1) { refusal = "field_missing"; return false; }
            ReleaseSubmissionCompletion completion;
            if (!ReleaseSubmissionObservation.TryCompletion(done, out completion)) { refusal = "completion"; return false; }
            ReleaseSubmissionObservation decoded;
            if (!ReleaseSubmissionObservation.TryCreate(attempt, previous, item, version, plan, receipt,
                stamp, observed, raw, returned, io, legal, completion, note, out decoded, out refusal)) return false;
            if (!Sane(attempt) || !Sane(previous) || !Sane(item) || !Sane(version) || !Sane(plan) || !Sane(receipt)
                || !Sane(stamp) || !Sane(returned) || !Sane(done) || !Sane(note)) { refusal = "malformed_text"; return false; }
            if (!Same(EncodeUtf8(decoded), utf8)) { refusal = "noncanonical_wire"; return false; }
            observation = decoded; refusal = null; return true;
        }
        /// <summary>Backstop after the per-field validators: no decoded value may carry a control char or a lone surrogate.</summary>
        private static bool Sane(string value) { return ReleaseSubmissionObservation.WellFormedText(value); }
        private static void Key(StringBuilder wire, string name) { if (wire.Length > 1) wire.Append(','); wire.Append('"').Append(name).Append("\":"); }
        private static void Text(StringBuilder wire, string name, string value)
        { Key(wire, name); wire.Append(value == null ? "null" : "\"" + JsonEncodedText.Encode(value).Value + "\""); }
        private static void Flag(StringBuilder wire, string name, bool? value)
        { Key(wire, name); wire.Append(!value.HasValue ? "null" : value.Value ? "true" : "false"); }
        private static void Number(StringBuilder wire, string name, int? value)
        { Key(wire, name); wire.Append(!value.HasValue ? "null" : value.Value.ToString(CultureInfo.InvariantCulture)); }
        private static bool ReadText(ref Utf8JsonReader reader, bool optional, out string value)
        { value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null; return value != null || (optional && reader.TokenType == JsonTokenType.Null); }
        private static bool ReadFlag(ref Utf8JsonReader reader, bool optional, out bool? value)
        { value = reader.TokenType == JsonTokenType.True ? true : reader.TokenType == JsonTokenType.False ? (bool?)false : null; return value.HasValue || (optional && reader.TokenType == JsonTokenType.Null); }
        /// <summary>Any Int32, and only an Int32: TryGetInt32 refuses a fraction, an exponent or an out-of-range value.</summary>
        private static bool ReadNumber(ref Utf8JsonReader reader, out int? value)
        {
            int parsed = 0;
            bool number = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out parsed);
            value = number ? (int?)parsed : null; return number || reader.TokenType == JsonTokenType.Null;
        }
        private static bool Same(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
