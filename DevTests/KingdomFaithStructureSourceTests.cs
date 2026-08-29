#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomFaithStructureSourceTests
	{
		private static readonly string[] ProductionFiles =
		{
			"Experience/KingdomFaith.cs",
			"Experience/KingdomFaith.z01.ShrinePass.cs",
			"Experience/KingdomFaith.z02.ShrinePressureAndEducation.cs",
			"Experience/KingdomFaith.z03.EducationAndConsecration.cs"
		};

		[Test]
		public void LogicalAuthorityRetainsEveryDeclarationInOriginalOrder()
		{
			Assert.AreEqual(4, KingdomFaithLogicalSource.FileCount);
			string source = KingdomFaithLogicalSource.Read();
			Assert.AreEqual(4, Count(source, "public static partial class KingdomFaith"));
			AssertOrdered(source, "public static bool Enabled", "private static KingdomElapsedOptionDecision ObserveOption(",
				"private static void CommitOption(", "private static void CancelUncommittedFaith(",
				"private static void ResumeCanceledFaith(", "private static void AnchorPreservedFaith(",
				"public static void OnZoneActivated(", "private static void ForgetUnreached(",
				"private static bool LiftShrineBrink(", "private static void RunShrine(",
				"private static void ForgetPull(", "private static void AdvancePull(",
				"private static void SpendShrineWindow(", "private static void HandOffOpposedPressure(",
				"private static void RunEducationLapse(", "private static string NameOf(",
				"public static bool ZoneEducated(", "public static KingdomLodgingRules.Closeness EducatedCloseness(",
				"public static void OpenConsecration(", "private static List<GameObject> FaithBuildingsIn(");
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
