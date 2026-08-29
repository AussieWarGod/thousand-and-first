#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomFirstGuestRuntimeLogicalSource
	{
		private static readonly string[] Files =
		{
			"Growth/KingdomFirstGuestRuntime.cs",
			"Growth/KingdomFirstGuestRuntime.Admission.cs",
			"Growth/KingdomFirstGuestRuntime.Capacity.cs",
			"Growth/KingdomFirstGuestRuntime.Facts.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			return source.ToString();
		}
	}
}
#endif
