#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomMirrorGateDestinationTests
	{
		private const string Hub = "r_TAF_MirrorGate_JoppaWorld.11.22.1.1.10_20,10";
		private const string North = "r_TAF_MirrorGate_JoppaWorld.14.19.2.0.10_5,7";
		private const string South = "r_TAF_MirrorGate_JoppaWorld.09.31.0.2.10_31,4";
		private const string Stray = "r_TAF_MirrorGate_JoppaWorld.07.07.0.0.10_4,4";

		private static KingdomGateRow[] Hubbed()
		{
			return new[]
			{
				new KingdomGateRow(Hub, "Kavvat", North),
				new KingdomGateRow(North, "Ossuary Reach", Hub),
				new KingdomGateRow(South, "Sallow Ford", Hub),
				new KingdomGateRow(Stray, "Distant Fold", "")
			};
		}

		[Test]
		public void HubSpokesAreExactIncomingRowsInRegisterOrder()
		{
			int[] spokes = KingdomMirrorGateRules.HubSpokeIndices(Hubbed(), Hub);
			CollectionAssert.AreEqual(new[] { 1, 2 }, spokes);
			Assert.AreEqual(0, KingdomMirrorGateRules.HubSpokeIndices(Hubbed(), Stray).Length);
			Assert.AreEqual(0, KingdomMirrorGateRules.HubSpokeIndices(Hubbed(), "missing").Length);
		}

		[Test]
		public void DuplicateCityRowsCannotBecomeExtraSpokes()
		{
			string text = Hub + "^Kavvat^" + North + "|" + North
				+ "^Ossuary Reach^" + Hub + "|" + South + "^ossuary reach^" + Hub;
			Assert.IsFalse(KingdomMirrorGateRules.TryParseRegister(text,
				out KingdomGateRow[] rows, out int dropped));
			Assert.AreEqual(1, dropped);
			Assert.AreEqual(2, rows.Length);
			CollectionAssert.AreEqual(new[] { 1 },
				KingdomMirrorGateRules.HubSpokeIndices(rows, Hub));
		}

		[Test]
		public void SelectionChangesOnlyTheHubOutwardColumn()
		{
			KingdomGateRow[] rows = Hubbed();
			KingdomGateVerdict verdict = KingdomMirrorGateRules.TrySelectHubDestination(
				rows, Hub, South, out KingdomGateRow[] next, out string previous);
			Assert.AreEqual(KingdomGateVerdict.Joined, verdict);
			Assert.AreEqual(North, previous);
			Assert.AreEqual(South, KingdomMirrorGateRules.PartnerOf(next, Hub));
			Assert.AreEqual(Hub, KingdomMirrorGateRules.PartnerOf(next, North));
			Assert.AreEqual(Hub, KingdomMirrorGateRules.PartnerOf(next, South));
			Assert.AreEqual("", KingdomMirrorGateRules.PartnerOf(next, Stray));
			Assert.AreEqual(North, rows[0].Partner, "copy-on-write preserves the frozen register");
			for (int i = 0; i < rows.Length; i++)
			{
				Assert.AreEqual(rows[i].Key, next[i].Key);
				Assert.AreEqual(rows[i].City, next[i].City);
			}
		}

		[Test]
		public void SelectionIsIdempotentAndDoesNotMintAnotherAuthority()
		{
			KingdomGateRow[] rows = Hubbed();
			Assert.AreEqual(KingdomGateVerdict.Joined,
				KingdomMirrorGateRules.TrySelectHubDestination(rows, Hub, North,
					out KingdomGateRow[] next, out string previous));
			Assert.AreSame(rows, next);
			Assert.AreEqual(North, previous);
		}

		[Test]
		public void UnknownSelfAndNonSpokeDestinationsFailWithoutMutation()
		{
			KingdomGateRow[] rows = Hubbed();
			Assert.AreEqual(KingdomGateVerdict.RefusedUnkeyed,
				KingdomMirrorGateRules.TrySelectHubDestination(rows, Hub, "missing",
					out KingdomGateRow[] missing, out string _));
			Assert.AreSame(rows, missing);
			Assert.AreEqual(KingdomGateVerdict.RefusedNamed,
				KingdomMirrorGateRules.TrySelectHubDestination(rows, Hub, Hub,
					out KingdomGateRow[] self, out string _));
			Assert.AreSame(rows, self);
			Assert.AreEqual(KingdomGateVerdict.RefusedUnkeyed,
				KingdomMirrorGateRules.TrySelectHubDestination(rows, Hub, Stray,
					out KingdomGateRow[] stray, out string _));
			Assert.AreSame(rows, stray);
		}

		[Test]
		public void HubReconciliationPreservesAnExplicitLawfulChoice()
		{
			KingdomGateRow[] rows = Hubbed();
			KingdomMirrorGateRules.TrySelectHubDestination(rows, Hub, South,
				out KingdomGateRow[] selected, out string _);
			Assert.AreEqual(KingdomGateVerdict.Joined,
				KingdomMirrorGateRules.TryHub(selected, "Kavvat", out KingdomGateRow[] next,
					out int rekeyed, out string hubKey));
			Assert.AreEqual(Hub, hubKey);
			Assert.AreEqual(South, KingdomMirrorGateRules.PartnerOf(next, Hub));
			Assert.AreEqual(Hub, KingdomMirrorGateRules.PartnerOf(next, Stray),
				"reconciliation still turns every previously unkeyed spoke toward the hub");
			Assert.AreEqual(1, rekeyed);
			for (int i = 0; i < selected.Length; i++)
			{
				Assert.AreEqual(selected[i].Key, next[i].Key);
				Assert.AreEqual(selected[i].City, next[i].City);
			}
		}

		[Test]
		public void PlayerFacingRekeyTextNamesBothEndsAndExactConsequence()
		{
			string prompt = KingdomMirrorGateRules.DestinationPrompt(
				"Kavvat", "Ossuary Reach", "Sallow Ford");
			StringAssert.Contains("Kavvat", prompt);
			StringAssert.Contains("Ossuary Reach", prompt);
			StringAssert.Contains("Sallow Ford", prompt);
			StringAssert.Contains("Only the capital arch's outward crossing changes", prompt);
			StringAssert.Contains("nothing is spent", prompt);
			string line = KingdomMirrorGateRules.DestinationChangedLine("Kavvat", "Sallow Ford");
			StringAssert.Contains("Kavvat", line);
			StringAssert.Contains("Sallow Ford", line);
			StringAssert.Contains("still answers the capital", line);
		}

		[Test]
		public void RuntimeUsesTheRegisterCasAndNeverLoadsARemoteDestination()
		{
			string runtime = TestMain.ReadRepositoryText(
				"Growth/KingdomMirrorGate.Destination.cs");
			StringAssert.Contains("KingdomCrown.CrownedHere(system, hubCity)", runtime);
			StringAssert.Contains("KingdomMirrorGateRules.HubSpokeIndices", runtime);
			StringAssert.Contains("Popup.PickOption", runtime);
			StringAssert.Contains("GetStringGameState", runtime);
			StringAssert.Contains("TryReadDestinationRegister", runtime);
			StringAssert.Contains("FormatRegister(Rows)", runtime);
			StringAssert.Contains("TryWriteDestination(frozen, next)", runtime);
			StringAssert.Contains("SetStringGameState", runtime);
			StringAssert.Contains("TrySelectHubDestination", runtime);
			StringAssert.Contains("ReAnchorHere()", runtime);
			StringAssert.Contains("GameObject.Validate(gateObject)", runtime);
			StringAssert.Contains("ReferenceEquals(Actor, The.Player)", runtime);
			StringAssert.Contains("KingdomUpgrade.IsFunctionallyBuilt(gateObject)", runtime);
			StringAssert.DoesNotContain("GetIntProperty(\"KingdomBuilt\")", runtime);
			StringAssert.Contains("KingdomGrid", runtime);
			StringAssert.DoesNotContain("GetZone(", runtime);
			StringAssert.DoesNotContain("ZoneManager", runtime);
			string canChoose = runtime.Substring(runtime.IndexOf(
				"internal static bool CanChooseDestination", System.StringComparison.Ordinal));
			canChoose = canChoose.Substring(0, canChoose.IndexOf(
				"private static bool TryWriteDestination", System.StringComparison.Ordinal));
			StringAssert.DoesNotContain("Register(null)", canChoose,
				"inventory action discovery is a read, not a register repair");
			string choose = runtime.Substring(runtime.IndexOf(
				"internal static bool ChooseDestination", System.StringComparison.Ordinal));
			StringAssert.DoesNotContain("Register(system)", choose,
				"direct invocation must fail closed instead of repairing before consent");
			StringAssert.DoesNotContain("Anchor(Gate)", choose,
				"destination choice reads exact physical identity before any anchor mutation");
			string part = TestMain.ReadRepositoryText("Growth/KingdomMirrorGate.cs");
			StringAssert.Contains("r_RekeyMirrorGate", part);
			StringAssert.Contains("CanChooseDestination(this)", part);
		}

		[Test]
		public void StrikeAndConversionRefuseKeyedArchesBeforePublicationOrDebit()
		{
			string removal = TestMain.ReadRepositoryText(
				"Growth/KingdomMirrorGate.Removal.cs");
			StringAssert.Contains("TryParseRegister(raw", removal);
			StringAssert.Contains("dropped != 0", removal);
			StringAssert.Contains("FormatRegister(rows)", removal);
			StringAssert.Contains("MayRemove(rows, key)", removal);
			StringAssert.DoesNotContain("SetStringGameState", removal);

			string strike = TestMain.ReadRepositoryText(
				"Growth/KingdomMaterials.08.StrikeOrdering.cs");
			int strikeGuard = strike.IndexOf("KingdomMirrorGate.TryPreflightRemoval",
				System.StringComparison.Ordinal);
			Assert.GreaterOrEqual(strikeGuard, 0);
			Assert.Less(strikeGuard, strike.IndexOf("KingdomConstruction.NewJob",
				System.StringComparison.Ordinal));

			string convert = TestMain.ReadRepositoryText(
				"Growth/KingdomSocket.04.ConversionDeclarationsAndValidation.cs");
			StringAssert.Contains("KingdomMirrorGate.TryPreflightRemoval(Building, Z",
				convert);
			string continuation = TestMain.ReadRepositoryText(
				"Growth/KingdomMaterials.12.StrikeContinuation.cs");
			int firstContinuationGuard = continuation.IndexOf(
				"KingdomMirrorGate.TryPreflightRemoval", System.StringComparison.Ordinal);
			int lastContinuationGuard = continuation.LastIndexOf(
				"KingdomMirrorGate.TryPreflightRemoval", System.StringComparison.Ordinal);
			Assert.Less(firstContinuationGuard, continuation.IndexOf(
				"RemoveStrikePlotPart", System.StringComparison.Ordinal));
			Assert.Greater(lastContinuationGuard, firstContinuationGuard,
				"callback-capable target/link work must be followed by a fresh register proof");
			Assert.Less(lastContinuationGuard, continuation.IndexOf(
				"RemoveStrikePredecessor", System.StringComparison.Ordinal));

			string runtime = TestMain.ReadRepositoryText(
				"Growth/KingdomMirrorGate.Runtime.cs");
			int release = runtime.IndexOf("Release(Gate, system, rows, city)",
				System.StringComparison.Ordinal);
			int condemned = runtime.IndexOf("KingdomMaterials.HasActiveStrikeReceipt",
				System.StringComparison.Ordinal);
			Assert.Greater(condemned, release,
				"an already-keyed condemned arch must remain releasable");
		}
	}
}
#endif
