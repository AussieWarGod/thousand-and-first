using System;
using System.Collections.Generic;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class UploadCleanup
    {
        public static void Run(params Action[] steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            Action[] captured = (Action[])steps.Clone();
            for (int i = 0; i < captured.Length; i++)
                if (captured[i] == null)
                    throw new ArgumentException("Every cleanup action must be nonnull.", nameof(steps));

            List<Exception> failures = new List<Exception>();
            foreach (Action step in captured)
            {
                try { step(); }
                catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count != 0)
                throw new AggregateException("One or more cleanup actions failed.", failures);
        }
    }
}
