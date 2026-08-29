#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomCarryRuntimeStructureSourceTests
	{
		private static readonly string[] ProductionFiles =
		{
			"Experience/KingdomCarryRuntime.cs",
			"Experience/KingdomCarryRuntime.z01.DriveAndSinks.cs",
			"Experience/KingdomCarryRuntime.z02.Designation.cs",
			"Experience/KingdomCarryRuntime.z03.TrustedWorld.cs",
			"Experience/KingdomCarryRuntime.z04.ScheduleObservations.cs"
		};

		[Test]
		public void LogicalAuthorityRetainsEveryDeclarationInOriginalOrder()
		{
			Assert.AreEqual(5, KingdomCarryRuntimeLogicalSource.FileCount);
			string source = KingdomCarryRuntimeLogicalSource.Read();
			Assert.AreEqual(5, Count(source, "internal static partial class KingdomCarryRuntime"));
			Assert.AreEqual(1, Count(source, "private sealed class CarryWorld"));
			AssertOrdered(source, "internal sealed class PlantPlan", "internal static bool HasOpenOrLegacy(",
				"internal static bool TryPreparePlant(", "internal static bool PublishPlant(",
				"internal static bool Drive(", "private static bool SettlePickups(",
				"private static bool SettleSinks(", "private static bool TryScanDesignation(",
				"private static bool EligibleSource(", "private static KingdomCarryBook Authority(",
				"private sealed class CarryWorld", "public object InvokeSchedule(",
				"public object InvokeCarryMove(",
				"private List<IKingdomLifecycleTrustedObservation> Build(",
				"private static KingdomLifecycleResourceRevision ScheduleRow(",
				"private sealed class Observation");
		}

		[Test]
		public void EveryProductionShardIsStrictlyUnderThreeHundredPhysicalLines()
		{
			for (int i = 0; i < ProductionFiles.Length; i++)
			{
				string source = TestMain.ReadRepositoryText(ProductionFiles[i]);
				int lines = source.Replace("\r\n", "\n").Split('\n').Length;
				Assert.Less(lines, 300, ProductionFiles[i]);
			}
		}

		private static int Count(string source, string token)
		{
			int count = 0;
			int cursor = 0;
			while ((cursor = source.IndexOf(token, cursor, StringComparison.Ordinal)) >= 0)
			{
				count++;
				cursor += token.Length;
			}
			return count;
		}

		private static void AssertOrdered(string source, params string[] tokens)
		{
			int cursor = -1;
			for (int i = 0; i < tokens.Length; i++)
			{
				int next = source.IndexOf(tokens[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, tokens[i]);
				cursor = next;
			}
		}
	}
}
#endif
