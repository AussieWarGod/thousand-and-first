#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomCarryRuntimeSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static string Slice(string source, string start, string end)
		{
			int at = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(at, 0, start);
			int until = source.IndexOf(end, at + start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(until, at, end);
			return source.Substring(at, until - at);
		}

		[Test]
		public void CraftedSignIsTruthfulAndMerchantStockIsOnlyAnAlternative()
		{
			XDocument objects = XDocument.Parse(Source("ObjectBlueprints.xml"));
			XElement sign = objects.Descendants("object").Single(x =>
				(string)x.Attribute("Name") == "r_KingdomCarrySign");
			XElement craft = sign.Elements("part").Single(x =>
				(string)x.Attribute("Name") == "TinkerItem");
			ClassicAssert.AreEqual("true", (string)craft.Attribute("CanBuild"));
			ClassicAssert.AreEqual("1", (string)craft.Attribute("BuildTier"));
			ClassicAssert.AreEqual("00", (string)craft.Attribute("Bits"));
			ClassicAssert.AreEqual("1", (string)craft.Attribute("NumberMade"));

			XDocument populations = XDocument.Parse(Source("PopulationTables.xml"));
			ClassicAssert.GreaterOrEqual(populations.Descendants("object").Count(x =>
				(string)x.Attribute("Blueprint") == "r_KingdomCarrySign"), 2);
		}

		[Test]
		public void ScanAcceptsMixedWholeObjectsButRefusesUnsafeOrAmbiguousCargo()
		{
			string runtime = KingdomCarryRuntimeLogicalSource.Read();
			string scan = Slice(runtime, "private static bool TryScanDesignation(",
				"private static bool EligibleSource(");
			string eligibility = Slice(runtime, "private static bool EligibleSource(",
				"private static bool CargoShaped(");
			StringAssert.Contains("KingdomLifecycleTopology.Inventory", scan);
			StringAssert.Contains("KingdomLifecycleTopology.Cell", scan);
			StringAssert.Contains("KingdomLifecycleRules.MaxCarrySources", scan);
			StringAssert.Contains("HashSet<string>", scan);
			StringAssert.Contains("IDIfAssigned", scan);
			StringAssert.DoesNotContain("sources.Sort", scan);
			StringAssert.DoesNotContain(".ID;", scan);
			StringAssert.Contains("item.Count", eligibility);
			StringAssert.Contains("item.IsImportant()", eligibility);
			StringAssert.Contains("item.Equipped != null", eligibility);
			StringAssert.Contains("!item.IsTakeable()", eligibility);
			StringAssert.Contains("!FounderOwned(item) || item.IsOwned()", eligibility);
			StringAssert.Contains("item.OwnedByPlayer", runtime);
			StringAssert.Contains("DroppedByPlayer", runtime);
			ClassicAssert.IsFalse(runtime.Contains("TryMaterialOf"),
				"exact carry accepts arbitrary eligible GameObjects, not material buckets");
		}

		[Test]
		public void ConsentPrecedesReservationAndPublicationPrecedesEveryPhysicalCallback()
		{
			string guestbook = KingdomGuestbookLogicalSource.Read();
			string action = Slice(guestbook, "public static void AttemptPlantCarrySign(",
				"/// <summary>Compatibility resolver for v5 saves only.");
			int consent = action.IndexOf("Popup.ShowYesNo", StringComparison.Ordinal);
			int publish = action.IndexOf("KingdomCarryRuntime.PublishPlant", StringComparison.Ordinal);
			ClassicAssert.Greater(consent, 0);
			ClassicAssert.Greater(publish, consent);

			string runtime = KingdomCarryRuntimeLogicalSource.Read();
			string prepare = Slice(runtime, "internal static bool TryPreparePlant(",
				"/// <summary>After consent");
			ClassicAssert.IsFalse(prepare.Contains("TryPrepareManifestReservation"));
			ClassicAssert.IsFalse(prepare.Contains("TryPublishCarry"));
			ClassicAssert.IsFalse(prepare.Contains("Destroy("));
			string commit = Slice(runtime, "internal static bool PublishPlant(",
				"internal static bool Drive(");
			AssertOrdered(commit, "TryPrepareManifestReservation",
				"PrepareCarrySchedule", "PrepareExactCarrySource",
				"PrepareExactCarryOutput", "FreezeExactCarryManifest",
				"TryPublishCarry", "TryActivateManifestReservation", "Drive(");
		}

		[Test]
		public void ExactAdapterMovesSameReferencesWithoutCargoDestroyMintOrStackMerge()
		{
			string runtime = KingdomCarryRuntimeLogicalSource.Read();
			string move = Slice(runtime, "public object InvokeCarryMove(",
				"public object InvokeLifecycleProjection(");
			StringAssert.Contains("ReferenceEquals(accepted, item)", move);
			StringAssert.Contains("NoStack: true", move);
			StringAssert.Contains("return item;", move);
			ClassicAssert.IsFalse(move.Contains("Destroy("));
			ClassicAssert.IsFalse(move.Contains("Obliterate("));
			ClassicAssert.IsFalse(move.Contains("GameObject.Create"));
			ClassicAssert.IsFalse(move.Contains("KingdomMaterials.Deliver"));

			string central = KingdomCentralLogisticsLogicalSource.Read();
			string arrival = Slice(central,
				"internal static bool TryMaterializeManifestArrival(",
				"internal static bool TryAcknowledgeManifestPickup(");
			StringAssert.Contains("SystemLongDistanceMoveTo", arrival);
			StringAssert.Contains("binding.ObjectId", arrival);
			ClassicAssert.IsFalse(arrival.Contains("GameObject.Create"));
			ClassicAssert.IsFalse(arrival.Contains("Destroy("));
			ClassicAssert.IsFalse(arrival.Contains("Obliterate("));
		}

		[Test]
		public void ProtectedPurposeEvidenceBlocksDesignationMovementAndReloadCredit()
		{
			string runtime = KingdomCarryRuntimeLogicalSource.Read();
			const string protectedEvidence =
				"TryObjectGraphAvailableForOrdinaryTransfer(item, out _)";
			string eligibility = Slice(runtime, "private static bool EligibleSource(",
				"private static bool CargoShaped(");
			StringAssert.Contains(
				"TryObjectGraphAvailableForOrdinaryTransfer(item, out failure)", eligibility);
			string sign = Slice(runtime, "private static bool ExactSign(",
				"private static int ReferenceCount(");
			StringAssert.Contains("TryObjectAvailableForLocalDebit(sign, out _)", sign);

			string move = Slice(runtime, "public object InvokeCarryMove(",
				"public object InvokeLifecycleProjection(");
			string inventory = Slice(move,
				"if (targetTopology == KingdomLifecycleTopology.Inventory)",
				"else if (targetTopology == KingdomLifecycleTopology.Cell)");
			string cell = Slice(move,
				"else if (targetTopology == KingdomLifecycleTopology.Cell)",
				"else return null;");
			ClassicAssert.AreEqual(2, Count(inventory, protectedEvidence));
			ClassicAssert.AreEqual(2, Count(cell, protectedEvidence));
			AssertOrdered(inventory, protectedEvidence,
				"owner.Inventory.AddObject(item, null, Silent: true, NoStack: true)",
				protectedEvidence);
			AssertOrdered(cell, protectedEvidence,
				"cell.AddObject(item, NoStack: true, Silent: true)", protectedEvidence);

			string observation = Slice(runtime, "private void AddAt(",
				"private Observation ObjectObservation(");
			ClassicAssert.AreEqual(2, Count(observation, protectedEvidence));
			StringAssert.DoesNotContain(".SetIntProperty(",
				eligibility + sign + move + observation);
			StringAssert.DoesNotContain(".SetStringProperty(",
				eligibility + sign + move + observation);
		}

		[Test]
		public void CentralTripsUseTwelveObjectCapacityAndSameManifestAcknowledgements()
		{
			string runtime = KingdomCarryRuntimeLogicalSource.Read();
			StringAssert.Contains("TryAcknowledgeManifestPickup(system, op.Id", runtime);
			StringAssert.Contains("TryAcknowledgeManifestDelivered(system, op.Id", runtime);
			StringAssert.Contains("op.ManifestRevision", runtime);

			string central = KingdomCentralLogisticsLogicalSource.Read();
			string reserve = Slice(central,
				"internal static bool TryPrepareManifestReservation(",
				"internal static bool TryActivateManifestReservation(");
			StringAssert.Contains("KingdomLogisticsRules.CarrierCapacity - 1", reserve);
			StringAssert.Contains("if (count > KingdomLogisticsRules.CarrierCapacity)", reserve);
			StringAssert.Contains("deliveryManifestSourceStart: start", reserve);
			StringAssert.Contains("deliveryManifestSourceCount: count", reserve);
		}

		[Test]
		public void ThreatWaitNeverConvertsExactCargoIntoRoadLoss()
		{
			string runtime = KingdomCarryRuntimeLogicalSource.Read();
			string projection = Slice(runtime,
				"case KingdomLifecyclePhase.ProjectionIntent:",
				"case KingdomLifecyclePhase.Projected:");
			StringAssert.Contains("ThreatPresent(system, zone)", projection);
			StringAssert.Contains("SetExactCarryDestinationSafety(book, op, true, now)", projection);
			StringAssert.Contains("SetExactCarryDestinationSafety(book, op, false, now)", projection);
			StringAssert.Contains("ProveExactCarryDestination(book,\n\t\t\t\t\top, source, output, false",
				runtime);
			ClassicAssert.IsFalse(runtime.Contains("output, true"));
			ClassicAssert.IsFalse(runtime.Contains("lost: true"));
		}

		[Test]
		public void ScalarDestroyMintPathIsExplicitlyLegacyOnly()
		{
			string guestbook = KingdomGuestbookLogicalSource.Read();
			string action = Slice(guestbook, "public static void AttemptPlantCarrySign(",
				"/// <summary>Compatibility resolver for v5 saves only.");
			ClassicAssert.IsFalse(action.Contains("KingdomMaterials.Deliver"));
			ClassicAssert.IsFalse(action.Contains("Destroy("));
			ClassicAssert.IsFalse(action.Contains("Obliterate("));
			StringAssert.Contains("ResolveLegacyHaulIfDue", guestbook);
			StringAssert.Contains("if (manifest.Total() <= 0) return;", guestbook);
		}

		[Test]
		public void CargoAndDestinationNamesAreEscapedOnlyInRenderedSnapshots()
		{
			string runtime = KingdomCarryRuntimeLogicalSource.Read();
			StringAssert.Contains(
				"KingdomPresentation.Rich(item.BaseDisplayNameStripped)", runtime);
			StringAssert.Contains(
				"string destination = KingdomPresentation.Rich(op.DestinationSettlementName);",
				runtime);
			StringAssert.Contains(
				"DestinationSettlementName = system.SeatName;", runtime);
			StringAssert.DoesNotContain(
				"DestinationSettlementName = KingdomPresentation.Rich", runtime);
		}

		private static int Count(string source, string value)
		{
			int count = 0;
			for (int at = 0; ; )
			{
				at = source.IndexOf(value, at, StringComparison.Ordinal);
				if (at < 0) return count;
				count++;
				at += value.Length;
			}
		}

		private static void AssertOrdered(string source, params string[] values)
		{
			int cursor = -1;
			for (int i = 0; i < values.Length; i++)
			{
				int at = source.IndexOf(values[i], cursor + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(at, cursor, values[i]);
				cursor = at;
			}
		}
	}
}
#endif
