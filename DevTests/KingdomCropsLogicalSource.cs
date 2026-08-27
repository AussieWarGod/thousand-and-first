#if TAF_TESTS
using System;
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCropsLogicalSource
	{
		private const string PartsMarker = "// The engine resolves an XML <part Name=\"X\"/>";

		internal static string Read()
		{
			string anchor = TestMain.ReadRepositoryText("Growth/KingdomCrops.cs");
			int parts = anchor.IndexOf(PartsMarker, StringComparison.Ordinal);
			if (parts < 0) throw new InvalidOperationException("Crop part marker is missing.");
			StringBuilder source = new StringBuilder(anchor.Substring(0, parts));
			for (int i = 0; i <= 6; i++)
			{
				string[] names =
				{
					"FieldStateAndFoodCredit", "Milling", "Sowing", "WithdrawalAndWildSeed",
					"Rows", "Delivery", "HelpersAndAnnouncements"
				};
				source.Append(TestMain.ReadRepositoryText("Growth/KingdomCrops.0"
					+ i + "." + names[i] + ".cs")).Append('\n');
			}
			source.Append(anchor.Substring(parts)).Append('\n');
			return source.ToString();
		}
	}
}
#endif
