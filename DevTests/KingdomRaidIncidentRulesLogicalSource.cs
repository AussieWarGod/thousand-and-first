#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomRaidIncidentRulesLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Raids/KingdomRaidIncidentRules.cs",
			"Raids/KingdomRaidIncidentRules.00.DefenceReservationsAndIdentity.cs",
			"Raids/KingdomRaidIncidentRules.01.PublicationValidation.cs",
			"Raids/KingdomRaidIncidentRules.02.Apply.cs",
			"Raids/KingdomRaidIncidentRules.03.LedgerValidationAndCopy.cs",
			"Raids/KingdomRaidIncidentRules.04.ResolutionAndIncidentShape.cs",
			"Raids/KingdomRaidIncidentRules.05.LedgerChannelAndRecoveryShape.cs"
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
