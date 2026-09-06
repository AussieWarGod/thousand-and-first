using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst.WorkshopSteam
{
    // Executable SDK-free ordering tests. Recorded markers are fake-port evidence, not disk proof.
    public static class UploadAftermathTests
    {
        private static readonly string SHA = new string('a', 64);
        private static readonly UploadStatus[] Terminal = {
            UploadStatus.Aborted, UploadStatus.Uncertain, UploadStatus.NeedsUser,
            UploadStatus.Rejected, UploadStatus.SubmittedUnverified
        };
        private static int cases, failed;

        public static int Main() { return Run(); }

        public static int Run()
        {
            cases = failed = 0;
            Case("successful-order-and-returned-facts", delegate {
                Port port = new Port(); UploadAftermathResult result = Invoke(port);
                Need(result.ObservationSHA == SHA && result.ContentUnchanged && result.MetadataMatches, "returned facts");
                Order(port, "persist,content,metadata"); Need(port.Recorded == port.Token, "prior evidence");
            });
            foreach (UploadStatus status in Terminal)
            foreach (bool hasObservation in new[] { false, true })
            {
                UploadStatus captured = status; bool present = hasObservation;
                Case("terminal-" + status + "-observation-" + hasObservation, delegate {
                    Port port = new Port { Observation = present ? SHA : null, RecordOnPersist = present };
                    if (captured == UploadStatus.SubmittedUnverified && !present)
                    {
                        Throws<InvalidDataException>(() => Invoke(port, captured));
                        Order(port, "persist"); Need(port.Recorded == null, "no invented evidence");
                        return;
                    }
                    UploadAftermathResult result = Invoke(port, captured);
                    bool submitted = captured == UploadStatus.SubmittedUnverified;
                    Need(result.ObservationSHA == port.Observation && result.ContentUnchanged
                        && result.MetadataMatches == submitted, "terminal facts");
                    Order(port, submitted ? "persist,content,metadata" : "persist,content");
                    Need(present ? port.Recorded == port.Token : port.Recorded == null, "exact marker");
                });
            }
            foreach (UploadStatus status in Terminal)
            {
                UploadStatus captured = status;
                Case("changed-content-skips-metadata-" + status, delegate {
                    Port port = new Port { Content = false };
                    UploadAftermathResult result = Invoke(port, captured);
                    Need(result.ObservationSHA == SHA && !result.ContentUnchanged && !result.MetadataMatches, "changed content facts");
                    Order(port, "persist,content"); Need(port.Recorded == port.Token, "evidence survives changed content");
                });
            }
            Case("null-result-no-effects", delegate {
                Port port = new Port(); Throws<ArgumentNullException>(() => UploadAftermath.Run(null, port.Revalidate, port)); Order(port, "");
            });
            Case("null-content-callback-no-effects", delegate {
                Port port = new Port(); Throws<ArgumentNullException>(() => UploadAftermath.Run(Result(), null, port)); Order(port, "");
            });
            Case("null-port-no-effects", delegate {
                Port port = new Port(); Throws<ArgumentNullException>(() => UploadAftermath.Run(Result(), port.Revalidate, null)); Order(port, "");
            });
            foreach (UploadStatus status in new[] { UploadStatus.InProgress, (UploadStatus)99 })
            {
                UploadStatus captured = status;
                Case("nonterminal-or-unknown-status-" + status, delegate {
                    Port port = new Port(); Throws<ArgumentException>(() => Invoke(port, captured)); Order(port, "");
                });
            }
            string[] invalid = { "", new string('a', 63), new string('a', 65), new string('A', 64),
                new string('g', 64), new string('a', 63) + "\n", " " + new string('a', 63) };
            for (int i = 0; i < invalid.Length; i++)
            foreach (UploadStatus status in new[] { UploadStatus.SubmittedUnverified, UploadStatus.Aborted })
            {
                string value = invalid[i]; UploadStatus captured = status;
                Case("invalid-observation-" + i + "-" + status, delegate {
                    Port port = new Port { Observation = value };
                    Throws<InvalidDataException>(() => Invoke(port, captured));
                    Order(port, "persist"); Need(port.Recorded == port.Token, "invalid acknowledgement does not erase record");
                });
            }
            foreach (bool afterRecord in new[] { false, true })
            {
                bool recorded = afterRecord;
                Case("persistence-throws-after-record-" + afterRecord, delegate {
                    Exception fault = new IOException("persist cut");
                    Port port = new Port { PersistenceFault = fault, RecordOnPersist = recorded };
                    SameException(fault, () => Invoke(port)); Order(port, "persist");
                    Need(recorded ? port.Recorded == port.Token : port.Recorded == null, "persistence cut marker");
                });
            }
            Case("content-throws-after-record", delegate {
                Exception fault = new InvalidOperationException("content cut"); Port port = new Port { ContentFault = fault };
                SameException(fault, () => Invoke(port)); Order(port, "persist,content"); Need(port.Recorded == port.Token, "content cut marker");
            });
            Case("metadata-throws-after-record", delegate {
                Exception fault = new InvalidOperationException("metadata cut"); Port port = new Port { MetadataFault = fault };
                SameException(fault, () => Invoke(port)); Order(port, "persist,content,metadata"); Need(port.Recorded == port.Token, "metadata cut marker");
            });
            Case("metadata-mismatch-is-not-delivery", delegate {
                Port port = new Port { Metadata = false }; UploadAftermathResult result = Invoke(port);
                Need(result.ObservationSHA == SHA && result.ContentUnchanged && !result.MetadataMatches, "metadata mismatch facts");
                Order(port, "persist,content,metadata"); Need(port.Recorded == port.Token, "metadata mismatch marker");
            });
            Case("later-disposal-throws-with-evidence-retained", delegate {
                Exception fault = new IOException("dispose cut"); Port port = new Port { DisposalFault = fault };
                UploadAftermathResult result = null;
                SameException(fault, delegate { try { result = Invoke(port); } finally { port.Dispose(); } });
                Need(result != null && result.ObservationSHA == SHA && port.Recorded == port.Token, "later disposal cannot erase evidence");
                Order(port, "persist,content,metadata,dispose");
            });
            if (cases != 41) { failed++; Console.WriteLine("FAIL declared case count: " + cases); }
            Console.WriteLine("UploadAftermath: cases=" + cases + "; failed=" + failed + "; sdk_calls=false; file_durability_proof=false");
            return failed == 0 ? 0 : 1;
        }

        private static UploadResult Result(UploadStatus status = UploadStatus.SubmittedUnverified)
        { return new UploadResult(status, UploadPhase.AwaitingCompletion, UploadOperation.AwaitCompletion, UploadReason.None); }
        private static UploadAftermathResult Invoke(Port port, UploadStatus status = UploadStatus.SubmittedUnverified)
        { return UploadAftermath.Run(Result(status), port.Revalidate, port); }

        private sealed class Port : IWorkshopSubmissionEvidencePort, IDisposable
        {
            internal readonly List<string> Calls = new List<string>();
            internal readonly object Token = new object();
            internal object Recorded;
            internal string Observation = SHA;
            internal bool Content = true, Metadata = true, RecordOnPersist = true;
            internal Exception PersistenceFault, ContentFault, MetadataFault, DisposalFault;
            public string PersistSubmissionObservation()
            {
                Calls.Add("persist");
                if (RecordOnPersist) Recorded = Token;
                if (PersistenceFault != null) throw PersistenceFault;
                return Observation;
            }
            internal bool Revalidate()
            {
                Calls.Add("content"); Need(Recorded == Token || Observation == null, "content preceded persistence");
                if (ContentFault != null) throw ContentFault;
                return Content;
            }
            public bool VerifyPublishedMetadata()
            {
                Calls.Add("metadata"); Need(Recorded == Token, "metadata preceded persistence");
                if (MetadataFault != null) throw MetadataFault;
                return Metadata;
            }
            public void Dispose()
            { Calls.Add("dispose"); if (DisposalFault != null) throw DisposalFault; }
        }
        private static void Order(Port port, string expected)
        { Need(string.Join(",", port.Calls) == expected, "call order or duplicate execution"); }
        private static void SameException(Exception expected, Action action)
        {
            Exception actual = null;
            try { action(); } catch (Exception error) { actual = error; }
            Need(ReferenceEquals(expected, actual), "exception was swallowed or replaced");
        }
        private static void Throws<T>(Action action) where T : Exception
        {
            Exception actual = null;
            try { action(); } catch (Exception error) { actual = error; }
            Need(actual != null && actual.GetType() == typeof(T), "expected " + typeof(T).Name);
        }
        private static void Need(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException(reason); }
        private static void Case(string name, Action action)
        {
            cases++;
            try { action(); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + " " + error.GetType().Name + ": " + error.Message); }
        }
    }
}
