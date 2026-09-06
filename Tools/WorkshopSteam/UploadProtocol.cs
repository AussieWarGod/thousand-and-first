using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed class UploadRequest
    {
        public readonly ulong Item;
        public readonly string ManifestId, Version, Title, Description, ContentPath, PreviewPath, ChangeNote;
        public readonly ReadOnlyCollection<string> Tags;
        public readonly int SteamVisibility;

        public UploadRequest(ulong item, string manifestId, string version, string title,
            string description, string contentPath, string previewPath, string[] tags,
            int steamVisibility, string changeNote)
        {
            Item = item; ManifestId = manifestId; Version = version; Title = title;
            Description = description; ContentPath = contentPath; PreviewPath = previewPath;
            Tags = tags == null ? null : Array.AsReadOnly((string[])tags.Clone());
            SteamVisibility = steamVisibility; ChangeNote = changeNote;
        }
    }

    public enum UploadOperation
    {
        ValidateRequest, Prepare, Revalidate, StartUpdate, SetTitle, SetDescription, SetTags,
        SetVisibility, SetContent, SetPreview, RemoveManifestId, AddManifestId,
        RemoveManifestVersion, AddManifestVersion, RecordAttempt, Submit, AwaitCompletion
    }

    public enum UploadPhase { Validation, Preparation, Authority, Updating, Recording, Submitting, AwaitingCompletion }
    public enum UploadStatus { InProgress, Aborted, Uncertain, NeedsUser, Rejected, SubmittedUnverified }
    public enum UploadReason { None, InvalidRequest, Refused, Exception, Timeout, IoFailure, LegalAgreement, ServerRejected, UnknownCompletion }
    public enum UploadCompletion { Unknown, Ok, Rejected, LegalAgreementRequired, TimedOut, IoFailure }

    /// <summary>The adapter owns its update handle and source lease. Prepare acquires readiness;
    /// Revalidate re-proves target authority and exact leased content. Setters affect only that
    /// update. RecordAttempt returns true only after the exact attempt is durably recorded.
    /// Submit initiates at most one request; false or exception cannot prove no request escaped.
    /// WaitForCompletion must be bounded and prefer LegalAgreementRequired whenever that flag
    /// is present, even on an otherwise successful callback. No operation may retry submission.</summary>
    public interface IWorkshopUploadPort
    {
        bool Try(UploadOperation operation, UploadRequest request,
            string metadataKey = null, string metadataValue = null);
        UploadCompletion WaitForCompletion();
    }

    public sealed class UploadResult
    {
        public readonly UploadStatus Status;
        public readonly UploadPhase Phase;
        public readonly UploadOperation Operation;
        public readonly UploadReason Reason;

        internal UploadResult(UploadStatus status, UploadPhase phase, UploadOperation operation, UploadReason reason)
        {
            Status = status; Phase = phase; Operation = operation; Reason = reason;
        }
    }

    /// <summary>Runs one in-memory attempt. Every later call returns its existing outcome without
    /// touching the port, including after uncertainty. Durable cross-process replay prevention and
    /// subscriber delivery verification belong to the caller; this protocol proves neither.</summary>
    public sealed class UploadProtocol
    {
        private readonly object gate = new object();
        private UploadResult result;
        private bool started;
        private static readonly UploadOperation[] BeforeSubmit =
        {
            UploadOperation.Prepare, UploadOperation.Revalidate, UploadOperation.StartUpdate,
            UploadOperation.SetTitle, UploadOperation.SetDescription, UploadOperation.SetTags,
            UploadOperation.SetVisibility, UploadOperation.SetContent, UploadOperation.SetPreview,
            UploadOperation.RemoveManifestId, UploadOperation.AddManifestId,
            UploadOperation.RemoveManifestVersion, UploadOperation.AddManifestVersion,
            UploadOperation.Revalidate, UploadOperation.RecordAttempt
        };

        public UploadResult Run(UploadRequest request, IWorkshopUploadPort port)
        {
            lock (gate)
            {
                if (started) return result;
                started = true;
                result = new UploadResult(UploadStatus.InProgress, UploadPhase.Validation,
                    UploadOperation.ValidateRequest, UploadReason.None);
                result = Execute(request, port);
                return result;
            }
        }

        private static UploadResult Execute(UploadRequest request, IWorkshopUploadPort port)
        {
            UploadOperation operation = UploadOperation.ValidateRequest;
            bool submitted = false;
            try
            {
                if (port == null || !Valid(request))
                    return Result(UploadStatus.Aborted, operation, UploadReason.InvalidRequest);
                foreach (UploadOperation next in BeforeSubmit)
                {
                    operation = next;
                    string key = null, value = null;
                    if (operation == UploadOperation.RemoveManifestId || operation == UploadOperation.AddManifestId)
                        key = "manifest_id";
                    if (operation == UploadOperation.RemoveManifestVersion || operation == UploadOperation.AddManifestVersion)
                        key = "manifest_version";
                    if (operation == UploadOperation.AddManifestId) value = request.ManifestId;
                    if (operation == UploadOperation.AddManifestVersion) value = request.Version;
                    if (!port.Try(operation, request, key, value))
                        return Result(UploadStatus.Aborted, operation, UploadReason.Refused);
                }
                operation = UploadOperation.Submit;
                submitted = true;
                if (!port.Try(operation, request))
                    return Result(UploadStatus.Uncertain, operation, UploadReason.Refused);
                operation = UploadOperation.AwaitCompletion;
                switch (port.WaitForCompletion())
                {
                    case UploadCompletion.Ok:
                        return Result(UploadStatus.SubmittedUnverified, operation, UploadReason.None);
                    case UploadCompletion.LegalAgreementRequired:
                        return Result(UploadStatus.NeedsUser, operation, UploadReason.LegalAgreement);
                    case UploadCompletion.Rejected:
                        return Result(UploadStatus.Rejected, operation, UploadReason.ServerRejected);
                    case UploadCompletion.TimedOut:
                        return Result(UploadStatus.Uncertain, operation, UploadReason.Timeout);
                    case UploadCompletion.IoFailure:
                        return Result(UploadStatus.Uncertain, operation, UploadReason.IoFailure);
                    default:
                        return Result(UploadStatus.Uncertain, operation, UploadReason.UnknownCompletion);
                }
            }
            catch (Exception)
            {
                return Result(submitted ? UploadStatus.Uncertain : UploadStatus.Aborted,
                    operation, UploadReason.Exception);
            }
        }

        private static UploadResult Result(UploadStatus status, UploadOperation operation, UploadReason reason)
        {
            UploadPhase phase = operation == UploadOperation.ValidateRequest ? UploadPhase.Validation
                : operation == UploadOperation.Prepare ? UploadPhase.Preparation
                : operation == UploadOperation.Revalidate ? UploadPhase.Authority
                : operation == UploadOperation.RecordAttempt ? UploadPhase.Recording
                : operation == UploadOperation.Submit ? UploadPhase.Submitting
                : operation == UploadOperation.AwaitCompletion ? UploadPhase.AwaitingCompletion : UploadPhase.Updating;
            return new UploadResult(status, phase, operation, reason);
        }

        private static bool Valid(UploadRequest request)
        {
            if (request == null || request.Item == 0 || !Text(request.ManifestId, 255)
                || !Text(request.Version, 128) || !Regex.IsMatch(request.Version,
                    @"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z")
                || !Text(request.Title, 128) || !Text(request.Description, 7999, true)
                || !Text(request.ContentPath, 4096) || !Path.IsPathFullyQualified(request.ContentPath)
                || !Text(request.PreviewPath, 4096) || !Path.IsPathFullyQualified(request.PreviewPath)
                || !Text(request.ChangeNote, 8000, true, true)
                || request.SteamVisibility != 0 && request.SteamVisibility != 2
                || request.Tags == null || request.Tags.Count == 0 || request.Tags.Count > 64) return false;
            HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string tag in request.Tags)
                if (!Text(tag, 255) || !tags.Add(tag)) return false;
            return true;
        }

        private static bool Text(string value, int limit, bool multiline = false, bool empty = false)
        {
            if (value == null || value.Length > limit || !empty && value.Length == 0
                || value != value.Trim()) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsControl(character)
                    && (!multiline || character != '\n' && character != '\r' && character != '\t')) return false;
                if (!char.IsSurrogate(character)) continue;
                if (!char.IsHighSurrogate(character) || i + 1 == value.Length
                    || !char.IsLowSurrogate(value[++i])) return false;
            }
            return Encoding.UTF8.GetByteCount(value) <= limit;
        }
    }
}
