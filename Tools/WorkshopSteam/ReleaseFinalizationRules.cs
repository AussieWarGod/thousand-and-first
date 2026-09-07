using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    /// <summary>Pure record checks, not publication capabilities. A native registry must hold and
    /// re-prove every input lease; finalization creation additionally needs a fresh SDK verification.</summary>
    internal static class ReleaseFinalizationRules
    {
        internal const int MaxAttempts = 64;
        private const string Schema = "taf-release-finalization-v1";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] AttemptFields = { "attemptId", "item", "manifestId",
            "requestVersion", "steamVisibility", "planSHA", "receiptSHA", "packagePath", "contentPath", "startUtc" };

        internal static bool Higher(string requested, string prior)
        {
            int patch, before;
            return Patch(requested, out patch) && (prior == null || Patch(prior, out before) && patch > before);
        }

        internal static bool VersionAllowed(string item, string requested, string prior)
        {
            if (item == "3794797472") return Higher(requested, prior);
            int patch, before;
            return item == "3796495680" && Patch(requested, out patch)
                && (prior == null || Patch(prior, out before) && patch >= before);
        }

        internal static bool UnseenInventory(string inventory, IList<string> earlier)
        {
            if (!Hash(inventory) || earlier == null || earlier.Count > MaxAttempts) return false;
            foreach (string previous in earlier)
                if (!Hash(previous) || string.Equals(inventory, previous, StringComparison.Ordinal)) return false;
            return true;
        }

        private static bool Patch(string version, out int patch)
        {
            patch = -1;
            return version != null && version.StartsWith("0.3.", StringComparison.Ordinal)
                && version.Length <= 13 && int.TryParse(version.Substring(4), NumberStyles.None,
                    CultureInfo.InvariantCulture, out patch)
                && version == "0.3." + patch.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool Clean(ReleaseSubmissionObservation value)
        {
            return value != null && value.PreviousObservationSHA == null && value.CallbackObserved
                && value.Completion == ReleaseSubmissionCompletion.Ok && value.RawEResult == 1
                && value.ReturnedItem == value.Item && value.IoFailure == false
                && value.LegalAgreementRequired == false && Higher(value.RequestVersion, null);
        }

        /// <summary>Checks the original ten-field attempt's exact native writer bytes, not a
        /// deserializer's defaults. Unknown, partial or merely successful-looking observations refuse.</summary>
        internal static bool TrySubmission(byte[] attempt, byte[] submitted, string item,
            out ReleaseSubmissionObservation observation, out string contentPath)
        {
            observation = null; contentPath = null;
            ReleaseSubmissionObservation value; string reason;
            if (!ReleaseSubmissionObservationCodec.TryDecode(submitted, out value, out reason)
                || !Clean(value) || value.Item != item || (item != "3794797472" && item != "3796495680")
                || attempt == null || attempt.Length == 0 || attempt.Length > 65536
                || value.AttemptSHA != Sha(attempt)) return false;
            try
            {
                Utf8.GetCharCount(attempt);
                using (JsonDocument document = JsonDocument.Parse(attempt, new JsonDocumentOptions
                { MaxDepth = 2, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow }))
                using (MemoryStream stream = new MemoryStream())
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
                    string[] fields = new string[10]; int count = 0, visibility = -1;
                    using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            if (count >= AttemptFields.Length || property.Name != AttemptFields[count]) return false;
                            if (count == 4)
                            {
                                if (property.Value.ValueKind != JsonValueKind.Number
                                    || !property.Value.TryGetInt32(out visibility)) return false;
                                writer.WriteNumber(property.Name, visibility);
                            }
                            else
                            {
                                if (property.Value.ValueKind != JsonValueKind.String) return false;
                                fields[count] = property.Value.GetString();
                                if (string.IsNullOrEmpty(fields[count]) || fields[count].Length > 32767
                                    || !ReleaseSubmissionObservation.WellFormedText(fields[count])) return false;
                                writer.WriteString(property.Name, fields[count]);
                            }
                            count++;
                        }
                        writer.WriteEndObject(); writer.Flush();
                    }
                    Guid id; DateTime stamp;
                    if (count != 10 || !Equal(attempt, stream.ToArray())
                        || !Guid.TryParseExact(fields[0], "D", out id) || id == Guid.Empty || fields[0] != id.ToString("D")
                        || fields[1] != item || fields[2] != "r_ThousandAndFirst"
                        || fields[3] != value.RequestVersion || visibility != (item == "3794797472" ? 0 : 2)
                        || fields[5] != value.PlanSHA || fields[6] != value.ReceiptSHA
                        || fields[7] != fields[8] || !ContentPath(fields[8])
                        || !DateTime.TryParseExact(fields[9], ReleaseSubmissionObservation.StampFormat,
                            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out stamp)
                        || fields[9] != stamp.ToString(ReleaseSubmissionObservation.StampFormat, CultureInfo.InvariantCulture)
                        || string.CompareOrdinal(fields[9], value.ObservedUtc) > 0) return false;
                    observation = value; contentPath = fields[8]; return true;
                }
            }
            catch (JsonException) { return false; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static bool ContentPath(string path)
        {
            if (path == null || path.Length < 4 || path[0] < 'A' || path[0] > 'Z'
                || path[1] != ':' || path[2] != '\\' || path.IndexOf('/') >= 0) return false;
            foreach (string part in path.Substring(3).Split('\\'))
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".", StringComparison.Ordinal)
                    || part.EndsWith(" ", StringComparison.Ordinal) || part.IndexOfAny("<>:\"|?*".ToCharArray()) >= 0) return false;
            return true;
        }

        internal static bool Binds(ReleaseSubmissionObservation submission, ReleaseInstallationObservation installed,
            string markerSHA, string directorySHA)
        {
            return Clean(submission) && installed != null && installed.AttemptSHA == submission.AttemptSHA
                && installed.SubmissionSHA == Sha(ReleaseSubmissionObservationCodec.EncodeUtf8(submission))
                && installed.Item == submission.Item && installed.Version == submission.RequestVersion
                && installed.PlanSHA == submission.PlanSHA && installed.ReceiptSHA == submission.ReceiptSHA
                && installed.RegistryMarkerSHA == markerSHA && installed.AttemptDirectorySHA == directorySHA
                && string.CompareOrdinal(installed.ObservedUtc, submission.ObservedUtc) >= 0;
        }

        /// <summary>Encoding is not creation authority. The only production caller writes these
        /// bytes after SteamInstalledDelivery.Verify has completed its entire SDK cleanup.</summary>
        internal static byte[] FinalizationBytes(int ordinal, string previousSHA, string installationSHA)
        {
            if (ordinal < 1 || ordinal > MaxAttempts || (ordinal == 1 ? previousSHA != "" : !Hash(previousSHA))
                || !Hash(installationSHA)) throw new InvalidDataException("Finalization identity bounds.");
            return JsonSerializer.SerializeToUtf8Bytes(new[] { Schema,
                ordinal.ToString(CultureInfo.InvariantCulture), previousSHA, installationSHA });
        }

        internal static bool Finalized(ReleaseSubmissionObservation submission, byte[] installedBytes,
            byte[] finalization, int ordinal, string previousSHA, string markerSHA, string directorySHA,
            out ReleaseInstallationObservation installation)
        {
            installation = null;
            ReleaseInstallationObservation value;
            if (!ReleaseInstallationObservation.TryDecode(installedBytes, out value)
                || !Binds(submission, value, markerSHA, directorySHA)
                || finalization == null || finalization.Length > 512) return false;
            try
            {
                if (!Equal(finalization, FinalizationBytes(ordinal, previousSHA, Sha(installedBytes)))) return false;
                installation = value; return true;
            }
            catch (InvalidDataException) { return false; }
        }

        internal static bool Hash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if ((c < '0' || c > '9') && (c < 'a' || c > 'f')) return false;
            return true;
        }
        internal static string Sha(byte[] bytes) { return ReleaseSubmissionObservationCodec.Sha256Hex(bytes); }
        internal static bool Equal(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
