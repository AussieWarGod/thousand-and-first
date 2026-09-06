using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class SteamPublish
    {
        // Exit 9 stays reserved for the launcher's own "helper did not report" sentinel.
        private const int ExitOk = 0, ExitRefused = 2, ExitCheckRefused = 3, ExitNeedsUser = 4,
            ExitSubmitUncertain = 5, ExitStateRootRefused = 6, ExitAttemptRetained = 7,
            ExitLockAbandoned = 8, ExitCleanupUncertain = 10;
        private const string StateRootRefused = "state_root_refused", AttemptRetained = "attempt_retained",
            LockAbandonedUncertain = "lock_abandoned_uncertain", CleanupUncertain = "cleanup_uncertain",
            Inspected = "inspected", Uncertain = "Uncertain", Refused = "refused";

        public static int Main(string[] args)
        {
            bool submissionPossible = false;
            string attempt = null;
            Dictionary<string, object> report;
            int exitCode;
            WorkshopReleaseRegistry registry = null;
            UploadPackage package = null;
            SteamUploadPort port = null;
            Exception cleanupFailure = null;
            try
            {
                if (args == null || args.Length != 7 || (args[0] != "check" && args[0] != "submit" && args[0] != "inspect"))
                    throw new ArgumentException(
                        "Expected check|submit|inspect PLAN PLAN_SHA ITEM CHANGE_NOTE ATTEMPT_ROOT RECEIPT_SHA.");
                // The compiled registry root is proved first: before any SDK call, item mutex,
                // marker initialization, attempt lease or file read. Nothing above touches the disk.
                if (!IsFixedStateRoot(args[5]))
                {
                    Print(Report(StateRootRefused, args[3]));
                    return ExitStateRootRefused;
                }
                ulong item = ParseItem(args[3]);
                string note = args[0] == "inspect" ? null : ReadNote(args[4]);
                // Open acquires the per-item kernel mutex and initializes the immutable marker.
                // The mutex stays held until this registry is disposed, which cleanup does last.
                registry = WorkshopReleaseRegistry.Open(item);
                bool retained = registry.HasRetainedAttempt();
                if (retained || args[0] == "inspect")
                {
                    // Inspection reuses the registry's exact proofs and creates no attempt.
                    report = Report(retained ? AttemptRetained : Inspected, args[3]);
                    report["proofs"] = registry.Inspect();
                    report["retainedAttempt"] = retained;
                    exitCode = retained ? ExitAttemptRetained : ExitOk;
                }
                else
                {
                    package = UploadPackage.Open(args[1], args[2], args[3], note);
                    if (package.ReceiptSHA != args[6]) throw new InvalidDataException("Receipt approval mismatch.");
                    RefuseOverlappingContent(package.Request.ContentPath);
                    if (args[0] == "check")
                    {
                        // Named, never created: check proves readiness without an attempt.
                        string planned = registry.PlannedAttemptPath();
                        port = new SteamUploadPort(package, planned);
                        bool ready = port.Try(UploadOperation.Prepare, package.Request)
                            && port.Try(UploadOperation.Revalidate, package.Request);
                        report = Report(ready ? "checked_not_submitted" : "check_refused", args[3]);
                        report["version"] = package.Request.Version;
                        report["planSHA"] = package.PlanSHA;
                        report["receiptSHA"] = package.ReceiptSHA;
                        report["plannedAttempt"] = planned;
                        report["attemptCreated"] = false;
                        report["diagnostics"] = port.LastDiagnostics;
                        exitCode = ready ? ExitOk : ExitCheckRefused;
                    }
                    else
                    {
                        submissionPossible = true;
                        // The durable attempt directory exists before the protocol can submit,
                        // and every failure below retains it. Nothing here deletes or moves it.
                        attempt = registry.BeginFirstAttempt();
                        port = new SteamUploadPort(package, attempt);
                        UploadResult result = new UploadProtocol().Run(package.Request, port);
                        UploadAftermathResult aftermath = UploadAftermath.Run(result, package.Revalidate, port);
                        report = Report(aftermath.ContentUnchanged ? result.Status.ToString() : Uncertain, args[3]);
                        report["phase"] = result.Phase.ToString();
                        report["operation"] = result.Operation.ToString();
                        report["reason"] = result.Reason.ToString();
                        report["version"] = package.Request.Version;
                        report["planSHA"] = package.PlanSHA;
                        report["receiptSHA"] = package.ReceiptSHA;
                        report["attempt"] = attempt;
                        report["observationSHA"] = aftermath.ObservationSHA;
                        report["contentUnchanged"] = aftermath.ContentUnchanged;
                        report["metadataMatches"] = aftermath.MetadataMatches;
                        report["diagnostics"] = port.LastDiagnostics;
                        exitCode = result.Status == UploadStatus.SubmittedUnverified && aftermath.MetadataMatches
                            ? ExitOk : result.Status == UploadStatus.NeedsUser ? ExitNeedsUser : ExitSubmitUncertain;
                    }
                    // The mutex and the marker are still exact after the protocol, before cleanup.
                    registry.RequireExact();
                }
            }
            catch (WorkshopReleaseRegistry.RetainedAttempt error)
            {
                // A retained attempt is reconciliation work, never an automatic later attempt.
                report = Report(AttemptRetained, args != null && args.Length > 3 ? args[3] : null);
                report["errorType"] = error.GetType().Name;
                report["attempt"] = attempt;
                exitCode = ExitAttemptRetained;
            }
            catch (WorkshopReleaseRegistry.AbandonedLock error)
            {
                // An abandoned mutex is uncertainty about an earlier holder, not permission to retry.
                report = Report(LockAbandonedUncertain, args != null && args.Length > 3 ? args[3] : null);
                report["errorType"] = error.GetType().Name;
                exitCode = ExitLockAbandoned;
            }
            catch (Exception error)
            {
                // Do not print exception messages: native paths or SDK diagnostics may carry account data.
                report = Report(submissionPossible ? Uncertain : Refused, args != null && args.Length > 3 ? args[3] : null);
                report["errorType"] = error.GetType().Name;
                report["attempt"] = attempt;
                exitCode = submissionPossible ? ExitSubmitUncertain : ExitRefused;
            }
            finally { cleanupFailure = Release(port, package, registry); }
            if (cleanupFailure != null)
            {
                // Cleanup failure keeps every proven field and reports its own uncertainty.
                report["outcomeStatus"] = report["status"];
                report["status"] = CleanupUncertain;
                report["cleanupErrorTypes"] = ErrorTypes(cleanupFailure);
                Print(report);
                return ExitCleanupUncertain;
            }
            Print(report);
            return exitCode;
        }

        /// <summary>Byte-exact ordinal comparison against the compiled registry root. The launcher
        /// passes that literal verbatim, so no normalisation - and no filesystem access - runs first.
        /// A trailing separator, a different case or any alternate root is refused, not repaired.</summary>
        internal static bool IsFixedStateRoot(string stateRoot)
        {
            return string.Equals(stateRoot, WorkshopReleaseRegistry.StateRoot, StringComparison.Ordinal);
        }

        // Shared shape: every report names the fixed root, the item, and grants nothing.
        private static Dictionary<string, object> Report(string status, string item)
        {
            return new Dictionary<string, object>
            {
                { "status", status },
                { "item", item },
                { "stateRoot", WorkshopReleaseRegistry.StateRoot },
                { "retryAuthorized", false },
                { "delivered", false }
            };
        }

        // Cleanup runs the SDK port, then the leased package, then the registry, whose disposal
        // releases the item mutex last. Every step is attempted even when an earlier step throws.
        private static Exception Release(SteamUploadPort port, UploadPackage package, WorkshopReleaseRegistry registry)
        {
            try
            {
                UploadCleanup.Run(
                    delegate { if (port != null) port.Dispose(); },
                    delegate { if (package != null) package.Dispose(); },
                    delegate { if (registry != null) registry.Dispose(); });
                return null;
            }
            catch (Exception error) { return error; }
        }

        private static string[] ErrorTypes(Exception error)
        {
            List<string> names = new List<string> { error.GetType().Name };
            AggregateException group = error as AggregateException;
            if (group != null)
                foreach (Exception inner in group.InnerExceptions)
                {
                    if (names.Count >= 5) break;
                    names.Add(inner.GetType().Name);
                }
            return names.ToArray();
        }

        private static ulong ParseItem(string value)
        {
            ulong item;
            if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out item)
                || item.ToString(CultureInfo.InvariantCulture) != value)
                throw new ArgumentException("Expected one decimal Workshop item id.");
            return item;
        }

        private static string ReadNote(string path)
        {
            Ordinary(path, false);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length == 0 || stream.Length > 7999) throw new InvalidDataException("Change note bounds.");
                byte[] bytes = new byte[(int)stream.Length];
                stream.ReadExactly(bytes);
                return new UTF8Encoding(false, true).GetString(bytes).Trim();
            }
        }

        // The registry decides the attempt path; only its separation from content is checked here.
        private static void RefuseOverlappingContent(string contentPath)
        {
            string root = WorkshopReleaseRegistry.StateRoot;
            string content = Path.GetFullPath(contentPath).TrimEnd('\\');
            if (Within(root, content) || Within(content, root))
                throw new InvalidDataException("Registry state must be separate from package content.");
        }

        private static bool Within(string path, string directory)
        {
            directory = directory.TrimEnd('\\');
            return string.Equals(path, directory, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(directory + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static string Ordinary(string path, bool directory)
        {
            if (!OperatingSystem.IsWindows() || !Path.IsPathFullyQualified(path)
                || path.StartsWith(@"\\", StringComparison.Ordinal))
                throw new InvalidDataException("Expected local Windows path.");
            string full = Path.GetFullPath(path);
            FileSystemInfo entry = directory ? (FileSystemInfo)new DirectoryInfo(full) : new FileInfo(full);
            if (!entry.Exists || ((entry.Attributes & FileAttributes.Directory) != 0) != directory)
                throw new InvalidDataException("Unexpected file type.");
            while (entry != null)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked path refused.");
                DirectoryInfo folder = entry as DirectoryInfo;
                entry = folder != null ? folder.Parent : ((FileInfo)entry).Directory;
            }
            return full;
        }

        private static void Print(object result)
        {
            Console.WriteLine(JsonSerializer.Serialize(result));
        }
    }
}
