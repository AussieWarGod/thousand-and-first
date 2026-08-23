#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public class KingdomSealRulesTests
	{
		private static KingdomSettlement SampleSettlement()
		{
			KingdomSettlement seat = new KingdomSettlement();
			seat.SettlementName = "Kavvat";
			seat.Vocation = "holding";
			seat.Style = "common";
			seat.FoundedTick = 10;
			seat.FoundingRegionName = "Salt Marsh";
			seat.FoundingTerrainBlueprint = "TerrainSaltMarsh";
			seat.FoundingZLevel = 10;
			seat.Stage = GrowthStage.Camp;
			seat.Population = 3;
			seat.ClaimedZones.Add("JoppaWorld.1.1.1.1.10");
			seat.OriginCounts["salt-born"] = 2;
			seat.CreedCounts["Mechanimists"] = 1;
			seat.RosterNames.Add("Ari");
			seat.RosterOrigins.Add("salt-born");
			seat.RosterArrived.Add("Niv 12");
			seat.DeadNames.Add("Belet");
			seat.DeadCauses.Add("fell in a raid");

			KingdomCityBook book = new KingdomCityBook();
			book.SettlementId = "kavvat-id";
			book.WaterLevel = 25;
			book.ZoneIds.Add("JoppaWorld.1.1.1.1.10");
			book.ZoneDefences.Add(7);
			book.WorkIds.Add(1);
			book.WorkZoneIds.Add("JoppaWorld.1.1.1.1.10");
			book.WorkAnchorsX.Add(12);
			book.WorkAnchorsY.Add(13);
			book.WorkDesignKeys.Add("caskrack");
			book.WorkConditions.Add(95);
			seat.City = book;
			return seat;
		}

		private static KingdomSealRecord SampleCapturedRecord(string lineageId, string legacyId, string originId, int generation, int revision)
		{
			return KingdomSealRules.Capture(
				SampleSettlement(),
				new KingdomSealLineage(lineageId, legacyId, originId, generation, revision),
				"Realm of Salt",
				"Abram",
				new List<string> { "founded", "grew" },
				new List<string> { "rumor" },
				100L);
		}

		[Test]
		public void SanitizeTextRemovesMarkupAndControl()
		{
			string value = "{{R|Kavvat}} &Y\n\\ \u007f";
			string sanitized = KingdomSealRules.SanitizeText(value, 64);
			Assert.AreEqual("Kavvat", sanitized);
			Assert.IsTrue(KingdomSealRules.IsSafeText(sanitized));
		}

		[Test]
		public void InterregnumSeedIsDeterministicAndInputSensitive()
		{
			KingdomSealLineage a = new KingdomSealLineage("lineage-a", "legacy-a-2", "game-a", 2, 3);
			long one = KingdomSealRules.InterregnumSeed(a);
			long two = KingdomSealRules.InterregnumSeed(a);
			long changed = KingdomSealRules.InterregnumSeed(new KingdomSealLineage("lineage-a", "legacy-a-2", "game-a", 2, 4));
			Assert.AreEqual(one, two);
			Assert.AreNotEqual(one, changed);
		}

		[Test]
		public void CaptureCanonicalizesLiveWorkBlueprintsIntoInheritableSemanticKeys()
		{
			KingdomSettlement seat = SampleSettlement();
			KingdomCityBook book = seat.City;
			book.WorkDesignKeys[0] = "r_KingdomTent";
			book.WorkIds.Add(2);
			book.WorkZoneIds.Add(seat.ClaimedZones[0]);
			book.WorkAnchorsX.Add(30);
			book.WorkAnchorsY.Add(15);
			book.WorkDesignKeys.Add("r_KingdomRiteGround");
			book.WorkConditions.Add(88);

			KingdomSealRecord record = KingdomSealRules.Capture(seat,
				new KingdomSealLineage("lineage", "legacy", "origin", 0, 1),
				"Realm", "Founder", new List<string>(), new List<string>(), 100L);
			CollectionAssert.AreEqual(new[] { "tent", "heartbasin" }, record.WorkKeys);

			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryPrepare(record.WorkKeys, record.WorkX,
				record.WorkY, record.WorkConditions, KingdomRules.InheritedState.Held, 0,
				out placement, out fault), fault.ToString());
			Assert.AreEqual("tent", placement.WorkAt(0).Key);
			Assert.AreEqual("heartbasin", placement.WorkAt(1).Key);
		}

		[Test]
		public void AdjacentMigratedBlueprintWorksDegradeLocallyInsteadOfInvalidatingLegacy()
		{
			KingdomSettlement seat = SampleSettlement();
			KingdomCityBook book = seat.City;
			book.WorkDesignKeys[0] = "r_KingdomTent";
			book.WorkIds.Add(2);
			book.WorkZoneIds.Add(seat.ClaimedZones[0]);
			book.WorkAnchorsX.Add(13);
			book.WorkAnchorsY.Add(13);
			book.WorkDesignKeys.Add("r_KingdomHouse");
			book.WorkConditions.Add(90);

			KingdomSealRecord record = KingdomSealRules.Capture(seat,
				new KingdomSealLineage("lineage", "legacy", "origin", 0, 1),
				"Realm", "Founder", new List<string>(), new List<string>(), 100L);
			CollectionAssert.AreEqual(new[] { "tent", "house" }, record.WorkKeys,
				"Seal preserves the proven live blueprint identities");

			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryPrepare(record.WorkKeys, record.WorkX,
				record.WorkY, record.WorkConditions, KingdomRules.InheritedState.Held, 0,
				out placement, out fault), fault.ToString());
			Assert.AreEqual(KingdomInheritRules.MemoryKey, placement.WorkAt(0).Key);
			Assert.AreEqual(KingdomInheritRules.MemoryKey, placement.WorkAt(1).Key);
			Assert.AreNotEqual(placement.WorkAt(0).X, placement.WorkAt(1).X);
		}

		[Test]
		public void PromoteDrawsOnceAndResolvesState()
		{
			KingdomSealRecord record = SampleCapturedRecord("lineage-a", "legacy-a-1", "game-a", 1, 1);
			record = KingdomSealRules.WithTerminalCause(record, "fell", "combat", 9);
			Assert.Throws<InvalidOperationException>(() => KingdomSealRules.Promote(record, KingdomSealEligibility.Checkpointed));

			KingdomSealRecord promoted = KingdomSealRules.Promote(record, KingdomSealEligibility.Ended);
			int expectedRoll = KingdomRules.InterregnumRoll(
				KingdomSealRules.InterregnumSeed(new KingdomSealLineage(promoted.LineageId, promoted.LegacyId,
					promoted.OriginGameId, promoted.Generation, promoted.Revision)));

			Assert.AreEqual(KingdomSealStatus.Promoted, promoted.Status);
			Assert.AreEqual(expectedRoll, promoted.InterregnumRoll);
			Assert.IsTrue(promoted.IsResolved);
		}

		[Test]
		public void JudgeAndMayPromoteFollowEligibilityRules()
		{
			Assert.AreEqual(KingdomSealEligibility.Ended, KingdomSealRules.Judge(true, false));
			Assert.AreEqual(KingdomSealEligibility.Checkpointed, KingdomSealRules.Judge(true, true));
			Assert.AreEqual(KingdomSealEligibility.Living, KingdomSealRules.Judge(false, true));
			Assert.AreEqual(KingdomSealEligibility.Orphaned, KingdomSealRules.Judge(false, false));

			Assert.IsFalse(KingdomSealRules.MayPromote(KingdomSealStatus.Retired, KingdomSealEligibility.Living));
			Assert.IsFalse(KingdomSealRules.MayPromote(KingdomSealStatus.Living, KingdomSealEligibility.Ended));
			Assert.IsTrue(KingdomSealRules.MayPromote(KingdomSealStatus.Terminal, KingdomSealEligibility.Ended));
			Assert.IsFalse(KingdomSealRules.MayPromote(KingdomSealStatus.Terminal, KingdomSealEligibility.Checkpointed));
		}

		[Test]
		public void SelectChoosesLatestEligibleAndSkipsSpent()
		{
			KingdomSealRecord a = KingdomSealRules.PromoteRetirement(KingdomSealRules.WithRetirement(
				SampleCapturedRecord("dynasty", "legacy-a", "game-a", 1, 1)));
			KingdomSealRecord b = KingdomSealRules.PromoteRetirement(KingdomSealRules.WithRetirement(
				SampleCapturedRecord("dynasty", "legacy-b", "game-b", 2, 1)));
			KingdomSealRecord c = KingdomSealRules.PromoteRetirement(KingdomSealRules.WithRetirement(
				SampleCapturedRecord("dynasty", "legacy-c", "game-c", 2, 2)));

			KingdomSealRecord picked = KingdomSealRules.Select(new[] { a, b, c }, new HashSet<string> { "legacy-c" }, KingdomImportPolicy.LatestEligible);
			Assert.AreEqual("legacy-b", picked.LegacyId);
			Assert.AreEqual("dynasty", picked.LineageId);
		}

		[Test]
		public void LineagePersistsWhileEveryGenerationHasUniqueLegacyIdentity()
		{
			KingdomSealRecord founder = SampleCapturedRecord("dynasty", "legacy-founder", "game-founder", 0, 1);
			KingdomSealRecord heir = SampleCapturedRecord("dynasty", "legacy-heir", "game-heir", 1, 1);
			Assert.AreEqual(founder.LineageId, heir.LineageId);
			Assert.AreNotEqual(founder.LegacyId, heir.LegacyId);

			KingdomSealRecord parsed;
			KingdomSealFault fault;
			string detail;
			Assert.IsTrue(KingdomSealRecord.TryParse(heir.Compose(), out parsed, out fault, out detail), detail);
			Assert.AreEqual("dynasty", parsed.LineageId);
			Assert.AreEqual("legacy-heir", parsed.LegacyId);
		}

		[Test]
		public void SchemaOneMigratesItsSingleIdentityToLineageAndLegacy()
		{
			KingdomSealRecord original = SampleCapturedRecord("old-identity", "ignored-v2-identity", "game-old", 0, 1);
			KingdomSealBody current = original.WriteBody();
			KingdomSealBody schemaOne = new KingdomSealBody();
			for (int i = 0; i < current.Keys.Count; i++)
			{
				string key = current.Keys[i];
				if (key == "kind" || key == "legacy")
				{
					continue;
				}
				switch (current.KindOf(key))
				{
				case KingdomSealKind.Text:
					schemaOne.Put(key, current.Text(key));
					break;
				case KingdomSealKind.Number:
					schemaOne.Put(key, current.Number(key));
					break;
				case KingdomSealKind.TextList:
					schemaOne.PutList(key, current.TextList(key));
					break;
				case KingdomSealKind.NumberList:
					schemaOne.PutList(key, current.NumberList(key));
					break;
				default:
					schemaOne.PutList(key, new string[0]);
					break;
				}
			}

			KingdomSealRecord migrated;
			KingdomSealFault fault;
			string detail;
			Assert.IsTrue(KingdomSealRecord.TryParse(KingdomSealFormat.Compose(1, schemaOne),
				out migrated, out fault, out detail), detail);
			Assert.AreEqual("old-identity", migrated.LineageId);
			Assert.AreEqual("old-identity", migrated.LegacyId);
		}

		[Test]
		public void RetirementPromotionUsesExplicitPathOnly()
		{
			KingdomSealRecord retired = KingdomSealRules.WithRetirement(
				SampleCapturedRecord("dynasty", "legacy-retired", "game-retired", 1, 1));
			Assert.Throws<InvalidOperationException>(() => KingdomSealRules.Promote(retired, KingdomSealEligibility.Ended));
			Assert.AreEqual(KingdomSealStatus.Promoted, KingdomSealRules.PromoteRetirement(retired).Status);
		}

		[Test]
		public void RecordReaderRejectsTerminalAttemptWithoutCauseTuple()
		{
			KingdomSealRecord malformed = SampleCapturedRecord("dynasty", "legacy-bad", "game-bad", 1, 1);
			malformed.Status = KingdomSealStatus.Terminal;
			KingdomSealRecord parsed;
			KingdomSealFault fault;
			string detail;
			Assert.IsFalse(KingdomSealRecord.TryParse(malformed.Compose(), out parsed, out fault, out detail));
			Assert.IsNull(parsed);
			Assert.AreEqual(KingdomSealFault.MissingKey, fault);
		}

		[Test]
		public void RecordReaderRejectsEmptyRequiredIdentityToken()
		{
			KingdomSealRecord malformed = SampleCapturedRecord("dynasty", "legacy-bad", "game-bad", 1, 1);
			malformed.LegacyId = "";
			KingdomSealRecord parsed;
			KingdomSealFault fault;
			string detail;
			Assert.IsFalse(KingdomSealRecord.TryParse(malformed.Compose(), out parsed, out fault, out detail));
			Assert.IsNull(parsed);
			Assert.AreEqual(KingdomSealFault.OutOfBounds, fault);
		}

		[Test]
		public void ChooseGroundUsesMostWorksThenLexicalTieBreak()
		{
			KingdomCityBook book = new KingdomCityBook();
			book.WorkZoneIds.Add("z2");
			book.WorkZoneIds.Add("z1");
			book.WorkZoneIds.Add("z1");
			string grounded = KingdomSealRules.ChooseGround(book, new List<string> { "z3" });
			Assert.AreEqual("z1", grounded);

			KingdomCityBook none = new KingdomCityBook();
			string fallback = KingdomSealRules.ChooseGround(none, new List<string> { "z9", "z4" });
			Assert.AreEqual("z4", fallback);
		}
	}
}
#endif
