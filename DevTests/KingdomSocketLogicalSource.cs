#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSocketLogicalSource
	{
		internal const int FileCount = 15;

		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomSocket.00.r_KingdomSocket.cs",
			"Growth/KingdomSocket.01.ConstructionRetry.cs",
			"Growth/KingdomSocket.02.ConstructionInspection.cs",
			"Growth/KingdomSocket.03.ConstructionContinuation.cs",
			"Growth/KingdomSocket.04.ConversionDeclarationsAndValidation.cs",
			"Growth/KingdomSocket.05.ConversionPreparation.cs",
			"Growth/KingdomSocket.06.ConversionProjection.cs",
			"Growth/KingdomSocket.07.ClearanceAndSockets.cs",
			"Growth/KingdomSocket.07b.LegacySweep.cs",
			"Growth/KingdomSocket.08.SocketBuildPreparation.cs",
			"Growth/KingdomSocket.09.SocketBuildExecution.cs",
			"Growth/KingdomSocket.10.Redress.cs",
			"Growth/KingdomSocket.11.ConvertMenu.cs",
			"Growth/KingdomSocket.12.RedressMenu.cs",
			"Growth/KingdomSocket.cs"
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
