#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomEmptyCampSealTests
	{
		[TestCase("population=none")]
		[TestCase("population=unresolved")]
		public void RealFoundationAndPopulationRevisionSealWithoutInventingBodies(string population)
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			CollectionAssert.AreEqual(new[] { "human" }, EmptyCampProfileFixture.Current(ledger).BodyKeys);
			EmptyCampProfileFixture.Revise(ledger, record, population);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityProfileRevision source = EmptyCampProfileFixture.Current(ledger);
			CollectionAssert.AreEqual(new[] { "unresolved" }, source.BodyKeys);
			ClassicAssert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(ledger, record.RealmId,
				record, out long revision, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema, record.ProfileSchema);
			ClassicAssert.AreEqual(source.TechnologyBand, record.TechnologyBand);
			CollectionAssert.AreEqual(source.BodyKeys, record.CanonicalBodyKeys);
			ClassicAssert.AreNotSame(source.BodyKeys, record.CanonicalBodyKeys);
			ClassicAssert.IsTrue(KingdomPolityRules.Digest(record.SourceProfileDigest));
			ClassicAssert.IsTrue(KingdomPolityRules.Digest(record.ProfileProvenanceDigest));
			string wire = record.Compose();
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(wire, out KingdomSealRecord parsed,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			ClassicAssert.AreEqual(wire, parsed.Compose());
			ClassicAssert.AreEqual(0, parsed.Population);
			ClassicAssert.IsTrue(KingdomSealProfileCaptureRules.StillMatches(ledger, record.RealmId,
				parsed, revision, out failure), failure);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[TestCase("population=none", false)]
		[TestCase("population=unresolved", false)]
		[TestCase("population=none", true)]
		[TestCase("population=unresolved", true)]
		public void CurrentAndImportedUnresolvedCohortsRefuseWithoutAnyLedgerMutation(string population, bool imported)
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			EmptyCampProfileFixture.Revise(ledger, record, population);
			string polity = record.RealmId;
			if (imported)
			{
				KingdomPolityLegacySnapshot snapshot = EmptyCampProfileFixture.Snapshot();
				ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(snapshot,
					EmptyCampProfileFixture.Current(ledger), out string failure), failure);
				ledger = EmptyCampProfileFixture.Published(record, snapshot);
				KingdomPolityRecord legacy = ledger.Polities.Find(p => p.Source == KingdomPolitySource.ImportedLegacy);
				ClassicAssert.IsNotNull(legacy); polity = legacy.PolityId;
				ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision, 30,
					out KingdomPolityPublicationResult prepared, out failure), failure);
				ClassicAssert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
					prepared.ProjectionId, 31, out _, out failure), failure);
			}
			EmptyCampProfileFixture.RefusesCohort(ledger, polity, record.SettlementId);
		}

		[TestCase("revision")]
		[TestCase("technology")]
		[TestCase("body")]
		[TestCase("realm")]
		public void CapturedUnresolvedSealRefusesSourceOrOwnerDrift(string drift)
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			EmptyCampProfileFixture.Revise(ledger, record, "population=none");
			ClassicAssert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(ledger, record.RealmId,
				record, out long revision, out string failure), failure);
			string wire = record.Compose(), realm = record.RealmId;
			if (drift == "realm") realm = "taf:realm:v1:" + new string('f', 64);
			else
			{
				KingdomPolityProfileFactSet facts = EmptyCampProfileFixture.Facts(ledger, record,
					drift == "body" ? "body=human" : "population=unresolved");
				if (drift == "technology") { facts.TechnologyBand = 7; facts.Facts[2].ValueKey = "band=7"; }
				ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision, facts, out _, out failure), failure);
			}
			byte[] changed = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomSealProfileCaptureRules.StillMatches(ledger, realm, record, revision, out failure));
			ClassicAssert.IsNotEmpty(failure);
			ClassicAssert.AreEqual(wire, record.Compose());
			CollectionAssert.AreEqual(changed, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void ExactRevisionAndCaptureRetryAreByteStable()
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			KingdomPolityProfileFactSet facts = EmptyCampProfileFixture.Facts(ledger, record, "population=none");
			long expected = ledger.Revision;
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, expected, facts, out _, out string failure), failure);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, expected, facts,
				out KingdomPolityPublicationResult retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ClassicAssert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(ledger, record.RealmId, record,
				out long revision, out failure), failure);
			string wire = record.Compose();
			ClassicAssert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(ledger, record.RealmId, record,
				out long repeated, out failure), failure);
			ClassicAssert.AreEqual(revision, repeated); ClassicAssert.AreEqual(wire, record.Compose());
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}
	}

	// Pure fixture: production publication/revision/codec APIs; no engine or strong-body proof.
	internal static class EmptyCampProfileFixture
	{
		internal static KingdomSealRecord Record()
		{
			return KingdomSealTestIdentity.Bind(new KingdomSealRecord
			{
				WriterVersion = "test", EngineVersion = "test", LineageId = "empty-lineage",
				LegacyId = "empty-legacy", OriginGameId = "empty-origin", Revision = 1,
				WrittenTick = 100, FoundedTick = 10, FounderName = "Ari", RealmName = "Water Compact",
				SettlementName = "Empty Camp", GroundZoneId = "JoppaWorld.1.1.1.1.10",
				TerrainBlueprint = "TerrainSaltMarsh", Stage = (int)GrowthStage.Camp, Population = 0,
				Vocation = "holding", Style = "salt stone",
				Vigour = KingdomRules.SealedVigour(GrowthStage.Camp, 0, 0, 0, false)
			});
		}

		internal static KingdomPolityFoundationFacts Foundation(KingdomSealRecord record)
		{
			return new KingdomPolityFoundationFacts
			{
				RealmId = record.RealmId, FactionId = record.RealmId, SettlementId = record.SettlementId,
				DisplayName = record.RealmName, FounderName = record.FounderName, FoundedTick = 10,
				Vocation = record.Vocation, Style = record.Style, Creed = "water covenant",
				Stage = 0, Population = 0, TechnologyBand = 6,
				OriginKeys = new List<string> { "salt-born" }, CultureKeys = new List<string> { "Joppa" },
				SpeciesKeys = new List<string> { "human" }
			};
		}

		internal static KingdomPolityLedger Published(KingdomSealRecord record, KingdomPolityLegacySnapshot legacy = null)
		{
			ClassicAssert.IsTrue(KingdomPolityRules.TryCreate(record.RealmId,
				legacy == null ? KingdomPolityImportPolicy.Off : KingdomPolityImportPolicy.LatestEligible,
				out KingdomPolityLedger ledger, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				Foundation(record), legacy, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure); return ledger;
		}

		internal static KingdomPolityProfileRevision Current(KingdomPolityLedger ledger)
		{
			KingdomPolityRecord polity = ledger.Polities.Find(p => p.Source == KingdomPolitySource.CurrentRealm);
			ClassicAssert.IsNotNull(polity);
			KingdomPolityProfileRevision profile = ledger.Profiles.Find(p => p.ProfileId == polity.ProfileId && p.Revision == polity.ProfileRevision);
			ClassicAssert.IsNotNull(profile); return profile;
		}

		internal static KingdomPolityProfileFactSet Facts(KingdomPolityLedger ledger, KingdomSealRecord record, string population)
		{
			KingdomPolityProfileRevision prior = Current(ledger);
			return new KingdomPolityProfileFactSet
			{
				PolityId = record.RealmId, ProfileId = prior.ProfileId, PreviousRevision = prior.Revision,
				EffectiveTick = prior.EffectiveTick + 10, TechnologyBand = 6,
				Facts = new List<KingdomPolityProfileFact>
				{
					new KingdomPolityProfileFact { FactId = "taf:fact:profile:a-decision", Kind = KingdomPolityProfileFactKind.Decision,
						ValueKey = "gate=water", SourceRef = record.SettlementId },
					new KingdomPolityProfileFact { FactId = "taf:fact:profile:m-population", Kind = KingdomPolityProfileFactKind.Population,
						ValueKey = population, SourceRef = record.SettlementId },
					new KingdomPolityProfileFact { FactId = "taf:fact:profile:z-technology", Kind = KingdomPolityProfileFactKind.Technology,
						ValueKey = "band=6", SourceRef = record.SettlementId }
				}
			};
		}

		internal static void Revise(KingdomPolityLedger ledger, KingdomSealRecord record, string population)
		{
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision, Facts(ledger, record, population),
				out _, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		internal static KingdomPolityLegacySnapshot Snapshot()
		{
			return new KingdomPolityLegacySnapshot
			{
				LegacyToken = "empty-legacy", LineageToken = "empty-lineage", RealmName = "Empty Compact",
				FounderName = "Ari", SettlementName = "Empty Camp", Vocation = "holding", Style = "salt stone"
			};
		}

		internal static void RefusesCohort(KingdomPolityLedger ledger, string polity, string settlement)
		{
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryResolverContract(ledger, polity, KingdomPolityCohortPurpose.Guard,
				out int resolver, out int minimum, out int maximum, out string failure), failure);
			var request = new KingdomPolityCohortPlanRequest
			{
				CohortId = "taf:cohort:empty-camp", Purpose = KingdomPolityCohortPurpose.Guard,
				SourceRef = "taf:event:empty-camp-guard", PolityId = polity, SurfaceRef = settlement,
				MemberCount = 1, MinimumLevel = minimum, MaximumLevel = maximum, RulesVersion = resolver,
				EventStreamId = "taf:stream:empty-camp", EventOrdinal = 1,
				PresentationAuthority = new KingdomPolityPresentationAuthorityProof
				{ OptionKind = KingdomExperienceOptionKind.AmbientUse, EnableEpoch = 1, ReservedTick = 40 }
			};
			byte[] wire = KingdomPolityCodec.EncodeEnvelope(ledger);
			object polities = ledger.Polities, profiles = ledger.Profiles, cohorts = ledger.Cohorts;
			for (int retry = 0; retry < 2; retry++)
			{
				ClassicAssert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request, out _, out failure));
				StringAssert.Contains("no admissible manifested body", failure);
				CollectionAssert.AreEqual(wire, KingdomPolityCodec.EncodeEnvelope(ledger));
				ClassicAssert.AreSame(polities, ledger.Polities); ClassicAssert.AreSame(profiles, ledger.Profiles); ClassicAssert.AreSame(cohorts, ledger.Cohorts);
				ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			}
		}
	}
}
#endif
