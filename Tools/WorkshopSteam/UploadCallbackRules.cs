namespace ThousandAndFirst.WorkshopSteam
{
    // Classifies completed callbacks only; absence, timeout, authority and delivery are separate.
    public static class UploadCallbackRules
    {
        public static UploadCompletion Classify(ulong expectedItem, int rawEResult,
            ulong returnedItem, bool ioFailure, bool legalAgreementRequired)
        {
            if (expectedItem == 0) return UploadCompletion.Unknown;
            if (legalAgreementRequired) return UploadCompletion.LegalAgreementRequired;
            if (ioFailure) return UploadCompletion.IoFailure;
            if (rawEResult != 1) return UploadCompletion.Rejected;
            if (returnedItem != expectedItem) return UploadCompletion.Unknown;
            return UploadCompletion.Ok;
        }
    }
}
