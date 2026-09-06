using System;
using System.Collections.Generic;

namespace ThousandAndFirst.WorkshopSteam
{
    // Executable SDK-free orchestration tests; no claim about native resource disposal.
    public static class UploadCleanupTests
    {
        private static int cases, failed;

        public static int Main() { return Run(); }

        public static int Run()
        {
            cases = failed = 0;
            Case("zero-actions", delegate { UploadCleanup.Run(); });
            Case("all-success-in-order-once", delegate {
                List<int> calls = new List<int>();
                UploadCleanup.Run(() => calls.Add(0), () => calls.Add(1), () => calls.Add(2), () => calls.Add(3));
                Order(calls, "0,1,2,3");
            });
            for (int i = 0; i < 4; i++)
            {
                int position = i;
                Case("single-failure-position-" + i, delegate {
                    Exception fault = new InvalidOperationException("original failure");
                    Exception[] faults = new Exception[4]; faults[position] = fault;
                    VerifyFailures(faults, fault);
                });
            }
            Case("multiple-failures-original-order", delegate {
                Exception first = new InvalidOperationException("first");
                Exception second = new ArgumentException("second");
                VerifyFailures(new[] { first, null, second, null }, first, second);
            });
            Case("all-failures-original-order", delegate {
                Exception[] faults = { new InvalidOperationException("zero"), new ArgumentException("one"),
                    new NotSupportedException("two"), new ApplicationException("three") };
                VerifyFailures(faults, faults);
            });
            Case("nested-aggregate-not-flattened", delegate {
                Exception child = new InvalidOperationException("nested");
                Exception aggregate = new AggregateException("original aggregate", child);
                VerifyFailures(new[] { aggregate, null, child, null }, aggregate, child);
            });
            Case("hostile-message-never-read", delegate {
                HostileMessage fault = new HostileMessage();
                VerifyFailures(new Exception[] { fault, null, null, null }, fault);
                Need(fault.Reads == 0, "cleanup inspected an original exception message");
            });
            Case("null-array-refuses", delegate {
                Throws<ArgumentNullException>(() => UploadCleanup.Run((Action[])null));
            });
            for (int i = 0; i < 4; i++)
            {
                int position = i;
                Case("null-action-position-" + i + "-no-effects", delegate {
                    int effects = 0;
                    Action[] actions = { () => effects++, () => effects++, () => effects++, () => effects++ };
                    actions[position] = null;
                    Throws<ArgumentException>(() => UploadCleanup.Run(actions));
                    Need(effects == 0, "validation ran an earlier cleanup action");
                });
            }
            Case("caller-array-mutation-cannot-replace-captured-actions", delegate {
                List<int> calls = new List<int>();
                Action[] actions = new Action[3];
                actions[0] = delegate { calls.Add(0); actions[1] = null; actions[2] = () => calls.Add(99); };
                actions[1] = () => calls.Add(1); actions[2] = () => calls.Add(2);
                UploadCleanup.Run(actions); Order(calls, "0,1,2");
            });
            Case("duplicate-delegate-runs-once-per-supplied-position", delegate {
                int count = 0; Action action = () => count++;
                UploadCleanup.Run(action, action);
                Need(count == 2, "cleanup deduplicated or retried an action");
            });
            if (cases != 17) { failed++; Console.WriteLine("FAIL declared case count: " + cases); }
            Console.WriteLine("UploadCleanup: cases=" + cases + "; failed=" + failed
                + "; sdk_calls=false; native_cleanup_proof=false");
            return failed;
        }

        private static void VerifyFailures(Exception[] faults, params Exception[] expected)
        {
            List<int> calls = new List<int>();
            Action[] actions = new Action[faults.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                int position = i;
                actions[i] = delegate {
                    calls.Add(position);
                    if (faults[position] != null) throw faults[position];
                };
            }
            AggregateException aggregate = Capture(() => UploadCleanup.Run(actions));
            Order(calls, "0,1,2,3");
            Need(aggregate.InnerExceptions.Count == expected.Length, "original failure count changed");
            for (int i = 0; i < expected.Length; i++)
                Need(ReferenceEquals(aggregate.InnerExceptions[i], expected[i]), "original exception identity or order changed");
        }

        private sealed class HostileMessage : Exception
        {
            internal int Reads;
            public override string Message
            { get { Reads++; throw new InvalidOperationException("message getter must not be read"); } }
        }

        private static AggregateException Capture(Action action)
        {
            try { action(); }
            catch (AggregateException error) { return error; }
            throw new InvalidOperationException("Expected aggregate cleanup failure.");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            Exception actual = null;
            try { action(); } catch (Exception error) { actual = error; }
            Need(actual != null && actual.GetType() == typeof(T), "wrong argument refusal type");
        }

        private static void Order(List<int> calls, string expected)
        { Need(string.Join(",", calls) == expected, "cleanup order, completion or execution count changed"); }

        private static void Need(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException(reason); }

        private static void Case(string name, Action body)
        {
            cases++;
            try { body(); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + " " + error.GetType().Name); }
        }
    }
}
