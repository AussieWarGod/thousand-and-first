using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class UploadProtocolTests
    {
        private sealed class Case
        {
            internal readonly string Name;
            internal readonly Action Body;
            internal Case(string name, Action body) { Name = name; Body = body; }
        }

        private sealed class Call
        {
            internal UploadOperation Operation;
            internal UploadRequest Request;
            internal string Key, Value;
        }

        private sealed class Fake : IWorkshopUploadPort
        {
            internal readonly List<Call> Calls = new List<Call>();
            internal readonly Dictionary<string, List<string>> Metadata = new Dictionary<string, List<string>>
            {
                { "manifest_id", new List<string> { "old", "old-duplicate" } },
                { "manifest_version", new List<string> { "0.1.0", "0.2.0" } },
                { "foreign", new List<string> { "untouched" } }
            };
            internal Func<Call, int, bool> OnTry;
            internal Func<UploadCompletion> OnWait;
            internal UploadCompletion Completion = UploadCompletion.Ok;
            internal int Starts, Submits, Waits;
            internal bool Durable;

            public bool Try(UploadOperation operation, UploadRequest request, string metadataKey = null, string metadataValue = null)
            {
                var call = new Call { Operation = operation, Request = request, Key = metadataKey, Value = metadataValue };
                Calls.Add(call);
                if (operation == UploadOperation.StartUpdate) Starts++;
                if (operation == UploadOperation.Submit)
                {
                    Submits++;
                    True(Durable, "submission must follow a durable attempt record");
                }
                int occurrence = Calls.Count(row => row.Operation == operation);
                if (OnTry != null && !OnTry(call, occurrence)) return false;
                if (operation == UploadOperation.RecordAttempt) Durable = true;
                if (operation == UploadOperation.RemoveManifestId || operation == UploadOperation.RemoveManifestVersion)
                    Metadata.Remove(metadataKey);
                if (operation == UploadOperation.AddManifestId || operation == UploadOperation.AddManifestVersion)
                {
                    if (!Metadata.ContainsKey(metadataKey)) Metadata.Add(metadataKey, new List<string>());
                    Metadata[metadataKey].Add(metadataValue);
                }
                return true;
            }

            public UploadCompletion WaitForCompletion()
            {
                Waits++;
                return OnWait == null ? Completion : OnWait();
            }
        }

        private static readonly UploadOperation[] Expected =
        {
            UploadOperation.Prepare, UploadOperation.Revalidate, UploadOperation.StartUpdate,
            UploadOperation.SetTitle, UploadOperation.SetDescription, UploadOperation.SetTags,
            UploadOperation.SetVisibility, UploadOperation.SetContent, UploadOperation.SetPreview,
            UploadOperation.RemoveManifestId, UploadOperation.AddManifestId,
            UploadOperation.RemoveManifestVersion, UploadOperation.AddManifestVersion,
            UploadOperation.Revalidate, UploadOperation.RecordAttempt, UploadOperation.Submit
        };

        public static int Main(string[] args) { return Run(); }

        public static int Run()
        {
            Case[] cases = Cases();
            int failed = 0;
            foreach (Case test in cases)
            {
                try { test.Body(); }
                catch (Exception error) { failed++; Console.Error.WriteLine(test.Name + ": " + error.Message); }
            }
            Console.WriteLine("UploadProtocol: " + (cases.Length - failed) + "/" + cases.Length + " passed");
            return failed == 0 ? 0 : 1;
        }

        private static Case[] Cases()
        {
            return new[]
            {
                new Case("exact operation order, arguments, and metadata replacement", ExactSequence),
                new Case("both supported visibility values forwarded unchanged", () =>
                {
                    foreach (int value in new[] { 0, 2 })
                    {
                        var port = new Fake();
                        Good(Change("visibility", value), port);
                        Equal(value, port.Calls.Single(row => row.Operation == UploadOperation.SetVisibility).Request.SteamVisibility);
                    }
                }),
                new Case("tag input snapshot cannot be mutated through request", TagSnapshot),
                new Case("missing request or port is pre-submit invalid input", () =>
                {
                    var port = new Fake();
                    Equal(UploadReason.InvalidRequest, new UploadProtocol().Run(null, port).Reason);
                    Equal(0, port.Calls.Count);
                    Equal(UploadStatus.Aborted, new UploadProtocol().Run(Request(), null).Status);
                }),
                new Case("zero item rejected, positive u64 boundary retained", () =>
                {
                    Invalid(Change("item", 0UL));
                    Good(Change("item", ulong.MaxValue), new Fake());
                }),
                new Case("all other visibility values refuse", () =>
                {
                    foreach (int value in new[] { -1, 1, 3, int.MaxValue }) Invalid(Change("visibility", value));
                }),
                new Case("required strings never default or trim", () =>
                {
                    foreach (string field in new[] { "id", "version", "title", "description", "content", "preview" })
                        foreach (string value in new[] { null, "", " leading", "trailing " }) Invalid(Change(field, value));
                    Invalid(Change("note", null));
                }),
                new Case("version grammar is canonical X.Y.Z", () =>
                {
                    foreach (string value in new[] { "0.03.0", "00.3.0", "0.3.00", "1.2", "v1.2.3", "1.2.3-beta", "1.2.3\n" })
                        Invalid(Change("version", value));
                    Good(Change("version", "0.0.0"), new Fake());
                }),
                new Case("both payload paths must be rooted", () =>
                {
                    Invalid(Change("content", "relative/content")); Invalid(Change("preview", "preview.png"));
                }),
                new Case("tag shape is bounded and unique", () =>
                {
                    foreach (string[] tags in new[] { null, new string[0], new string[65],
                        new[] { "Building", "building" }, new[] { "" }, new[] { "bad\nname" }, new string[] { null } })
                        Invalid(Change("tags", tags));
                }),
                new Case("multibyte UTF-8 limits accept boundary and refuse next byte", Utf8Boundaries),
                new Case("controls and malformed surrogates cannot reach setters", () =>
                {
                    foreach (string value in new[] { "bad\0title", "bad\ntitle", "\ud800", "\udc00", "\ud800x" })
                        Invalid(Change("title", value));
                    Invalid(Change("description", "bad\u0001description"));
                    Good(Change("description", "Paired \ud83c\udfe0 Unicode"), new Fake());
                }),
                new Case("empty change note and interior multiline text retained", () =>
                {
                    Good(Change("note", ""), new Fake());
                    var request = Change("description", "First\r\nSecond\tcolumn");
                    Good(request, new Fake());
                    Good(Change("note", "First\nSecond"), new Fake());
                }),
                new Case("prepare refusal precedes update", () => Cut(UploadOperation.Prepare, 1, false)),
                new Case("authority refusal precedes update", () => Cut(UploadOperation.Revalidate, 1, false)),
                new Case("start refusal precedes all setters", () => Cut(UploadOperation.StartUpdate, 1, false)),
                new Case("every setter false stops before submit", () =>
                {
                    foreach (UploadOperation operation in Expected.Skip(3).Take(10)) Cut(operation, 1, false);
                }),
                new Case("every pre-submit exception is terminal without submit", () =>
                {
                    foreach (UploadOperation operation in Expected.Take(Expected.Length - 1).Distinct()) Cut(operation, 1, true);
                    Cut(UploadOperation.Revalidate, 2, true);
                }),
                new Case("second authority refusal prevents durable attempt", () => Cut(UploadOperation.Revalidate, 2, false)),
                new Case("durable attempt refusal prevents submit", () => Cut(UploadOperation.RecordAttempt, 1, false)),
                new Case("durable attempt exception prevents submit", () => Cut(UploadOperation.RecordAttempt, 1, true)),
                new Case("submit false is uncertain and never retried", () => SubmitFault(false)),
                new Case("submit exception is uncertain and never retried", () => SubmitFault(true)),
                new Case("timeout IO and unknown callbacks stay uncertain", () =>
                {
                    Completion(UploadCompletion.TimedOut, UploadStatus.Uncertain, UploadReason.Timeout);
                    Completion(UploadCompletion.IoFailure, UploadStatus.Uncertain, UploadReason.IoFailure);
                    Completion(UploadCompletion.Unknown, UploadStatus.Uncertain, UploadReason.UnknownCompletion);
                    Completion((UploadCompletion)255, UploadStatus.Uncertain, UploadReason.UnknownCompletion);
                }),
                new Case("IO exception after submit stays uncertain", () =>
                {
                    var port = new Fake { OnWait = () => { throw new IOException("synthetic callback transport failure"); } };
                    var protocol = new UploadProtocol();
                    UploadResult result = protocol.Run(Request(), port);
                    Equal(UploadStatus.Uncertain, result.Status); Equal(UploadPhase.AwaitingCompletion, result.Phase);
                    Equal(UploadReason.Exception, result.Reason); Repeat(protocol, result, port);
                }),
                new Case("legal agreement requires user action", () =>
                    Completion(UploadCompletion.LegalAgreementRequired, UploadStatus.NeedsUser, UploadReason.LegalAgreement)),
                new Case("definitive callback rejection is not delivery", () =>
                    Completion(UploadCompletion.Rejected, UploadStatus.Rejected, UploadReason.ServerRejected)),
                new Case("memoized result ignores replacement inputs and ports", () =>
                {
                    var first = new Fake(); var protocol = new UploadProtocol();
                    UploadResult result = protocol.Run(Request(), first);
                    var stranger = new Fake();
                    Same(result, protocol.Run(Change("item", 999UL), stranger)); Equal(0, stranger.Calls.Count);
                    Equal(1, first.Submits);
                }),
                new Case("reentrant invocation cannot start a second update", () =>
                {
                    var protocol = new UploadProtocol(); var port = new Fake();
                    port.OnTry = (call, count) =>
                    {
                        Equal(UploadStatus.InProgress, protocol.Run(Request(), port).Status); return true;
                    };
                    Equal(UploadStatus.SubmittedUnverified, protocol.Run(Request(), port).Status);
                    Equal(1, port.Starts); Equal(1, port.Submits);
                }),
                new Case("false after a setter mutated local state still aborts", () =>
                {
                    bool changed = false;
                    var port = new Fake { OnTry = (call, count) =>
                    {
                        if (call.Operation != UploadOperation.SetTitle) return true;
                        changed = true; return false;
                    } };
                    Equal(UploadStatus.Aborted, new UploadProtocol().Run(Request(), port).Status);
                    True(changed, "fault reached the setter"); Equal(0, port.Submits); Equal(0, port.Waits);
                })
            };
        }

        private static void ExactSequence()
        {
            UploadRequest request = Request(); var port = new Fake();
            var protocol = new UploadProtocol(); UploadResult result = protocol.Run(request, port);
            Equal(UploadStatus.SubmittedUnverified, result.Status);
            True(Expected.SequenceEqual(port.Calls.Select(row => row.Operation)), "exact operation order");
            foreach (Call call in port.Calls) Same(request, call.Request);
            string[] keys = { "manifest_id", "manifest_id", "manifest_version", "manifest_version" };
            string[] values = { null, request.ManifestId, null, request.Version };
            for (int index = 0; index < 4; index++)
            {
                Equal(keys[index], port.Calls[9 + index].Key); Equal(values[index], port.Calls[9 + index].Value);
            }
            foreach (Call call in port.Calls.Take(9).Concat(port.Calls.Skip(13)))
            { Equal<string>(null, call.Key); Equal<string>(null, call.Value); }
            True(port.Metadata["manifest_id"].SequenceEqual(new[] { request.ManifestId }), "old duplicate IDs removed");
            True(port.Metadata["manifest_version"].SequenceEqual(new[] { request.Version }), "old duplicate versions removed");
            Equal("untouched", port.Metadata["foreign"].Single());
            Equal(1, port.Starts); Equal(1, port.Submits); Equal(1, port.Waits); True(port.Durable, "durable record");
            Repeat(protocol, result, port);
        }

        private static void TagSnapshot()
        {
            string[] tags = { "Building", "Script" };
            UploadRequest request = Change("tags", tags);
            tags[0] = "tampered"; Equal("Building", request.Tags[0]);
            bool refused = false;
            try { ((IList<string>)request.Tags)[0] = "tampered"; } catch (NotSupportedException) { refused = true; }
            True(refused, "request tags must be read-only"); Good(request, new Fake());
        }

        private static void Utf8Boundaries()
        {
            foreach (var bound in new[] { Tuple.Create("id", 255), Tuple.Create("title", 128),
                Tuple.Create("description", 7999), Tuple.Create("note", 8000) })
            {
                string exact = new string('é', bound.Item2 / 2) + (bound.Item2 % 2 == 1 ? "x" : "");
                Good(Change(bound.Item1, exact), new Fake()); Invalid(Change(bound.Item1, exact + "x"));
            }
            string tag = new string('é', 127) + "x";
            Good(Change("tags", new[] { tag }), new Fake()); Invalid(Change("tags", new[] { tag + "x" }));
            Invalid(Change("description", new string('x', 8000)));
        }

        private static void Cut(UploadOperation operation, int occurrence, bool throws)
        {
            var port = new Fake { OnTry = (call, count) =>
            {
                if (call.Operation != operation || count != occurrence) return true;
                if (throws) throw new IOException("synthetic pre-submit fault"); return false;
            } };
            var protocol = new UploadProtocol(); UploadResult result = protocol.Run(Request(), port);
            Equal(UploadStatus.Aborted, result.Status); Equal(operation, result.Operation);
            Equal(throws ? UploadReason.Exception : UploadReason.Refused, result.Reason);
            Equal(operation, port.Calls.Last().Operation); Equal(0, port.Submits); Equal(0, port.Waits);
            int stop = Array.FindIndex(Expected, next => next == operation);
            if (operation == UploadOperation.Revalidate && occurrence == 2) stop = 13;
            True(Expected.Take(stop + 1).SequenceEqual(port.Calls.Select(row => row.Operation)), "no calls after fault");
            Repeat(protocol, result, port);
        }

        private static void SubmitFault(bool throws)
        {
            var port = new Fake { OnTry = (call, count) =>
            {
                if (call.Operation != UploadOperation.Submit) return true;
                if (throws) throw new IOException("synthetic uncertain submit"); return false;
            } };
            var protocol = new UploadProtocol(); UploadResult result = protocol.Run(Request(), port);
            Equal(UploadStatus.Uncertain, result.Status); Equal(UploadPhase.Submitting, result.Phase);
            Equal(1, port.Submits); Equal(0, port.Waits); True(port.Durable, "attempt recorded before submit");
            Repeat(protocol, result, port);
        }

        private static void Completion(UploadCompletion completion, UploadStatus status, UploadReason reason)
        {
            var port = new Fake { Completion = completion }; var protocol = new UploadProtocol();
            UploadResult result = protocol.Run(Request(), port);
            Equal(status, result.Status); Equal(reason, result.Reason);
            Equal(UploadPhase.AwaitingCompletion, result.Phase); Equal(UploadOperation.AwaitCompletion, result.Operation);
            Equal(1, port.Submits); Equal(1, port.Waits); Repeat(protocol, result, port);
        }

        private static void Repeat(UploadProtocol protocol, UploadResult result, Fake port)
        {
            int calls = port.Calls.Count, submits = port.Submits, waits = port.Waits;
            Same(result, protocol.Run(Request(), port)); Same(result, protocol.Run(null, null));
            Equal(calls, port.Calls.Count); Equal(submits, port.Submits); Equal(waits, port.Waits);
        }

        private static UploadRequest Request()
        {
            string root = Path.DirectorySeparatorChar == '\\' ? @"C:\leased\content" : "/leased/content";
            return new UploadRequest(123456789UL, "r_ThousandAndFirst", "0.3.0", "The Thousand and First [ALPHA]",
                "A synthetic bounded upload description.", root, root + Path.DirectorySeparatorChar + "preview.png",
                new[] { "Building", "Script" }, 2, "Private playtest candidate.");
        }

        private static UploadRequest Change(string field, object value)
        {
            UploadRequest r = Request();
            return new UploadRequest(field == "item" ? (ulong)value : r.Item, field == "id" ? (string)value : r.ManifestId,
                field == "version" ? (string)value : r.Version, field == "title" ? (string)value : r.Title,
                field == "description" ? (string)value : r.Description, field == "content" ? (string)value : r.ContentPath,
                field == "preview" ? (string)value : r.PreviewPath, field == "tags" ? (string[])value : r.Tags.ToArray(),
                field == "visibility" ? (int)value : r.SteamVisibility, field == "note" ? (string)value : r.ChangeNote);
        }

        private static void Good(UploadRequest request, Fake port)
        { Equal(UploadStatus.SubmittedUnverified, new UploadProtocol().Run(request, port).Status); }
        private static void Invalid(UploadRequest request)
        {
            var port = new Fake(); var protocol = new UploadProtocol(); UploadResult result = protocol.Run(request, port);
            Equal(UploadStatus.Aborted, result.Status); Equal(UploadReason.InvalidRequest, result.Reason);
            Equal(UploadPhase.Validation, result.Phase); Equal(0, port.Calls.Count); Repeat(protocol, result, port);
        }
        private static void Equal<T>(T expected, T actual)
        { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("expected " + expected + ", got " + actual); }
        private static void True(bool condition, string reason)
        { if (!condition) throw new Exception(reason); }
        private static void Same(object expected, object actual)
        { True(ReferenceEquals(expected, actual), "reference identity changed"); }
    }
}
