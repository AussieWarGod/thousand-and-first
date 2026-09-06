using System;
using System.IO;

namespace ThousandAndFirst.WorkshopSteam
{
    public interface IWorkshopSubmissionEvidencePort
    {
        string PersistSubmissionObservation();
        bool VerifyPublishedMetadata();
    }

    public sealed class UploadAftermathResult
    {
        public readonly string ObservationSHA;
        public readonly bool ContentUnchanged, MetadataMatches;

        internal UploadAftermathResult(string observationSHA, bool contentUnchanged, bool metadataMatches)
        {
            ObservationSHA = observationSHA;
            ContentUnchanged = contentUnchanged;
            MetadataMatches = metadataMatches;
        }
    }

    // One invocation orders evidence before later checks. It grants no retry or delivery authority.
    public static class UploadAftermath
    {
        public static UploadAftermathResult Run(UploadResult result, Func<bool> revalidateContent,
            IWorkshopSubmissionEvidencePort port)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (revalidateContent == null) throw new ArgumentNullException(nameof(revalidateContent));
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (result.Status == UploadStatus.InProgress || !Enum.IsDefined(typeof(UploadStatus), result.Status))
                throw new ArgumentException("Upload aftermath requires a terminal result.", nameof(result));

            string observationSHA = port.PersistSubmissionObservation();
            if (observationSHA != null && !CanonicalSHA(observationSHA))
                throw new InvalidDataException("Submission observation SHA is not canonical.");
            bool submitted = result.Status == UploadStatus.SubmittedUnverified;
            if (submitted && observationSHA == null)
                throw new InvalidDataException("Submitted upload has no persisted observation SHA.");

            bool unchanged = revalidateContent();
            bool metadata = submitted && unchanged && port.VerifyPublishedMetadata();
            return new UploadAftermathResult(observationSHA, unchanged, metadata);
        }

        private static bool CanonicalSHA(string value)
        {
            if (value.Length != 64) return false;
            foreach (char c in value)
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f')) return false;
            return true;
        }
    }
}
