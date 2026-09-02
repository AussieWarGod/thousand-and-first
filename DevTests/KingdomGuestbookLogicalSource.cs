#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomGuestbookLogicalSource
	{
		internal const int FileCount = 8;

		private static readonly string[] Paths =
		{
			"Experience/KingdomGuestbook.cs",
			"Experience/KingdomGuestbook.z01.LodgingAndHousing.cs",
			"Experience/KingdomGuestbook.z01b.MarketHandoff.cs",
			"Experience/KingdomGuestbook.z02.Lifecycle.cs",
			"Experience/KingdomGuestbook.z03.ReportingAndCarrySign.cs",
			"Experience/KingdomCarryHaul.cs",
			"Experience/r_KingdomNotableGuest.cs",
			"Experience/r_KingdomCarrySign.cs"
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
