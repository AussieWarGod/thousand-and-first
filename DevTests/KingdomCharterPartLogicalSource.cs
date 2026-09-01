#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCharterPartLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Core/KingdomCharterPart.cs",
			"Core/KingdomCharterPart.Chapters.cs",
			"Core/KingdomCharterPart.ExternalOwnership.cs",
			"Core/KingdomCharterPart.PolityTraffic.cs",
			"Core/KingdomCharterPart.Succession.cs",
			"Core/KingdomCharterPart.RealmRemoval.cs",
			"Core/KingdomCharterPart.Ground.cs",
			"Core/KingdomCharterPart.GroundWork.cs",
			"Core/KingdomCharterPart.Civic.cs",
			"Core/KingdomCharterPart.Commission.cs",
			"Core/KingdomCharterPart.Plans.cs",
			"Core/KingdomCharterPart.Threat.cs",
			"Core/KingdomCharterPart.Trade.cs",
			"Core/KingdomCharterPart.Vessels.cs",
			"Core/KingdomCharterPart.MealAndAdoption.cs",
			"Core/KingdomCharterPart.ReleaseAndCreed.cs"
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
