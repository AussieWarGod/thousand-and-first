#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomInheritanceStateLogicalSource
	{
		private static readonly string[] Files =
		{
			"World/KingdomInheritanceState.cs",
			"World/KingdomInheritanceState.z01.SerializationAndSelection.cs",
			"World/KingdomInheritanceState.z02.SelectionAndApply.cs",
			"World/KingdomInheritanceState.z03.PaintAndBuilders.cs",
			"World/KingdomInheritanceState.z04.FallbackAndValidation.cs",
			"World/KingdomInheritanceState.z05.ResumeAndTargetBuild.cs",
			"World/KingdomInheritanceState.z06.CommitProof.cs",
			"World/KingdomInheritanceState.z07.LoadedRepairAndRewind.cs",
			"World/KingdomInheritanceState.z08.ValidationAndInstall.cs",
			"World/KingdomInheritanceState.z09.ReleaseAndCleanup.cs",
			"World/KingdomInheritanceState.z10a.ReservationLeases.cs",
			"World/KingdomInheritanceState.z10b.Discoverability.cs",
			"World/KingdomInheritanceState.z11.QuarantineAndZoneNames.cs",
			"World/KingdomInheritanceState.z12.ReservationAndState.cs"
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
