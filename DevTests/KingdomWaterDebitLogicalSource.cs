#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomWaterDebitLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomWaterDebit.cs",
			"Growth/KingdomWaterDebit.Commit.cs",
			"Growth/KingdomWaterDebit.RollbackAndVerification.cs",
			"Growth/KingdomWaterDebit.ReservationVerification.cs",
			"Growth/KingdomWaterDebit.ClaimsAndHelpers.cs"
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
