namespace ThousandAndFirst.WorkshopSteam
{
    public static class DeliveryTests
    {
        public static int Main()
        {
            int packages = UploadPackageTests.Run();
            int installed = UploadPackageTests.RunInstalled();
            return packages == 0 && installed == 0 ? 0 : 1;
        }
    }
}
