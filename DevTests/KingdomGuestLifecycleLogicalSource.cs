#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomGuestLifecycleLogicalSource
	{
		private static readonly string[] Paths =
		{
			"Experience/KingdomGuestLifecycle.cs",
			"Experience/KingdomGuestLifecycle.RemovalAndLodge.cs",
			"Experience/KingdomGuestLifecycle.Settlement.cs",
			"Experience/KingdomGuestLifecycle.LodgeTerminal.cs",
			"Growth/KingdomMarketHandoffGlobalIndex.cs",
			"Experience/KingdomGuestLifecycle.SinksScheduleAndAuthority.cs",
			"Experience/KingdomGuestLifecycle.TrustedWorld.cs",
			"Experience/KingdomGuestLifecycle.WorldModels.cs"
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
