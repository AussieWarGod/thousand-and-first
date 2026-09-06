// ReleaseSubmissionObservationTests - standalone, SDK-free, in-memory harness for the release
// submission observation codec. No file, network, Steam SDK or test-framework dependency; the
// only output is a summary plus one line per failure. Run() returns the failure count so a caller
// can host it; Main forwards that as a process code. Ten groups: success, fault-outcomes,
// invariants, malformed, bounds, canonical, adversarial, chain-identity, roundtrip, no-authority.
// Adversarial replays the corrections a hostile pass forced: the opaque EResult spans the whole
// Int32, a failed callback may hand back the canonical returned id "0", and an escaped lone UTF-16
// surrogate is refused rather than thrown. The last group guards that no public member claims
// release authority.
using System;
using System.Reflection;
using System.Text;

namespace ThousandAndFirst.WorkshopSteam.Evidence
{
    public static class ReleaseSubmissionObservationTests
    {
        private const string AttemptSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string PlanSha = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
        private const string ReceiptSha = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
        private const string ItemText = "3794797472", OtherItem = "3794797473";
        private const string N = "null", T = "true", F = "false";
        private const string I = "\"" + ItemText + "\"", J = "\"" + OtherItem + "\"";
        private const string V = "\"0.3.0\"", S = "\"2026-09-06T14:20:31.1234567Z\"";
        private const string Tail = ",\"note\":null}";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string Pair = char.ConvertFromUtf32(0x1F600); // valid high+low pair, U+1F600
        private static int probes;
        private struct Case { internal string Group, Name, Refusal; internal byte[] Wire; internal bool Expect; }
        private static Case C(string group, string name, string wire, bool expect, string refusal)
        { return B(group, name, Utf8.GetBytes(wire), expect, refusal); }
        private static Case B(string group, string name, byte[] wire, bool expect, string refusal)
        {
            Case c = new Case(); c.Group = group; c.Name = name; c.Wire = wire; c.Expect = expect; c.Refusal = refusal;
            return c;
        }
        private static string Wire(string previous, string item, string version, string stamp, string observed,
            string raw, string returned, string io, string legal, string done, string note)
        {
            return "{\"schema\":\"" + ReleaseSubmissionObservationCodec.Schema + "\",\"attemptSHA\":\"" + AttemptSha
                + "\",\"previousObservationSHA\":" + previous + ",\"item\":" + item + ",\"requestVersion\":" + version
                + ",\"planSHA\":\"" + PlanSha + "\",\"receiptSHA\":\"" + ReceiptSha + "\",\"observedUtc\":" + stamp
                + ",\"callbackObserved\":" + observed + ",\"rawEResult\":" + raw + ",\"returnedItem\":" + returned
                + ",\"ioFailure\":" + io + ",\"legalAgreementRequired\":" + legal + ",\"completion\":" + done
                + ",\"note\":" + note + "}";
        }
        private static string Ok() { return Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", N); }
        private static string Quiet(string done) { return Wire(N, I, V, S, F, N, N, N, N, done, N); }
        private static string Retail(string replacement) { return Ok().Replace(Tail, replacement); }
        private static byte[] Bom()
        {
            byte[] body = Utf8.GetBytes(Ok()), bytes = new byte[body.Length + 3];
            bytes[0] = 0xEF; bytes[1] = 0xBB; bytes[2] = 0xBF; Array.Copy(body, 0, bytes, 3, body.Length);
            return bytes;
        }
        private static byte[] Corrupt()
        {
            byte[] bytes = Utf8.GetBytes(Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", "\"zz\""));
            for (int i = 0; i < bytes.Length; i++) if (bytes[i] == (byte)'z') { bytes[i] = 0xC3; break; }
            return bytes;
        }
        private static bool Make(string previous, out ReleaseSubmissionObservation value, out string refusal)
        {
            return ReleaseSubmissionObservation.TryCreate(AttemptSha, previous, ItemText, "0.3.0", PlanSha,
                ReceiptSha, "2026-09-06T14:20:31.1234567Z", true, 1, ItemText, false, false,
                ReleaseSubmissionCompletion.Ok, "alpha lane", out value, out refusal);
        }
        private static Case[] Cases()
        {
            return new Case[]
            {
                C("success", "canonical-ok", Ok(), true, null),
                C("fault-outcomes", "rejected", Wire(N, I, V, S, T, "15", I, F, F, "\"Rejected\"", N), true, null),
                C("fault-outcomes", "rejected-no-returned-item", Wire(N, I, V, S, T, "15", N, F, F, "\"Rejected\"", N), true, null),
                C("fault-outcomes", "legal-agreement", Wire(N, I, V, S, T, "1", I, F, T, "\"LegalAgreementRequired\"", N), true, null),
                C("fault-outcomes", "io-failure", Wire(N, I, V, S, T, "2", N, T, N, "\"IoFailure\"", N), true, null),
                C("fault-outcomes", "timed-out-unobserved", Quiet("\"TimedOut\""), true, null),
                C("fault-outcomes", "unknown-unobserved", Quiet("\"Unknown\""), true, null),
                C("fault-outcomes", "wrong-returned-item-kept", Wire(N, I, V, S, T, "1", J, F, F, "\"Unknown\"", N), true, null),
                C("fault-outcomes", "note-carried", Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", "\"alpha lane\""), true, null),
                C("fault-outcomes", "previous-observation-chained", Wire("\"" + PlanSha + "\"", I, V, S, T, "1", I, F, F, "\"Ok\"", N), true, null),
                C("invariants", "ok-with-io-failure", Wire(N, I, V, S, T, "1", I, T, F, "\"Ok\"", N), false, "ok_without_clean_callback"),
                C("invariants", "ok-with-wrong-item", Wire(N, I, V, S, T, "1", J, F, F, "\"Ok\"", N), false, "ok_without_clean_callback"),
                C("invariants", "ok-with-legal-flag", Wire(N, I, V, S, T, "1", I, F, T, "\"Ok\"", N), false, "ok_without_clean_callback"),
                C("invariants", "ok-without-returned-item", Wire(N, I, V, S, T, "1", N, F, F, "\"Ok\"", N), false, "ok_without_clean_callback"),
                C("invariants", "unobserved-with-raw", Wire(N, I, V, S, F, "1", N, N, N, "\"TimedOut\"", N), false, "unobserved_fields_present"),
                C("invariants", "unobserved-with-returned", Wire(N, I, V, S, F, N, I, N, N, "\"TimedOut\"", N), false, "unobserved_fields_present"),
                C("invariants", "unobserved-with-io", Wire(N, I, V, S, F, N, N, F, N, "\"TimedOut\"", N), false, "unobserved_fields_present"),
                C("invariants", "unobserved-rejected", Quiet("\"Rejected\""), false, "unobserved_completion"),
                C("invariants", "unobserved-ok", Quiet("\"Ok\""), false, "unobserved_completion"),
                C("invariants", "observed-timed-out", Wire(N, I, V, S, T, "1", N, F, F, "\"TimedOut\"", N), false, "observed_timeout"),
                C("invariants", "observed-without-raw", Wire(N, I, V, S, T, N, N, F, F, "\"Unknown\"", N), false, "rawEResult_absent"),
                C("invariants", "raw-negative-recorded", Wire(N, I, V, S, T, "-1", N, F, F, "\"Unknown\"", N), true, null),
                C("invariants", "raw-past-old-bound-recorded", Wire(N, I, V, S, T, "65536", N, F, F, "\"Unknown\"", N), true, null),
                C("invariants", "ok-with-raw-zero", Wire(N, I, V, S, T, "0", I, F, F, "\"Ok\"", N), false, "ok_requires_raw_1"),
                C("invariants", "ok-with-raw-two", Wire(N, I, V, S, T, "2", I, F, F, "\"Ok\"", N), false, "ok_requires_raw_1"),
                C("invariants", "legal-completion-without-flag", Wire(N, I, V, S, T, "1", N, F, F, "\"LegalAgreementRequired\"", N), false, "legal_flag_absent"),
                C("invariants", "io-completion-without-flag", Wire(N, I, V, S, T, "1", N, F, F, "\"IoFailure\"", N), false, "io_flag_absent"),
                C("invariants", "undefined-completion", Quiet("\"Delivered\""), false, "completion"),
                C("invariants", "lowercase-completion", Quiet("\"unknown\""), false, "completion"),
                C("invariants", "numeric-completion", Quiet("\"0\""), false, "completion"),
                C("malformed", "truncated", Ok().Substring(0, 60), false, "malformed_json"),
                C("malformed", "trailing-object", Ok() + "{}", false, null),
                C("malformed", "trailing-text", Ok() + "x", false, null),
                C("malformed", "trailing-comma", Retail(",\"note\":null,}"), false, "malformed_json"),
                C("malformed", "comment", Ok().Replace("{\"schema\"", "{/*c*/\"schema\""), false, "malformed_json"),
                C("malformed", "duplicate-field", Retail(",\"note\":null,\"note\":null}"), false, "duplicate_field"),
                C("malformed", "unknown-field", Retail(",\"note\":null,\"extra\":1}"), false, "unknown_field"),
                C("malformed", "missing-field", Retail("}"), false, "field_missing"),
                C("malformed", "item-as-number", Wire(N, ItemText, V, S, T, "1", I, F, F, "\"Ok\"", N), false, "field_type"),
                C("malformed", "raw-as-string", Wire(N, I, V, S, T, "\"1\"", I, F, F, "\"Ok\"", N), false, "field_type"),
                C("malformed", "raw-as-decimal", Wire(N, I, V, S, T, "1.0", I, F, F, "\"Ok\"", N), false, "field_type"),
                C("malformed", "observed-as-string", Wire(N, I, V, S, "\"true\"", "1", I, F, F, "\"Ok\"", N), false, "field_type"),
                C("malformed", "item-as-array", Wire(N, "[\"" + ItemText + "\"]", V, S, T, "1", I, F, F, "\"Ok\"", N), false, "field_type"),
                C("malformed", "item-as-object", Wire(N, "{\"v\":1}", V, S, T, "1", I, F, F, "\"Ok\"", N), false, "field_type"),
                C("malformed", "array-root", "[" + Ok() + "]", false, "wire_not_object"),
                C("malformed", "empty-object", "{}", false, "wire_not_claimed"),
                C("bounds", "over-max-wire-bytes", Ok().Replace("{\"schema\"", "{" + new string(' ', 4200) + "\"schema\""), false, "wire_bounds"),
                C("bounds", "note-over-512", Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", "\"" + new string('x', 513) + "\""), false, "note"),
                C("bounds", "note-at-512", Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", "\"" + new string('x', 512) + "\""), true, null),
                C("bounds", "note-empty", Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", "\"\""), false, "note"),
                C("bounds", "note-control-char", Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", "\"\\u0001\""), false, "note"),
                B("bounds", "byte-order-mark", Bom(), false, "wire_byte_order_mark"),
                B("bounds", "invalid-utf8", Corrupt(), false, "invalid_utf8"),
                B("bounds", "null-wire", null, false, "wire_absent"),
                C("canonical", "uppercase-hash", Ok().Replace(PlanSha, PlanSha.ToUpperInvariant()), false, "planSHA"),
                C("canonical", "hash-63-chars", Ok().Replace(PlanSha, PlanSha.Substring(1)), false, "planSHA"),
                C("canonical", "hash-65-chars", Ok().Replace(PlanSha, PlanSha + "0"), false, "planSHA"),
                C("canonical", "previous-hash-uppercase", Wire("\"" + PlanSha.ToUpperInvariant() + "\"", I, V, S, T, "1", I, F, F, "\"Ok\"", N), false, "previousObservationSHA"),
                C("canonical", "item-leading-zero", Quiet("\"Unknown\"").Replace(I, "\"0" + ItemText + "\""), false, "item"),
                C("canonical", "item-zero", Quiet("\"Unknown\"").Replace(I, "\"0\""), false, "item"),
                C("canonical", "item-non-numeric", Quiet("\"Unknown\"").Replace(I, "\"37947974xx\""), false, "item"),
                C("canonical", "version-two-parts", Quiet("\"Unknown\"").Replace(V, "\"1.2\""), false, "requestVersion"),
                C("canonical", "version-leading-zero", Quiet("\"Unknown\"").Replace(V, "\"01.2.3\""), false, "requestVersion"),
                C("canonical", "stamp-with-offset", Quiet("\"Unknown\"").Replace(S, "\"2026-09-06T14:20:31.1234567+00:00\""), false, "observedUtc"),
                C("canonical", "stamp-three-fraction-digits", Quiet("\"Unknown\"").Replace(S, "\"2026-09-06T14:20:31.123Z\""), false, "observedUtc"),
                C("canonical", "stamp-no-fraction", Quiet("\"Unknown\"").Replace(S, "\"2026-09-06T14:20:31Z\""), false, "observedUtc"),
                C("canonical", "stamp-impossible-day", Quiet("\"Unknown\"").Replace(S, "\"2026-02-30T14:20:31.1234567Z\""), false, "observedUtc"),
                C("canonical", "whitespace", Ok().Replace(",\"item\":", ", \"item\":"), false, "noncanonical_wire"),
                C("canonical", "reordered-fields", Ok().Replace("\"attemptSHA\":\"" + AttemptSha + "\",\"previousObservationSHA\":null",
                    "\"previousObservationSHA\":null,\"attemptSHA\":\"" + AttemptSha + "\""), false, "noncanonical_wire"),
                C("canonical", "escaped-digit-in-item", Quiet("\"Unknown\"").Replace(I, "\"3\\u003794797472\""), false, "noncanonical_wire"),
                C("adversarial", "escaped-property-name", Ok().Replace("\"item\":", "\"\\u0069tem\":"), false, "noncanonical_wire"),
                C("adversarial", "ok-raw-one-accepted", Wire(N, I, V, S, T, "1", I, F, F, "\"Ok\"", N), true, null),
                C("adversarial", "ok-returned-zero", Wire(N, I, V, S, T, "1", "\"0\"", F, F, "\"Ok\"", N), false, "ok_without_clean_callback"),
                C("adversarial", "returned-zero-under-unknown", Wire(N, I, V, S, T, "2", "\"0\"", F, F, "\"Unknown\"", N), true, null),
                C("adversarial", "item-zero-refused", Wire(N, "\"0\"", V, S, T, "2", I, F, F, "\"Unknown\"", N), false, "item"),
                C("adversarial", "returned-item-leading-zero", Wire(N, I, V, S, T, "2", "\"00\"", F, F, "\"Unknown\"", N), false, "returnedItem"),
                C("adversarial", "raw-int32-min", Wire(N, I, V, S, T, "-2147483648", N, F, F, "\"Unknown\"", N), true, null),
                C("adversarial", "raw-int32-max", Wire(N, I, V, S, T, "2147483647", N, F, F, "\"Unknown\"", N), true, null),
                C("adversarial", "raw-past-int32-max", Wire(N, I, V, S, T, "2147483648", N, F, F, "\"Unknown\"", N), false, "field_type"),
                C("adversarial", "raw-past-int32-min", Wire(N, I, V, S, T, "-2147483649", N, F, F, "\"Unknown\"", N), false, "field_type"),
                C("adversarial", "raw-decimal-point", Wire(N, I, V, S, T, "1.0", N, F, F, "\"Unknown\"", N), false, "field_type"),
                C("adversarial", "raw-exponent", Wire(N, I, V, S, T, "1e0", N, F, F, "\"Unknown\"", N), false, "field_type"),
                C("adversarial", "note-raw-utf8-pair", Wire(N, I, V, S, T, "2", I, F, F, "\"Unknown\"", "\"" + Pair + "\""), false, "noncanonical_wire")
            };
        }
        private static int Check(string name, bool condition)
        { if (condition) return 0; Console.WriteLine("FAIL " + name); return 1; }
        private static int Table(Case[] cases)
        {
            int failed = 0;
            for (int i = 0; i < cases.Length; i++)
            {
                ReleaseSubmissionObservation decoded;
                string refusal;
                bool ok = ReleaseSubmissionObservationCodec.TryDecode(cases[i].Wire, out decoded, out refusal);
                if (ok == cases[i].Expect && (ok ? decoded != null && refusal == null : decoded == null && refusal != null)
                    && (cases[i].Refusal == null || cases[i].Refusal == refusal)) continue;
                failed++;
                Console.WriteLine("FAIL " + cases[i].Group + "/" + cases[i].Name + " expected=" + cases[i].Expect
                    + " got=" + ok + " refusal=" + (refusal ?? "<none>"));
            }
            return failed;
        }
        private static int Chain()
        {
            ReleaseSubmissionObservation first, second, decoded;
            string refusal;
            int failed = Check("chain-identity/sha256-known-vector",
                ReleaseSubmissionObservationCodec.Sha256Hex(Utf8.GetBytes("abc"))
                    == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
            failed += Check("chain-identity/first-created", Make(null, out first, out refusal) && refusal == null);
            if (first == null) return failed + 1;
            byte[] bytes = ReleaseSubmissionObservationCodec.EncodeUtf8(first);
            string hash = ReleaseSubmissionObservationCodec.Sha256Hex(bytes);
            failed += Check("chain-identity/hash-shape", hash.Length == 64 && hash == hash.ToLowerInvariant());
            failed += Check("chain-identity/encode-deterministic",
                ReleaseSubmissionObservationCodec.Sha256Hex(ReleaseSubmissionObservationCodec.EncodeUtf8(first)) == hash);
            failed += Check("chain-identity/second-created", Make(hash, out second, out refusal) && refusal == null);
            if (second == null) return failed + 1;
            failed += Check("chain-identity/chain-decodes",
                ReleaseSubmissionObservationCodec.TryDecode(ReleaseSubmissionObservationCodec.EncodeUtf8(second), out decoded, out refusal)
                    && decoded != null && decoded.PreviousObservationSHA == hash && decoded.AttemptSHA == AttemptSha);
            failed += Check("chain-identity/distinct-links", ReleaseSubmissionObservationCodec.Sha256Hex(
                ReleaseSubmissionObservationCodec.EncodeUtf8(second)) != hash);
            return failed;
        }
        private static int Roundtrip()
        {
            ReleaseSubmissionObservation source, decoded;
            string refusal;
            int failed = Check("roundtrip/source-created", Make(null, out source, out refusal));
            if (source == null) return failed + 1;
            string wire = ReleaseSubmissionObservationCodec.Encode(source);
            failed += Check("roundtrip/encode-stable", wire == ReleaseSubmissionObservationCodec.Encode(source));
            failed += Check("roundtrip/schema-is-first-field", wire.StartsWith("{\"schema\":\""
                + ReleaseSubmissionObservationCodec.Schema + "\",\"attemptSHA\":", StringComparison.Ordinal));
            failed += Check("roundtrip/no-whitespace", wire.IndexOf(' ') == wire.IndexOf("alpha lane", StringComparison.Ordinal) + 5);
            failed += Check("roundtrip/claim-matches", ReleaseSubmissionObservationCodec.MatchesSchema(Utf8.GetBytes(wire)));
            failed += Check("roundtrip/over-long-claim-still-matches",
                ReleaseSubmissionObservationCodec.MatchesSchema(Utf8.GetBytes(new string(' ', 5000) + wire)));
            failed += Check("roundtrip/decodes", ReleaseSubmissionObservationCodec.TryDecode(Utf8.GetBytes(wire), out decoded, out refusal));
            if (decoded == null) return failed + 1;
            failed += Check("roundtrip/fields-identical", decoded.AttemptSHA == source.AttemptSHA
                && decoded.PreviousObservationSHA == source.PreviousObservationSHA && decoded.Item == source.Item
                && decoded.RequestVersion == source.RequestVersion && decoded.PlanSHA == source.PlanSHA
                && decoded.ReceiptSHA == source.ReceiptSHA && decoded.ObservedUtc == source.ObservedUtc
                && decoded.CallbackObserved == source.CallbackObserved && decoded.RawEResult == source.RawEResult
                && decoded.ReturnedItem == source.ReturnedItem && decoded.IoFailure == source.IoFailure
                && decoded.LegalAgreementRequired == source.LegalAgreementRequired
                && decoded.Completion == source.Completion && decoded.Note == source.Note);
            failed += Check("roundtrip/re-encode-identical", ReleaseSubmissionObservationCodec.Encode(decoded) == wire);
            return failed;
        }
        private static bool Probe(int raw, string returned, string note,
            out ReleaseSubmissionObservation value, out string refusal)
        {
            return ReleaseSubmissionObservation.TryCreate(AttemptSha, null, ItemText, "0.3.0", PlanSha, ReceiptSha,
                "2026-09-06T14:20:31.1234567Z", true, raw, returned, false, false,
                ReleaseSubmissionCompletion.Unknown, note, out value, out refusal);
        }
        /// <summary>Create, encode, decode: the opaque raw value, the returned id and the note must survive exactly.</summary>
        private static int Trip(string name, int raw, string returned, string note)
        {
            ReleaseSubmissionObservation source, decoded; string refusal; probes++;
            if (!Probe(raw, returned, note, out source, out refusal) || source == null)
                return Check("adversarial/" + name + " create=" + (refusal ?? "<none>"), false);
            return Check("adversarial/" + name, ReleaseSubmissionObservationCodec.TryDecode(
                    ReleaseSubmissionObservationCodec.EncodeUtf8(source), out decoded, out refusal)
                && decoded != null && refusal == null && decoded.RawEResult == raw
                && decoded.ReturnedItem == returned && decoded.Note == note);
        }
        /// <summary>A canonical wire whose note becomes a lone escaped surrogate must be refused with a
        /// reason and no observation, and must never throw out of TryDecode.</summary>
        private static int Escaped(string escape)
        {
            ReleaseSubmissionObservation source, decoded; string refusal; probes++;
            string name = "adversarial/malformed-surrogate-" + escape;
            if (!Probe(2, ItemText, "probe", out source, out refusal)) return Check(name + " create", false);
            string wire = ReleaseSubmissionObservationCodec.Encode(source).Replace("\"probe\"", "\"" + escape + "\"");
            try
            {
                bool ok = ReleaseSubmissionObservationCodec.TryDecode(Utf8.GetBytes(wire), out decoded, out refusal);
                return Check(name, !ok && decoded == null && refusal != null);
            }
            catch (Exception error) { return Check(name + " threw " + error.GetType().Name, false); }
        }
        private static int Adversarial()
        {
            return Trip("opaque-result--2147483648", int.MinValue, ItemText, "probe")
                + Trip("opaque-result--1", -1, ItemText, "probe")
                + Trip("opaque-result-65536", 65536, ItemText, "probe")
                + Trip("opaque-result-2147483647", int.MaxValue, ItemText, "probe")
                + Trip("failed-returned-zero", 2, "0", "probe")
                + Trip("surrogate-pair-note", 2, ItemText, Pair)
                + Escaped("\\uD800") + Escaped("\\uDC00") + Escaped("\\uD800x");
        }
        private static int Authority()
        {
            string[] banned = { "Archive", "Retry", "Deliver", "Release", "Publish", "Apply" };
            Type[] types = { typeof(ReleaseSubmissionObservation), typeof(ReleaseSubmissionObservationCodec) };
            int failed = 0;
            for (int t = 0; t < types.Length; t++)
            {
                MemberInfo[] members = types[t].GetMembers(BindingFlags.Public | BindingFlags.Static
                    | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                for (int m = 0; m < members.Length; m++)
                    for (int b = 0; b < banned.Length; b++)
                        failed += Check("no-authority/" + types[t].Name + "." + members[m].Name,
                            members[m].Name.IndexOf(banned[b], StringComparison.Ordinal) < 0);
            }
            return failed;
        }
        public static int Run()
        {
            Case[] cases = Cases();
            int failed = Table(cases) + Chain() + Roundtrip() + Adversarial() + Authority();
            Console.WriteLine("ReleaseSubmissionObservationTests: 10 groups (success, fault-outcomes, invariants,"
                + " malformed, bounds, canonical, adversarial, chain-identity, roundtrip, no-authority); "
                + cases.Length + " wire cases; " + probes + " adversarial probes; " + failed + " failure(s).");
            return failed;
        }
        public static int Main() { return Run() == 0 ? 0 : 1; }
    }
}
