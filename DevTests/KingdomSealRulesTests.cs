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
		private const string FoundingTransaction = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private static readonly string ExactRealmId = MintRealm();
		private static readonly string ExactSettlementId = MintSettlement();

		private static string MintRealm()
		{
			Assert.IsTrue(KingdomIdentityRules.TryMintRealm(FoundingTransaction,
				out string id, out KingdomIdentityFault fault), fault.ToString());
			return id;
		}

		private static string MintSettlement()
		{
			Assert.IsTrue(KingdomIdentityRules.TryMintSettlement(ExactRealmId,
				FoundingTransaction, out string id, out KingdomIdentityFault fault),
				fault.ToString());
			return id;
		}

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
			seat.DeadNames.Add("Belet");
			seat.DeadCauses.Add("fell in a raid");

			KingdomCityBook book = new KingdomCityBook();
			book.SettlementId = ExactSettlementId;
			book.WaterLevel = 25;
			book.WaterCapacity = 25;
			book.ZoneIds.Add("JoppaWorld.1.1.1.1.10");
			book.ZoneDistrictCodes.Add(0);
			book.ZoneLastReadTicks.Add(0L);
			book.ZoneWaterLevels.Add(25L);
			book.ZoneWaterCapacities.Add(25L);
			book.ZoneFoodLevels.Add(0L);
			book.ZoneFoodCapacities.Add(0L);
			book.ZoneMaterialsLevels.Add(0L);
			book.ZoneMaterialsCapacities.Add(0L);
			book.ZoneRoofs.Add(0);
			book.ZoneDefences.Add(7);
			book.ZoneWaterCarries.Add(0);
			book.ZoneFoodCarries.Add(0);
			book.ZoneOwedWater.Add(0);
			book.ZoneOwedFood.Add(0);
			book.ZoneOwedMaterials.Add(0);
			book.WorkIds.Add(1);
			book.WorkZoneIds.Add("JoppaWorld.1.1.1.1.10");
			book.WorkAnchorsX.Add(12);
			book.WorkAnchorsY.Add(13);
			book.WorkDesignKeys.Add("caskrack");
			book.WorkConditions.Add(95);
			book.WorkCrews.Add(0);
			book.WorkRanThroughTicks.Add(0L);
			book.WorkKinds.Add((int)KingdomWorkKind.Other);
			book.WorkStages.Add(0);
			book.WorkProgress.Add(0);
			book.WorkNextTicks.Add(0L);
			book.ResidentIds.Add(1);
			book.ResidentNames.Add("Ari");
			book.ResidentOrigins.Add("salt-born");
			book.ResidentOriginCodes.Add(KingdomResidentRules.OriginCode("salt-born"));
			book.ResidentCreedCodes.Add(0);
			book.ResidentKeptCreeds.Add("");
			book.ResidentArrivedTicks.Add(10L);
			book.ResidentArrived.Add("Niv 12");
			book.ResidentHomeWorkIds.Add(0);
			book.ResidentJobWorkIds.Add(0);
			book.ResidentJobRoles.Add(0);
			book.ResidentDayShapes.Add((int)KingdomDayShape.Hearth);
			book.ResidentStandings.Add((int)KingdomResidentStanding.Resident);
			book.ResidentCauses.Add((int)KingdomStandingCause.None);
			book.ResidentBoundZoneIds.Add("JoppaWorld.1.1.1.1.10");
			book.ResidentRoofStanding.Add(0);
			book.ResidentRoofTicks.Add(0L);
			book.ResidentRoofWarnedTicks.Add(KingdomBrinkRules.Unwarned);
			book.ResidentCreedStanding.Add(0);
			book.ResidentCreedTicks.Add(0L);
			book.ResidentCreedWarnedTicks.Add(KingdomBrinkRules.Unwarned);
			book.ResidentCreedToward.Add("");
			book.ResidentCreedChannels.Add(0);
			seat.City = book;
			return seat;
		}

		private static KingdomSealIdentity SampleIdentity(KingdomSettlement Seat)
		{
			Seat.City.SettlementId = ExactSettlementId;
			KingdomSealIdentity identity = new KingdomSealIdentity
			{
				RealmId = ExactRealmId,
				SettlementId = ExactSettlementId,
				SettlementIds = new List<string> { ExactSettlementId },
				RealmIdentityVersion = KingdomIdentityRules.RulesVersion,
				RealmIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction,
				RealmIdentityTransactionId = FoundingTransaction,
				RealmIdentityFoundedTick = 10L,
				RealmIdentityFirstClaimedZone = Seat.ClaimedZones[0],
				SettlementIdentityVersion = KingdomIdentityRules.RulesVersion,
				SettlementIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction,
				SettlementIdentityTransactionId = FoundingTransaction,
				SettlementIdentityFoundedTick = Seat.FoundedTick,
				SettlementIdentityFirstClaimedZone = Seat.ClaimedZones[0]
			};
			Assert.IsTrue(KingdomSealRules.TryBuildSettlementProvenance(ExactSettlementId,
				identity.SettlementIdentityVersion, identity.SettlementIdentityOrigin,
				identity.SettlementIdentityTransactionId, identity.SettlementIdentityFoundedTick,
				identity.SettlementIdentityFirstClaimedZone,
				identity.SettlementIdentityLegacyId, out string provenance));
			identity.SettlementProvenanceRows.Add(provenance);
			return identity;
		}

		[Test]
		public void CaptureNeverPromotesMutableSettlementNameIntoIdentityPayload()
		{
			KingdomSettlement seat = SampleSettlement();
			seat.City.SettlementId = null;
			Assert.Throws<ArgumentException>(() => KingdomSealRules.Capture(seat, null,
				new KingdomSealLineage("lineage", "legacy", "origin", 0, 1),
				"Realm", "Founder", new List<string>(), new List<string>(), 100L));

			seat.City.SettlementId = ExactSettlementId;
			KingdomSealRecord bound = KingdomSealRules.Capture(seat, SampleIdentity(seat),
				new KingdomSealLineage("lineage", "legacy", "origin", 0, 1),
				"Realm", "Founder", new List<string>(), new List<string>(), 100L);
			Assert.AreEqual(ExactSettlementId, bound.SettlementId);
		}

		private static KingdomSealRecord SampleCapturedRecord(string lineageId, string legacyId, string originId, int generation, int revision)
		{
			KingdomSettlement seat = SampleSettlement();
			return KingdomSealRules.Capture(
				seat, SampleIdentity(seat),
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

			KingdomSealRecord record = KingdomSealRules.Capture(seat, SampleIdentity(seat),
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

			KingdomSealRecord record = KingdomSealRules.Capture(seat, SampleIdentity(seat),
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
			CollectionAssert.AreEqual(heir.RealmSettlementProvenance,
				parsed.RealmSettlementProvenance);
		}

		[Test]
		public void WholeTopologyProvenanceRejectsReplacementAndWrongRealm()
		{
			const string secondTransaction = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
			Assert.IsTrue(KingdomIdentityRules.TryMintSettlement(ExactRealmId,
				secondTransaction, out string second, out KingdomIdentityFault fault),
				fault.ToString());
			Assert.IsTrue(KingdomSealRules.TryBuildSettlementProvenance(ExactSettlementId,
				KingdomIdentityRules.RulesVersion, KingdomIdentityOrigin.FoundingTransaction,
				FoundingTransaction, 10L, "zone-a", "", out string firstRow));
			Assert.IsTrue(KingdomSealRules.TryBuildSettlementProvenance(second,
				KingdomIdentityRules.RulesVersion, KingdomIdentityOrigin.FoundingTransaction,
				secondTransaction, 20L, "zone-b", "", out string secondRow));
			List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>(ExactSettlementId, firstRow),
				new KeyValuePair<string, string>(second, secondRow)
			};
			pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
			List<string> ids = new List<string> { pairs[0].Key, pairs[1].Key };
			List<string> rows = new List<string> { pairs[0].Value, pairs[1].Value };
			Assert.IsTrue(KingdomSealRules.ExactTopologyProvenance(ExactRealmId, ids, rows));

			const string replacementTransaction = "cccccccccccccccccccccccccccccccc";
			Assert.IsTrue(KingdomIdentityRules.TryMintSettlement(ExactRealmId,
				replacementTransaction, out string replacement, out fault), fault.ToString());
			List<string> replaced = new List<string>(ids);
			replaced[1] = replacement;
			replaced.Sort(StringComparer.Ordinal);
			Assert.IsFalse(KingdomSealRules.ExactTopologyProvenance(ExactRealmId,
				replaced, rows));

			Assert.IsTrue(KingdomIdentityRules.TryMintRealm(secondTransaction,
				out string wrongRealm, out fault), fault.ToString());
			Assert.IsFalse(KingdomSealRules.ExactTopologyProvenance(wrongRealm, ids, rows));
		}

		[Test]
		public void PreIdentitySchemaIsDeliberatelyRejected()
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
			Assert.IsFalse(KingdomSealRecord.TryParse(KingdomSealFormat.Compose(1, schemaOne),
				out migrated, out fault, out detail));
			Assert.IsNull(migrated);
			Assert.AreEqual(KingdomSealFault.UnsupportedSchema, fault);
		}

		[Test]
		public void PreTopologyProvenanceSchemaIsDeliberatelyRejected()
		{
			KingdomSealRecord original = SampleCapturedRecord("old-topology", "old-topology-run",
				"game-old-topology", 0, 1);
			Assert.IsFalse(KingdomSealRecord.TryParse(
				KingdomSealFormat.Compose(3, original.WriteBody()), out KingdomSealRecord parsed,
				out KingdomSealFault fault, out string detail));
			Assert.IsNull(parsed);
			Assert.AreEqual(KingdomSealFault.UnsupportedSchema, fault, detail);
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

	/// <summary>Canonical immutable authority for seal fixtures outside engine-backed tests.</summary>
	internal static class KingdomSealTestIdentity
	{
		private const string TransactionId = "0123456789abcdef0123456789abcdef";

		public static KingdomSealRecord Bind(KingdomSealRecord Record)
		{
			if (Record == null) throw new ArgumentNullException("Record");
			KingdomIdentityFault fault;
			string realmId;
			string settlementId;
			if (!KingdomIdentityRules.TryMintRealm(TransactionId, out realmId, out fault) ||
				!KingdomIdentityRules.TryMintSettlement(realmId, TransactionId,
					out settlementId, out fault))
				throw new InvalidOperationException("Test identity mint failed: " + fault);
			Record.RealmId = realmId;
			Record.SettlementId = settlementId;
			Record.RealmSettlementIds = new List<string> { settlementId };
			Record.RealmIdentityVersion = KingdomIdentityRules.RulesVersion;
			Record.RealmIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
			Record.RealmIdentityTransactionId = TransactionId;
			Record.RealmIdentityLegacyFaction = "";
			Record.RealmIdentityFoundedTick = Record.FoundedTick;
			Record.RealmIdentitySeedHigh = 0UL;
			Record.RealmIdentitySeedLow = 0UL;
			Record.RealmIdentityFirstClaimedZone = Record.GroundZoneId ?? "";
			Record.SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
			Record.SettlementIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
			Record.SettlementIdentityTransactionId = TransactionId;
			Record.SettlementIdentityFoundedTick = Record.FoundedTick;
			Record.SettlementIdentityFirstClaimedZone = Record.GroundZoneId ?? "";
			Record.SettlementIdentityLegacyId = "";
			if (!KingdomSealRules.TryBuildSettlementProvenance(settlementId,
				Record.SettlementIdentityVersion, Record.SettlementIdentityOrigin,
				Record.SettlementIdentityTransactionId, Record.SettlementIdentityFoundedTick,
				Record.SettlementIdentityFirstClaimedZone, Record.SettlementIdentityLegacyId,
				out string provenance))
				throw new InvalidOperationException("Test settlement provenance row failed.");
			Record.RealmSettlementProvenance = new List<string> { provenance };
			return Record;
		}
	}
}
#endif
