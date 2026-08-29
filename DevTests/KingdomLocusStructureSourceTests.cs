#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomLocusStructureSourceTests
	{
		private static readonly string[] ProductionFiles =
		{
			"Experience/KingdomLocus.cs",
			"Experience/KingdomLocus.z00.Keeper.cs",
			"Experience/KingdomLocus.z00a.KeeperProjection.cs",
			"Experience/KingdomLocus.z00b.Ambient.cs",
			"Experience/KingdomLocus.z01.GuestAndPilgrimPass.cs",
			"Experience/KingdomLocus.z02.LifecycleGuestsAndHeart.cs",
			"Experience/KingdomLocus.z03.WaterAndGuestPart.cs",
			"Experience/r_KingdomLocusAmbient.cs"
		};

		[Test]
		public void LogicalAuthorityRetainsEveryDeclarationInOriginalOrder()
		{
			Assert.AreEqual(8, KingdomLocusLogicalSource.FileCount);
			string source = KingdomLocusLogicalSource.Read();
			Assert.AreEqual(7, Count(source, "public static partial class KingdomLocus"));
			Assert.AreEqual(1, Count(source, "public class r_KingdomGuest : IPart"));
			Assert.AreEqual(1, Count(source,
				"public sealed class r_KingdomLocusAmbient : IPart"));
			AssertOrdered(source, "public static bool Enabled", "public static void OnZoneActivated(",
				"private static void RunKeeperPass(", "private static List<GameObject> FindBenches(",
				"private static GameObject FindBench(",
				"private static List<string> KeeperCandidates(",
				"private static string FirstMarkedCandidate(",
				"private static GameObject FindSettler(",
				"private static void DemoteKeepers(", "private static void DemoteKeeper(",
				"private static void UpdateKeeperConversation(",
				"private static void DescribeOtherBenches(",
				"private static void SetBenchDescription(", "private static void ConfigureAmbient(",
				"internal static bool TryClaimAmbient(",
				"private static bool AmbientAuthorityCurrent(",
				"private static void RunGuestPass(", "private static bool RunPilgrimPass(",
				"internal static GameObject CreateLifecycleGuest(", "private static GameObject FindCausalPilgrim(",
				"internal static Cell HeartArrivalCell(", "private static bool ResolvePilgrim(",
				"private static GameObject FindGuest(", "private static bool SpawnGuest(",
				"public static void OfferGuestWater(", "private static string PlainGuestName(",
				"private static bool DepartGuest(", "public class r_KingdomGuest : IPart",
				"public override bool WantEvent(", "public override bool HandleEvent(GetInventoryActionsEvent E)",
				"public override bool HandleEvent(InventoryActionEvent E)",
				"public sealed class r_KingdomLocusAmbient : IPart",
				"public override bool WantEvent(",
				"public override bool HandleEvent(IdleQueryEvent E)");
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
