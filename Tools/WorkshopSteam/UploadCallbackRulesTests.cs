using System;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class UploadCallbackRulesTests
    {
        // Columns: non-OK/wrong item, non-OK/exact item, OK/wrong item, OK/exact item.
        // Rows: no flags, IO only, legal only, legal and IO together.
        private static readonly UploadCompletion[,] Outcomes =
        {
            { UploadCompletion.Rejected, UploadCompletion.Rejected, UploadCompletion.Unknown, UploadCompletion.Ok },
            { UploadCompletion.IoFailure, UploadCompletion.IoFailure, UploadCompletion.IoFailure, UploadCompletion.IoFailure },
            { UploadCompletion.LegalAgreementRequired, UploadCompletion.LegalAgreementRequired,
                UploadCompletion.LegalAgreementRequired, UploadCompletion.LegalAgreementRequired },
            { UploadCompletion.LegalAgreementRequired, UploadCompletion.LegalAgreementRequired,
                UploadCompletion.LegalAgreementRequired, UploadCompletion.LegalAgreementRequired }
        };

        public static int Main() { return Run(); }

        public static int Run()
        {
            int cases = 0, failed = 0;
            ulong[] expectedItems = { 0UL, 1UL, 3796495680UL, ulong.MaxValue };
            int[] rawResults = { int.MinValue, -1, 0, 1, 2, 9, 16, int.MaxValue };
            foreach (ulong expectedItem in expectedItems)
            foreach (int raw in rawResults)
            foreach (bool legal in new[] { false, true })
            foreach (bool io in new[] { false, true })
            foreach (string itemShape in new[] { "zero", "exact", "wrong" })
            {
                ulong returnedItem = itemShape == "zero" ? 0UL : itemShape == "exact" ? expectedItem
                    : expectedItem == ulong.MaxValue ? ulong.MaxValue - 1UL : expectedItem + 1UL;
                int row = (legal ? 2 : 0) + (io ? 1 : 0);
                int column = (raw == 1 ? 2 : 0) + (returnedItem == expectedItem ? 1 : 0);
                UploadCompletion expected = expectedItem == 0 ? UploadCompletion.Unknown : Outcomes[row, column];
                cases++;
                try
                {
                    UploadCompletion actual = UploadCallbackRules.Classify(expectedItem, raw, returnedItem, io, legal);
                    if (actual != expected)
                        throw new InvalidOperationException("expected " + expected + ", observed " + actual);
                }
                catch (Exception error)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL expectedItem=" + expectedItem + " raw=" + raw
                        + " returnedItem=" + returnedItem + " legal=" + legal + " io=" + io
                        + " shape=" + itemShape + ": " + error.Message);
                }
            }
            if (cases != 384)
            {
                failed++;
                Console.Error.WriteLine("FAIL callback table count: " + cases + ", expected 384");
            }
            Console.WriteLine("UploadCallbackRules: " + cases + " cases, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
