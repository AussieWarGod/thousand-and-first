#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCitizenshipLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomCitizenship.Part.cs",
			"Core/KingdomCitizenship.cs",
			"Core/KingdomCitizenship.Removal.cs",
			"Core/KingdomCitizenship.ReceiptAndNotices.cs"
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
