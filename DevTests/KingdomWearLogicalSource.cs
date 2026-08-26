#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomWearLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomWear.00.r_KingdomWear.cs",
			"Growth/KingdomWear.01.RepairRecovery.cs",
			"Growth/KingdomWear.02.RepairTargetAndCarry.cs",
			"Growth/KingdomWear.03.Activation.cs",
			"Growth/KingdomWear.04.Resolve.cs",
			"Growth/KingdomWear.05.WearRoll.cs",
			"Growth/KingdomWear.06.DamageIncidents.cs",
			"Growth/KingdomWear.07.LeakEntry.cs",
			"Growth/KingdomWear.08.LeakFrame.cs",
			"Growth/KingdomWear.09.LeakContinuation.cs",
			"Growth/KingdomWear.10.LeakOutputs.cs",
			"Growth/KingdomWear.11.LeakReceipts.cs",
			"Growth/KingdomWear.12.RepairProjection.cs",
			"Growth/KingdomWear.13.RepairCompletion.cs",
			"Growth/KingdomWear.cs"
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
