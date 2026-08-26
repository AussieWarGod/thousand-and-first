#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSemanticSelectionLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomSemanticSelection.PersonPlan.cs",
			"Core/KingdomSemanticSelection.cs",
			"Core/KingdomSemanticSelection.NamesAndBlueprints.cs",
			"Core/KingdomSemanticSelection.ArrivalCellsAndCatalogue.cs"
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
