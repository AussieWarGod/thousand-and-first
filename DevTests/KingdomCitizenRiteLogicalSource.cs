#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCitizenRiteLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Experience/KingdomCitizenRite.cs",
			"Experience/KingdomCitizenRite.Chronicle.cs",
			"Experience/KingdomCitizenRite.Hosting.cs",
			"Experience/KingdomCitizenRite.Conversation.cs",
			"Experience/KingdomCitizenRite.Projection.cs",
			"Experience/KingdomCitizenRiteProjectionRules.cs",
			"Experience/r_KingdomCitizenRiteProjection.cs"
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
