#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomInheritanceWorldExtensionLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"World/KingdomInheritanceWorldExtension.cs",
			"World/KingdomInheritanceWorldIndex.cs",
			"World/KingdomInheritanceWorldRuntime.cs"
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
