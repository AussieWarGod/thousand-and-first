#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomHappeningLifecycleLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomPhysicalHappeningKind.cs",
			"Simulation/City/KingdomHappeningLifecyclePhase.cs",
			"Simulation/City/KingdomHappeningSinkState.cs",
			"Simulation/City/KingdomHappeningLifecycleFault.cs",
			"Simulation/City/KingdomHappeningResumeAction.cs",
			"Simulation/City/KingdomHappeningParticipant.cs",
			"Simulation/City/KingdomHappeningSemanticReceipt.cs",
			"Simulation/City/KingdomHappeningProposal.cs",
			"Simulation/City/KingdomHappeningOperation.cs",
			"Simulation/City/KingdomHappeningLifecycleBook.cs",
			"Simulation/City/KingdomHappeningLifecycleRules.cs",
			"Simulation/City/KingdomHappeningLifecycleRules.Transitions.cs",
			"Simulation/City/KingdomHappeningLifecycleRules.Recovery.cs",
			"Simulation/City/KingdomHappeningLifecycleRules.Codec.cs",
			"Simulation/City/KingdomHappeningLifecycleRules.Validation.cs",
			"Simulation/City/KingdomHappeningLifecycleRules.Wire.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Paths.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Paths[i])).Append('\n');
			}
			return source.ToString();
		}
	}
}
#endif
