using System;
using System.Text;
using System.Text.Json;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    /// <summary>Immutable account of one verified client installation, linked to retained submission
    /// and registry identities. Parsing proves only wire shape, never delivery, finalization or retry.
    /// The installation need not have been transferred by the linked submission.</summary>
    internal sealed partial class ReleaseInstallationObservation
    {
        internal const string Schema = "taf-release-installation-v1";
        internal const string Scope = "one_subscribed_client_installation";
        internal const int MaxBytes = 4096;
        internal readonly string AttemptSHA, SubmissionSHA, Item, Version, PlanSHA, ReceiptSHA;
        internal readonly string RegistryMarkerSHA, AttemptDirectorySHA, InventorySHA, ObservedUtc;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private ReleaseInstallationObservation(string[] values)
        {
            AttemptSHA = values[1]; SubmissionSHA = values[2]; Item = values[3]; Version = values[4];
            PlanSHA = values[5]; ReceiptSHA = values[6]; RegistryMarkerSHA = values[7];
            AttemptDirectorySHA = values[8]; InventorySHA = values[9]; ObservedUtc = values[10];
        }

        internal byte[] Encode()
        {
            return JsonSerializer.SerializeToUtf8Bytes(new[] { Schema, AttemptSHA, SubmissionSHA, Item,
                Version, PlanSHA, ReceiptSHA, RegistryMarkerSHA, AttemptDirectorySHA, InventorySHA, ObservedUtc, Scope });
        }

        /// <summary>Fixed twelve-string array; bounded strict UTF-8 and byte-identical re-encoding.
        /// No property defaults, omitted values, extra elements, comments or trailing tokens.</summary>
        internal static bool TryDecode(byte[] bytes, out ReleaseInstallationObservation result)
        {
            result = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes) return false;
            try
            {
                Utf8.GetCharCount(bytes);
                using (JsonDocument doc = JsonDocument.Parse(bytes, new JsonDocumentOptions
                { MaxDepth = 2, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow }))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() != 12)
                        return false;
                    string[] values = new string[12]; int i = 0;
                    foreach (JsonElement value in doc.RootElement.EnumerateArray())
                    {
                        if (value.ValueKind != JsonValueKind.String) return false;
                        values[i++] = value.GetString();
                    }
                    if (!Valid(values)) return false;
                    var parsed = new ReleaseInstallationObservation(values);
                    byte[] canonical = parsed.Encode();
                    if (canonical.Length != bytes.Length) return false;
                    for (i = 0; i < bytes.Length; i++) if (bytes[i] != canonical[i]) return false;
                    result = parsed; return true;
                }
            }
            catch (JsonException) { return false; }
            catch (DecoderFallbackException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static bool Valid(string[] values)
        {
            if (values == null || values.Length != 12 || values[0] != Schema || values[11] != Scope
                || (values[3] != "3794797472" && values[3] != "3796495680")
                || values[4] == null || !values[4].StartsWith("0.3.", StringComparison.Ordinal)) return false;
            foreach (int index in new[] { 1, 2, 5, 6, 7, 8, 9 }) if (!Hash(values[index])) return false;
            ReleaseSubmissionObservation shape; string refusal;
            return ReleaseSubmissionObservation.TryCreate(values[1], null, values[3], values[4], values[5],
                values[6], values[10], false, null, null, null, null, ReleaseSubmissionCompletion.Unknown,
                null, out shape, out refusal);
        }

        private static bool Hash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if ((c < '0' || c > '9') && (c < 'a' || c > 'f')) return false;
            return true;
        }
    }
}
