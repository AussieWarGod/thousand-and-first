using System;
using System.IO;
using System.Globalization;
using System.Text.Json;

namespace ThousandAndFirst.WorkshopSteam
{
    public static class SteamVerify
    {
        private const string Note = "Verify one subscribed installation; no Workshop update is requested.";

        public static int Main(string[] args)
        {
            try
            {
                if (args == null || args.Length != 5 || args[0] != "verify")
                    throw new ArgumentException("Expected verify PLAN PLAN_SHA ITEM RECEIPT_SHA.");
                DeliveryResult result;
                ulong item;
                if (!ulong.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out item)
                    || args[3] != item.ToString(CultureInfo.InvariantCulture))
                    throw new ArgumentException("Expected canonical item identity.");
                using (WorkshopReleaseRegistry registry = WorkshopReleaseRegistry.Open(item))
                using (UploadPackage package = UploadPackage.Open(args[1], args[2], args[3], Note))
                {
                    if (!string.Equals(package.ReceiptSHA, args[4], StringComparison.Ordinal))
                        throw new InvalidDataException("Receipt approval mismatch.");
                    registry.RequireExact();
                    result = SteamInstalledDelivery.Verify(package, path =>
                    {
                        registry.RequireExact();
                        return package.LeaseInstalled(path);
                    });
                    registry.RequireExact();
                }
                // Both package and registry cleanup precede any positive record.
                if (result == null) throw new InvalidDataException("Missing installation result.");
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = result.Status,
                    reason = result.Reason,
                    item = result.Item,
                    version = result.Version,
                    planSHA = result.PlanSHA,
                    receiptSHA = result.ReceiptSHA,
                    installedPath = result.InstalledPath,
                    inventorySHA = result.InventorySHA,
                    scope = "one_subscribed_client_installation",
                    verifiedClientInstallations = result.SubscribedInstallationVerified ? 1 : 0,
                    subscribedInstallationVerified = result.SubscribedInstallationVerified,
                    freshTransferVerified = false,
                    releaseReady = false
                }));
                return result.SubscribedInstallationVerified ? 0 : 3;
            }
            catch (Exception error)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "Refused",
                    errorType = error.GetType().Name,
                    scope = "one_subscribed_client_installation",
                    verifiedClientInstallations = 0,
                    subscribedInstallationVerified = false,
                    freshTransferVerified = false,
                    releaseReady = false
                }));
                return 2;
            }
        }
    }
}
