#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSealRecordLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomSealStatus.cs",
			"Core/KingdomSealRecord.cs",
			"Core/KingdomSealRecord.Fields.cs",
			"Core/KingdomSealRecord.Writing.cs",
			"Core/KingdomSealRecord.Reading.cs",
			"Core/KingdomSealRecord.Validation.cs",
			"Core/KingdomSealRecord.Profile.cs",
			"Core/KingdomSealRecord.Utilities.cs",
			"Core/KingdomSealRecord.Collections.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			}
			return source.ToString();
		}
	}
}
#endif
