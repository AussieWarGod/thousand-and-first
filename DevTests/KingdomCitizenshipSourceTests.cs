#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCitizenshipSourceTests
	{
		[Test]
		public void EnrollmentNeverReplacesBrainOrTemporaryAllegianceState()
		{
			string founding = TestMain.ReadRepositoryText("Core/KingdomFounding.cs");
			string runtime = KingdomCitizenshipLogicalSource.Read();
			StringAssert.DoesNotContain("Brain.Factions =", founding);
			StringAssert.DoesNotContain("Allegiance.Calm =", founding);
			StringAssert.DoesNotContain("Allegiance.Hostile =", founding);
			StringAssert.Contains("Brain.GetBaseAllegiance()", runtime);
			StringAssert.Contains("baseSet[factionId] = receipt.AppliedValue", runtime);
			StringAssert.Contains("baseSet.Remove(receipt.FactionId)", runtime);
			StringAssert.DoesNotContain("SetFactionMembership", runtime);
			StringAssert.DoesNotContain("PartyLeader =", runtime);
			StringAssert.DoesNotContain(".Calm =", runtime);
			StringAssert.DoesNotContain(".Hostile =", runtime);
		}

		[Test]
		public void ClaimingGroundPreservesNativeOwnershipForEveryExistingObject()
		{
			string founding = TestMain.ReadRepositoryText("Core/KingdomFounding.cs");
			string claim = Slice(founding, "internal static bool ClaimZone(",
				"public static bool ZonesAdjacent(");
			StringAssert.Contains("Z.SetZoneProperty(\"faction\", system.KingdomFactionName)", claim);
			StringAssert.Contains("faction.HolyPlaces.Add(Z.ZoneID)", claim);
			StringAssert.DoesNotContain("GetObjects", claim);
			StringAssert.DoesNotContain("OwnedByPlayer", claim);
			StringAssert.DoesNotContain("IsOwned(", claim);
			StringAssert.DoesNotContain("SetFactionMembership", claim);
			StringAssert.DoesNotContain("Brain.", claim);
		}

		[Test]
		public void ReceiptSurvivesReloadAndDeathOwnsOnlyItsExactCleanup()
		{
			string runtime = KingdomCitizenshipLogicalSource.Read();
			StringAssert.Contains("WriteNamedFields(this, typeof(r_KingdomCitizenship))", runtime);
			StringAssert.Contains("ReadNamedFields(this, typeof(r_KingdomCitizenship))", runtime);
			StringAssert.Contains("ID == BeforeDeathRemovalEvent.ID", runtime);
			StringAssert.Contains("KingdomCitizenshipRemovalReason.Death", runtime);
			StringAssert.Contains("Death belongs to the engine", runtime);
			StringAssert.Contains("LegacyPriorUnknown", runtime);
			StringAssert.Contains("native base factions or changed allegiance flags", runtime);
			StringAssert.Contains("KingdomOffices.RecordDeath(ParentObject, E.Killer)", runtime);
			string offices = TestMain.ReadRepositoryText("Experience/KingdomOffices.cs");
			string death = Slice(offices, "public static void RecordDeath(",
				"private static void TagCitizens(");
			Assert.That(death.IndexOf("KingdomResidents.TryMarkDead", StringComparison.Ordinal),
				Is.LessThan(death.IndexOf("if (!Enabled)", StringComparison.Ordinal)));
			Assert.That(death.IndexOf("KingdomCreed.Forget", StringComparison.Ordinal),
				Is.LessThan(death.IndexOf("if (!Enabled)", StringComparison.Ordinal)));
		}

		[Test]
		public void LegacyDeathPartKeepsItsEngineResolvedIdentityAfterFileSplit()
		{
			string source = TestMain.ReadRepositoryText(
				"Experience/r_KingdomCitizenLegacy.cs");
			StringAssert.Contains("namespace XRL.World.Parts", source);
			StringAssert.Contains("[Serializable]", source);
			StringAssert.Contains("public class r_KingdomCitizenLegacy : IPart", source);
			StringAssert.Contains("ID == BeforeDeathRemovalEvent.ID", source);
			StringAssert.Contains("KingdomOffices.RecordDeath(ParentObject, E.Killer)", source);
			StringAssert.DoesNotContain("namespace ThousandAndFirst\n{", source);
		}

		[Test]
		public void LifecycleConsumersUseRealmQualifiedAuthorityAndReversibleRemoval()
		{
			string runtime = KingdomCitizenshipLogicalSource.Read();
			string survey = KingdomSurveyLogicalSource.Read();
			string residents = TestMain.ReadRepositoryText("Simulation/City/KingdomResidents.cs");
			string growth = KingdomGrowthLogicalSource.Read();
			StringAssert.Contains("KingdomCitizenship.BelongsTo(citizenshipSystem, item)", survey);
			StringAssert.Contains("KingdomCitizenship.BelongsTo(System, Body)", residents);
			StringAssert.Contains("KingdomCitizenship.CanRemove(System, Body", residents);
			StringAssert.Contains("KingdomCitizenshipRemovalReason.Accession", residents);
			StringAssert.Contains("KingdomCitizenship.BelongsTo(System, Leaver)", growth);
			StringAssert.Contains("KingdomCitizenshipRemovalReason.Emigration", growth);
			StringAssert.Contains("TryRestoreEmigrationAfterCleanRefusal", growth);
			StringAssert.Contains("receipt.RemovalReason != (int)KingdomCitizenshipRemovalReason.Emigration", runtime);
			StringAssert.Contains("KingdomResidents.TryLocate(System, Citizen", runtime);
		}

		[Test]
		public void LegacyObservationProvesCurrentFactionBeforeClaimingOwnership()
		{
			string runtime = KingdomCitizenshipLogicalSource.Read();
			string observe = Slice(runtime, "public static bool ObserveLegacy(",
				"public static bool CanRemove(");
			int proof = observe.IndexOf("baseSet.TryGetValue(factionId", StringComparison.Ordinal);
			int mint = observe.IndexOf("Citizen.RequirePart<r_KingdomCitizenship>()", StringComparison.Ordinal);
			Assert.That(proof, Is.GreaterThanOrEqualTo(0));
			Assert.That(mint, Is.GreaterThan(proof));
			StringAssert.Contains("cannot prove ownership by this realm faction", observe);
			StringAssert.Contains("PublishUnownedLegacyNotice", observe);
		}

		[Test]
		public void SuccessionPreservesForeignBrainStateExceptVanillaPlayerSlot()
		{
			string source = KingdomSuccessionLogicalSource.Read();
			string prepare = Slice(source, "private static void PrepareSuccessor(",
				"private static bool TryResetPersonalKnowledge");
			StringAssert.Contains("GetBaseAllegiance()", prepare);
			StringAssert.Contains("baseSet[\"Player\"] = 100", prepare);
			StringAssert.DoesNotContain("Brain.Factions", prepare);
			StringAssert.DoesNotContain("Allegiance.Clear", prepare);
			StringAssert.DoesNotContain("PartyLeader =", prepare);
			StringAssert.DoesNotContain("FactionFeelings.Clear", prepare);
			StringAssert.DoesNotContain("RemovePart<GivesRep>", prepare);
		}

		[Test]
		public void NewArrivalPlanHashesTheExactChainAndPreservesConversation()
		{
			string source = KingdomGrowthLogicalSource.Read();
			StringAssert.Contains("ArrivalCitizenshipPlanValue = \"base-slot-v1\"", source);
			StringAssert.Contains("WriteExactAllegianceGraph", source);
			StringAssert.Contains("WriteExactAllyReason", source);
			StringAssert.Contains("WriteCitizenshipReceiptGraph", source);
			StringAssert.Contains("ArrivalAllegianceAcyclic", source);
			string apply = Slice(source, "private static void ApplyArrivalDomain",
				"private static bool ReconcileArrivalClock");
			StringAssert.Contains("KingdomCitizenshipEnrollmentReason.Arrival", apply);
			StringAssert.DoesNotContain("addSimpleConversationToObject", apply);
			StringAssert.Contains("legacy destructive citizenship plan requires visible quarantine",
				apply);
			string freeze = Slice(source, "private static bool PrepareArrivalPersonPlan(",
				"private static bool FreezePersonProperty(");
			StringAssert.Contains("settler.GetIntProperty(\"KingdomCitizen\") != 0", freeze);
			StringAssert.Contains("settler.GetPart<r_KingdomCitizenship>() != null", freeze);
		}

		[Test]
		public void SuccessionReprovesCitizenshipImmediatelyBeforeBodyTransfer()
		{
			string source = KingdomSuccessionLogicalSource.Read();
			int immediateBoundary = source.IndexOf(
				"Re-prove exact reversible citizenship immediately before irreversible body transfer.",
				StringComparison.Ordinal);
			int finalProof = source.IndexOf(
				"KingdomCitizenship.CanRemove(system, heirBody", immediateBoundary,
				StringComparison.Ordinal);
			int transfer = source.IndexOf(
				"KingdomPlayerBodyTransfer forward = SetPlayerBodyAndRebindAll(game, founder,",
				finalProof, StringComparison.Ordinal);
			Assert.That(immediateBoundary, Is.GreaterThanOrEqualTo(0));
			Assert.That(finalProof, Is.GreaterThanOrEqualTo(0));
			Assert.That(finalProof, Is.LessThan(transfer));
			Assert.That(transfer - finalProof, Is.LessThan(1400));
		}

		private static string Slice(string source, string begin, string end)
		{
			int start = source.IndexOf(begin, StringComparison.Ordinal);
			int finish = source.IndexOf(end, start, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
			Assert.That(finish, Is.GreaterThan(start));
			return source.Substring(start, finish - start);
		}
	}
}
#endif
