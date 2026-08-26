#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomApiRulesLogicalSource
	{
		private static readonly string[] Files =
		{
			"Api/KingdomExtensionVerdict.cs",
			"Api/KingdomApiRules.cs",
			"Api/KingdomApiRules.Admission.cs",
			"Api/KingdomApiRules.Streams.cs",
			"Api/KingdomApiRules.Text.cs",
			"Api/KingdomApiRules.Behaviour.cs",
			"Api/KingdomApiRules.Identity.cs"
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
