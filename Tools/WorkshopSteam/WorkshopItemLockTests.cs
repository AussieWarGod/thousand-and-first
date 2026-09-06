// Root-only runner: compile these two files as net9/C#7.3, StartupObject
// ThousandAndFirst.WorkshopSteam.WorkshopItemLockTests, then dotnet <built-test.dll> --run.
// Real Windows mutexes, no SDK or release-state writes. Run with both publishing lanes idle.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class WorkshopItemLockTests
    {
        private const ulong Item = WorkshopItemLock.StagingItem;
        private const string Name = @"Global\taf-workshop-item-3796495680";
        private static int passed, failed;

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0] == "--child") return ChildMain(args[1]);
                if (args.Length != 1 || args[0] != "--run") throw new ArgumentException("Expected --run.");
                return Run();
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        public static int Run()
        {
            passed = failed = 0;
            Case("invalid item before platform/native access", () => {
                foreach (ulong item in new[] { 0UL, 1UL, 333640UL, ulong.MaxValue })
                    Expect<ArgumentOutOfRangeException>(() => WorkshopItemLock.Acquire(item));
            });
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            { Console.Error.WriteLine("Windows required: native cases NOT RUN."); return 2; }
            Case("fixed names and independent items", () => {
                using (WorkshopItemLock staging = WorkshopItemLock.Acquire(Item))
                using (WorkshopItemLock alpha = WorkshopItemLock.Acquire(WorkshopItemLock.PublicItem))
                using (Mutex a = Mutex.OpenExisting(Name))
                using (Mutex b = Mutex.OpenExisting(@"Global\taf-workshop-item-3794797472"))
                {
                    Check(staging.IsHeld && alpha.IsHeld, "both exact leases held");
                    staging.RequireHeld(Item); alpha.RequireHeld(WorkshopItemLock.PublicItem);
                    Check(staging.Item == Item && alpha.Item == WorkshopItemLock.PublicItem, "item facts");
                    Expect<InvalidOperationException>(() => staging.RequireHeld(WorkshopItemLock.PublicItem));
                    Expect<ArgumentOutOfRangeException>(() => staging.RequireHeld(0));
                }
            });
            Case("same-thread reentry refuses", () => {
                using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item))
                { Expect<InvalidOperationException>(() => WorkshopItemLock.Acquire(Item)); lease.RequireHeld(Item); }
            });
            Case("concurrent first acquisitions have one owner", ConcurrentFirst);
            Case("wrong-thread observation use and disposal", () => {
                using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item))
                {
                    OnThread(() => {
                        Check(!lease.IsHeld, "wrong thread must not appear held");
                        Expect<InvalidOperationException>(() => lease.RequireHeld(Item));
                        Expect<InvalidOperationException>(() => lease.Dispose());
                        Expect<InvalidOperationException>(() => WorkshopItemLock.Acquire(Item));
                    });
                    lease.RequireHeld(Item);
                }
            });
            Case("release reacquire and idempotent disposal", () => {
                WorkshopItemLock old = WorkshopItemLock.Acquire(Item); old.Dispose(); old.Dispose();
                Check(!old.IsHeld, "disposed lease"); Expect<ObjectDisposedException>(() => old.RequireHeld(Item));
                using (WorkshopItemLock next = WorkshopItemLock.Acquire(Item))
                { next.RequireHeld(Item); Check(!next.Abandoned, "ordinary release is not abandonment"); }
            });
            Case("native create failure releases local reservation", () => {
                using (EventWaitHandle collision = new EventWaitHandle(false, EventResetMode.ManualReset, Name))
                    Expect<Win32Exception>(() => WorkshopItemLock.Acquire(Item));
                using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item)) lease.RequireHeld(Item);
            });
            Case("cross-process timeout then reacquire", () => {
                using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item))
                using (Child child = new Child("timeout-recover"))
                {
                    Check(child.Line() == "timeout", "contending child must time out");
                    lease.Dispose(); child.Send("resume");
                    Check(child.Line() == "recovered", "timeout cleanup permits later acquisition"); child.Exit(0);
                }
            });
            Case("observed abandonment is retained on held lease", () => {
                using (Child child = new Child("abandon"))
                {
                    Check(child.Line() == "held", "child owns item");
                    using (Mutex keeper = Mutex.OpenExisting(Name))
                    {
                        child.Send("abandon"); child.Exit(23);
                        using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item))
                        { Check(lease.Abandoned && lease.IsHeld, "WAIT_ABANDONED observation"); lease.RequireHeld(Item); }
                        using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item))
                            Check(!lease.Abandoned, "signal is not manufactured after ordinary release");
                    }
                }
            });
            Case("cleanup fault is surfaced and poisons local item", () => {
                using (Child child = new Child("cleanup-fault"))
                { Check(child.Line() == "cleanup-refused", "real ReleaseMutex failure surfaced"); child.Exit(0); }
            });
            Case("terminated local owner cannot mint independent lease", () => {
                using (Child child = new Child("thread-abandon"))
                { Check(child.Line() == "local-refused", "dead-thread reservation retained"); child.Exit(0); }
            });
            Console.WriteLine("WorkshopItemLock: " + passed + " PASS, " + failed + " FAIL (no SDK/release-state access).");
            return failed == 0 ? 0 : 1;
        }

        private static void ConcurrentFirst()
        {
            using (ManualResetEventSlim start = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (CountdownEvent attempted = new CountdownEvent(2))
            {
                int owners = 0, refusals = 0; Exception error = null;
                ThreadStart work = () => {
                    bool announced = false;
                    try
                    {
                        Check(start.Wait(5000), "concurrent start deadline");
                        using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item))
                        {
                            Interlocked.Increment(ref owners); attempted.Signal(); announced = true;
                            Check(release.Wait(10000), "concurrent release deadline"); lease.RequireHeld(Item);
                        }
                    }
                    catch (InvalidOperationException) { Interlocked.Increment(ref refusals); }
                    catch (Exception failure) { Interlocked.CompareExchange(ref error, failure, null); }
                    finally { if (!announced) attempted.Signal(); }
                };
                Thread one = new Thread(work) { IsBackground = true }, two = new Thread(work) { IsBackground = true };
                one.Start(); two.Start(); start.Set();
                try { Check(attempted.Wait(10000), "two bounded acquisition outcomes"); }
                finally { release.Set(); Join(one); Join(two); }
                if (error != null) throw error;
                Check(owners == 1 && refusals == 1, "exactly one local authority");
            }
        }

        private static int ChildMain(string mode)
        {
            if (mode == "cleanup-fault")
            {
                WorkshopItemLock lease = WorkshopItemLock.Acquire(Item);
                IntPtr handle = (IntPtr)typeof(WorkshopItemLock).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lease);
                Check(ReleaseMutex(handle), "fixture releases its own mutex before cleanup");
                InvalidOperationException failure = Expect<InvalidOperationException>(() => lease.Dispose());
                Check(failure.InnerException is Win32Exception && !lease.IsHeld, "cleanup fault remains observable");
                Expect<InvalidOperationException>(() => lease.RequireHeld(Item));
                Expect<InvalidOperationException>(() => lease.Dispose());
                Expect<InvalidOperationException>(() => WorkshopItemLock.Acquire(Item));
                Console.WriteLine("cleanup-refused"); return 0;
            }
            if (mode == "thread-abandon")
            {
                OnThread(() => { WorkshopItemLock lease = WorkshopItemLock.Acquire(Item); lease.RequireHeld(Item); });
                Expect<InvalidOperationException>(() => WorkshopItemLock.Acquire(Item));
                Console.WriteLine("local-refused"); return 0;
            }
            if (mode == "timeout-recover")
            {
                Stopwatch clock = Stopwatch.StartNew();
                Expect<TimeoutException>(() => WorkshopItemLock.Acquire(Item));
                Check(clock.ElapsedMilliseconds >= 4500, "actual five-second wait, not immediate refusal");
                Console.WriteLine("timeout"); Console.Out.Flush(); Check(Input() == "resume", "resume handshake");
                using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item)) lease.RequireHeld(Item);
                Console.WriteLine("recovered"); return 0;
            }
            if (mode != "abandon") throw new ArgumentException("Unknown child mode.");
            using (WorkshopItemLock lease = WorkshopItemLock.Acquire(Item))
            {
                Console.WriteLine("held"); Console.Out.Flush(); Check(Input() == "abandon", "abandon handshake");
                Environment.Exit(23);
            }
            throw new Exception("Environment.Exit unexpectedly returned.");
        }

        private static string Input()
        { Task<string> line = Console.In.ReadLineAsync(); Check(line.Wait(15000), "child input deadline"); return line.Result; }

        private sealed class Child : IDisposable
        {
            private readonly Process process;
            private readonly Task<string> errors;
            internal Child(string mode)
            {
                string host = Environment.ProcessPath;
                Check(!string.IsNullOrEmpty(host) && Path.IsPathFullyQualified(host), "absolute test host required");
                ProcessStartInfo start = new ProcessStartInfo(host) { UseShellExecute = false,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                if (string.Equals(Path.GetFileNameWithoutExtension(host), "dotnet", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(typeof(WorkshopItemLockTests).Assembly.Location);
                start.ArgumentList.Add("--child"); start.ArgumentList.Add(mode);
                process = Process.Start(start);
                if (process == null) throw new Exception("Child process did not start.");
                try { errors = process.StandardError.ReadToEndAsync(); }
                catch (Exception error)
                {
                    try
                    {
                        if (!process.HasExited) process.Kill(true);
                        Check(process.WaitForExit(5000), "child initialization cleanup deadline");
                    }
                    catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                    finally { process.Dispose(); }
                    throw;
                }
            }
            internal string Line()
            { Task<string> line = process.StandardOutput.ReadLineAsync(); Check(line.Wait(12000), "child output deadline"); return line.Result; }
            internal void Send(string text) { process.StandardInput.WriteLine(text); process.StandardInput.Flush(); }
            internal void Exit(int expected)
            {
                Check(process.WaitForExit(12000), "child exit deadline"); Check(errors.Wait(3000), "stderr drain deadline");
                Check(process.ExitCode == expected && errors.Result.Length == 0, "child exit/stderr: " + errors.Result);
            }
            public void Dispose()
            {
                try
                {
                    if (!process.HasExited) process.Kill(true);
                    Check(process.WaitForExit(5000), "owned child cleanup deadline");
                    Check(errors.Wait(3000), "owned child stderr cleanup deadline");
                }
                finally { process.Dispose(); }
            }
        }

        private static void OnThread(Action action)
        {
            Exception failure = null;
            Thread thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } }) { IsBackground = true };
            thread.Start(); Join(thread); if (failure != null) throw failure;
        }
        private static void Join(Thread thread) { Check(thread.Join(12000), "owned test thread did not finish"); }
        private static void Case(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static T Expect<T>(Action action) where T : Exception
        { try { action(); } catch (T error) { return error; } throw new Exception("Expected " + typeof(T).Name); }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseMutex(IntPtr handle);
    }
}
