#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPurposeLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomPurpose.cs",
			"Growth/KingdomPurpose.00.CatalogueAndDispatch.cs",
			"Growth/KingdomPurpose.01.Transport.cs",
			"Growth/KingdomPurpose.02.Commitments.cs",
			"Growth/KingdomPurpose.03.CargoIdentityAndEscrow.cs",
			"Growth/KingdomPurpose.04.DeliveryAndLookup.cs",
			"Growth/KingdomPurpose.05.Siting.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Paths.Length; i++)
				source.Append(TestMain.ReadRepositoryText(Paths[i])).Append('\n');
			return source.ToString();
		}
	}
}
#endif
