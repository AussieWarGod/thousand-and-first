#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomLocusLogicalSource
	{
		internal const int FileCount = 9;

		private static readonly string[] Paths =
		{
			"Experience/KingdomLocus.cs",
			"Experience/KingdomLocus.z00.Keeper.cs",
			"Experience/KingdomLocus.z00a.KeeperProjection.cs",
			"Experience/KingdomLocus.z00b.Ambient.cs",
			"Experience/KingdomLocus.z01.GuestAndPilgrimPass.cs",
			"Experience/KingdomLocus.z02.LifecycleGuestsAndHeart.cs",
			"Experience/KingdomLocus.z03.WaterAndGuestPart.cs",
			"Experience/r_KingdomGuest.cs",
			"Experience/r_KingdomLocusAmbient.cs"
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
