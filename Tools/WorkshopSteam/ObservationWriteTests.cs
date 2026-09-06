using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThousandAndFirst.WorkshopSteam.Evidence;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class ObservationWriteTests
    {
        private const string Item = "12345", Stamp = "2026-09-06T14:20:31.1234567Z";
        private static readonly string PlanSha = new string('1', 64), PackageSha = new string('2', 64);
        private static readonly HashSet<string> Groups = new HashSet<string>(StringComparer.Ordinal);
        private static int cases, failures;
        public static int Main(string[] args) { return Run(); }
        public static int Run()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            { Console.WriteLine("UNSUPPORTED: observation writes require Windows file leases"); return 2; }
            cases = failures = 0; Groups.Clear();
            Case("exact", "canonical-bytes-and-link", Exact);
            foreach (string kind in new[] { "hash", "item", "previous", "null" })
                Case("invalid", kind, delegate { Invalid(kind); });
            Case("repeat", "same-and-conflicting-observation", Repeat);
            foreach (bool same in new[] { true, false })
                Case("existing", same ? "same-bytes" : "different-bytes", delegate { Existing(same); });
            Case("disposed", "parent-refused", Disposed);
            Case("held", "both-files-deny-writer-and-rename", Held);
            Case("release", "independent-child-and-retained-files", Release);
            Case("later-fault", "exception-unwinds-with-both-records", LaterException);
            foreach (bool conflict in new[] { false, true })
                Case("concurrent", conflict ? "conflicting-writers" : "same-writers", delegate { Concurrent(conflict); });
            if (cases != 14 || Groups.Count != 9) { Console.WriteLine("FAIL fixture census"); return 2; }
            Console.WriteLine("ObservationWriteTests: cases=" + cases + " groups=" + Groups.Count + " failures=" + failures + "; fixture_trees_retained=true; sdk_calls=false");
            return failures == 0 ? 0 : 1;
        }
        private static void Exact()
        {
            using (var f = new Fixture())
            {
                ReleaseSubmissionObservation observation = Observation(f);
                byte[] expected = ReleaseSubmissionObservationCodec.EncodeUtf8(observation);
                using (UploadAttemptLease child = f.Parent.CreateObservation(observation))
                {
                    Check(f.Parent.Revalidate() && child.Revalidate()); Equal(Read(f.Attempt), f.Bytes); Equal(Read(f.Output), expected);
                    Check(Directory.GetFiles(f.State).Length == 2);
                    ReleaseSubmissionObservation decoded; string refusal;
                    Check(ReleaseSubmissionObservationCodec.TryDecode(Read(f.Output), out decoded, out refusal) && refusal == null);
                    Check(decoded.AttemptSHA == Hash(f.Bytes) && decoded.PlanSHA == PlanSha && decoded.ReceiptSHA == PackageSha
                        && decoded.Item == Item && decoded.PreviousObservationSHA == null && decoded.Note == "first"
                        && decoded.RawEResult == 1 && decoded.ReturnedItem == Item && decoded.IoFailure == false
                        && decoded.LegalAgreementRequired == false && decoded.Completion == ReleaseSubmissionCompletion.Ok);
                    Check(Hash(Read(f.Output)) == Hash(expected) && Hash(f.Bytes) != PackageSha && PackageSha != PlanSha);
                }
            }
        }
        private static void Invalid(string kind)
        {
            using (var f = new Fixture())
            {
                ReleaseSubmissionObservation observation = kind == "null" ? null : Observation(f, "first",
                    kind == "hash" ? new string('f', 64) : null, kind == "item" ? "54321" : Item,
                    kind == "previous" ? new string('3', 64) : null);
                Refuses(delegate { return f.Parent.CreateObservation(observation); });
                Check(!File.Exists(f.Output) && Directory.GetFiles(f.State).Length == 1 && f.Parent.Revalidate()); Equal(Read(f.Attempt), f.Bytes);
            }
        }
        private static void Repeat()
        {
            using (var f = new Fixture())
            {
                ReleaseSubmissionObservation first = Observation(f);
                byte[] expected = ReleaseSubmissionObservationCodec.EncodeUtf8(first);
                using (UploadAttemptLease child = f.Parent.CreateObservation(first))
                {
                    Refuses(delegate { return f.Parent.CreateObservation(first); });
                    Refuses(delegate { return f.Parent.CreateObservation(Observation(f, "conflicting")); });
                    Check(child.Revalidate() && f.Parent.Revalidate()); Equal(Read(f.Output), expected); Equal(Read(f.Attempt), f.Bytes);
                }
            }
        }
        private static void Existing(bool same)
        {
            using (var f = new Fixture())
            {
                ReleaseSubmissionObservation observation = Observation(f);
                byte[] existing = same ? ReleaseSubmissionObservationCodec.EncodeUtf8(observation) : Encoding.UTF8.GetBytes("existing-output-do-not-replace");
                WriteNew(f.Output, existing);
                Refuses(delegate { return f.Parent.CreateObservation(observation); });
                Refuses(delegate { return f.Parent.CreateObservation(observation); });
                Check(f.Parent.Revalidate() && Directory.GetFiles(f.State).Length == 2);
                Equal(Read(f.Output), existing); Equal(Read(f.Attempt), f.Bytes);
            }
        }
        private static void Disposed()
        {
            using (var f = new Fixture())
            {
                ReleaseSubmissionObservation observation = Observation(f); f.Parent.Dispose();
                Refuses(delegate { return f.Parent.CreateObservation(observation); });
                Check(!f.Parent.Revalidate() && !File.Exists(f.Output)); Equal(File.ReadAllBytes(f.Attempt), f.Bytes);
            }
        }
        private static void Held()
        {
            using (var f = new Fixture())
            {
                ReleaseSubmissionObservation observation = Observation(f);
                using (UploadAttemptLease child = f.Parent.CreateObservation(observation))
                {
                    foreach (string path in new[] { f.Attempt, f.Output })
                    {
                        Blocked(delegate { using (File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } });
                        Blocked(delegate { File.Move(path, path + ".moved"); }); Check(!File.Exists(path + ".moved"));
                    }
                    Check(child.Revalidate() && f.Parent.Revalidate()); Equal(Read(f.Attempt), f.Bytes);
                    Equal(Read(f.Output), ReleaseSubmissionObservationCodec.EncodeUtf8(observation));
                }
            }
        }
        private static void Release()
        {
            using (var f = new Fixture())
            {
                ReleaseSubmissionObservation observation = Observation(f);
                UploadAttemptLease child = f.Parent.CreateObservation(observation);
                try
                {
                    f.Parent.Dispose(); Check(!f.Parent.Revalidate() && child.Revalidate()); Writable(f.Attempt);
                    Blocked(delegate { using (File.Open(f.Output, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } });
                }
                finally { child.Dispose(); }
                child.Dispose(); f.Parent.Dispose(); Check(!child.Revalidate());
                Writable(f.Attempt); Writable(f.Output); Equal(File.ReadAllBytes(f.Attempt), f.Bytes);
                Equal(File.ReadAllBytes(f.Output), ReleaseSubmissionObservationCodec.EncodeUtf8(observation));
                Check(Directory.GetFiles(f.State).Length == 2);
            }
        }
        private sealed class LaterFault : Exception { }
        private static void LaterException()
        {
            var f = new Fixture(); ReleaseSubmissionObservation observation = Observation(f); bool caught = false;
            try
            {
                using (f) using (UploadAttemptLease child = f.Parent.CreateObservation(observation))
                { Check(child.Revalidate() && f.Parent.Revalidate()); throw new LaterFault(); }
            }
            catch (LaterFault) { caught = true; }
            Check(caught && !f.Parent.Revalidate() && Directory.GetFiles(f.State).Length == 2);
            Writable(f.Attempt); Writable(f.Output); Equal(File.ReadAllBytes(f.Attempt), f.Bytes);
            Equal(File.ReadAllBytes(f.Output), ReleaseSubmissionObservationCodec.EncodeUtf8(observation));
        }
        private static void Concurrent(bool conflict)
        {
            using (var f = new Fixture()) using (var ready = new CountdownEvent(2)) using (var start = new ManualResetEventSlim(false))
            {
                var observations = new[] { Observation(f), Observation(f, conflict ? "other-writer" : "first") };
                var children = new UploadAttemptLease[2]; var refused = new bool[2]; var errors = new Exception[2]; var workers = new Task[2];
                for (int i = 0; i < workers.Length; i++)
                {
                    int index = i;
                    workers[i] = Task.Run(delegate {
                        ready.Signal(); start.Wait();
                        try { children[index] = f.Parent.CreateObservation(observations[index]); }
                        catch (Exception error) when (error is IOException || error is InvalidDataException) { refused[index] = true; }
                        catch (Exception error) { errors[index] = error; }
                    });
                }
                try
                {
                    bool coordinated = ready.Wait(10000); start.Set(); Check(coordinated && Task.WaitAll(workers, 20000));
                    Check(errors[0] == null && errors[1] == null);
                    int winner = children[0] != null ? 0 : 1, loser = 1 - winner;
                    Check(children[winner] != null && children[loser] == null && !refused[winner] && refused[loser]);
                    Check(f.Parent.Revalidate() && children[winner].Revalidate() && Directory.GetFiles(f.State).Length == 2);
                    Equal(Read(f.Attempt), f.Bytes); Equal(Read(f.Output), ReleaseSubmissionObservationCodec.EncodeUtf8(observations[winner]));
                    Refuses(delegate { return f.Parent.CreateObservation(observations[loser]); });
                    Equal(Read(f.Attempt), f.Bytes);
                }
                finally
                {
                    start.Set(); bool ended = false, closed = true;
                    try { ended = Task.WaitAll(workers, 20000); } catch { closed = false; }
                    foreach (UploadAttemptLease child in children)
                        if (child != null) try { child.Dispose(); } catch { closed = false; }
                    Check(ended && closed);
                }
            }
        }
        private static ReleaseSubmissionObservation Observation(Fixture f, string note = "first", string attemptSha = null,
            string item = Item, string previous = null)
        {
            ReleaseSubmissionObservation observation; string refusal;
            Check(ReleaseSubmissionObservation.TryCreate(attemptSha ?? Hash(f.Bytes), previous, item, "0.3.7", PlanSha, PackageSha,
                Stamp, true, 1, item, false, false, ReleaseSubmissionCompletion.Ok, note, out observation, out refusal));
            Check(refusal == null); return observation;
        }
        private static byte[] Read(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            { byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes; }
        }
        private static void WriteNew(string path, byte[] bytes)
        { using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); } }
        private static void Writable(string path) { using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } }
        private static string Hash(byte[] bytes) { return ReleaseSubmissionObservationCodec.Sha256Hex(bytes); }
        private static void Equal(byte[] a, byte[] b) { Check(a.Length == b.Length); for (int i = 0; i < a.Length; i++) Check(a[i] == b[i]); }
        private static void Check(bool value) { if (!value) throw new InvalidOperationException("observation write fixture assertion failed"); }
        private static void Blocked(Action action)
        { bool blocked = false; try { action(); } catch (IOException) { blocked = true; } Check(blocked); }
        private static void Refuses(Func<UploadAttemptLease> action)
        {
            UploadAttemptLease unexpected = null; bool refused = false;
            try { unexpected = action(); }
            catch (Exception error) when (error is IOException || error is InvalidDataException) { refused = true; }
            finally { if (unexpected != null) unexpected.Dispose(); }
            Check(refused);
        }
        private static void Case(string group, string name, Action body)
        {
            cases++; Groups.Add(group);
            try { body(); Console.WriteLine("PASS " + group + "/" + name); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL " + group + "/" + name + " " + error.GetType().Name); }
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly string Root, State, Attempt, Output;
            internal readonly byte[] Bytes = Encoding.UTF8.GetBytes("{\"attemptId\":\"synthetic\",\"item\":\"12345\"}\n");
            internal readonly UploadAttemptLease Parent;
            internal Fixture()
            {
                Root = Path.Combine(Path.GetTempPath(), "taf-observation-write-test." + Guid.NewGuid().ToString("N"));
                Check(!Directory.Exists(Root) && !File.Exists(Root)); State = Path.Combine(Root, "state"); Directory.CreateDirectory(State);
                Attempt = Path.Combine(State, Item + ".active.attempt.json"); Output = Attempt + ".submission.json";
                Parent = UploadAttemptLease.Create(Attempt, Bytes); Console.WriteLine("fixture=" + Root);
            }
            public void Dispose() { Parent.Dispose(); }
        }
    }
}
