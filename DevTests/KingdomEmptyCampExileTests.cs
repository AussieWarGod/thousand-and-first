#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomEmptyCampExileTests
	{
		private const string NextRealm = "taf:realm:v1:3333333333333333333333333333333333333333333333333333333333333333";
		private const string NextSettlement = "taf:settlement:v1:4444444444444444444444444444444444444444444444444444444444444444";

		// "body=human" is a canonical-body realm: it proves the revision-aware exile basis is
		// not an empty-camp special case. On 0.3.1 every realm at profile revision >= 2 was
		// refused, because the check recomputed the receipt from the latest profile revision.
		[TestCase("population=none")]
		[TestCase("population=unresolved")]
		[TestCase("body=human")]
		public void RevisedEmptyCampExileDetachesAndReturnsTheExactOriginalLedger(string population)
		{
			bool canonical = population == "body=human";
			int schema = canonical ? KingdomPolityProfileRules.CurrentLegacyProfileSchema :
				KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema;
			string[] bodies = canonical ? new[] { "human" } : new[] { "unresolved" };
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = Revised(record, population);
			ClassicAssert.IsTrue(KingdomPolityRules.TryObserveCurrentFoundation(ledger, record.RealmId,
				record.RealmId, out string failure), failure);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityRealmExileFacts facts = Capture(ledger, record, schema);
			string profileDigest = facts.Legacy.SourceProfileDigest;
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger, facts, before);
			ClassicAssert.AreEqual(schema, transition.Legacy.ProfileSchema);
			ClassicAssert.AreEqual(6, transition.Legacy.TechnologyBand);
			CollectionAssert.AreEqual(bodies, transition.Legacy.CanonicalBodyKeys);
			ClassicAssert.AreNotSame(facts.Legacy, transition.Legacy);
			ClassicAssert.AreNotSame(facts.Legacy.CanonicalBodyKeys, transition.Legacy.CanonicalBodyKeys);
			facts.Legacy.CanonicalBodyKeys[0] = canonical ? "snapjaw" : "human";
			ClassicAssert.AreEqual(profileDigest, transition.Legacy.SourceProfileDigest);
			CollectionAssert.AreEqual(bodies, transition.Legacy.CanonicalBodyKeys);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, ledger.Revision, transition,
				transition.Revision, record.RealmId, out KingdomPolityPublicationResult restored, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, restored.Outcome);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Restored, transition.Phase);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, 0, transition, 0,
				record.RealmId, out KingdomPolityPublicationResult retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityRules.TryCompleteRealmReturn(ledger, transition, transition.Revision,
				record.RealmId, out _, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.None, transition.Phase);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityRules.TryCompleteRealmReturn(ledger, transition, transition.Revision,
				record.RealmId, out retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[TestCase("population=none")]
		[TestCase("population=unresolved")]
		public void RefoundImportsCommittedUnresolvedTechnologyIntoFreshAuthority(string population)
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = Revised(record, population);
			string oldProfile = EmptyCampProfileFixture.Current(ledger).ProfileId;
			KingdomPolityRealmExileFacts facts = Capture(ledger, record);
			string sourceDigest = facts.Legacy.SourceProfileDigest, proof = facts.Legacy.ProfileProvenanceDigest;
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger, facts, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityRules.TryBindIdentity(ledger, NextRealm,
				KingdomPolityImportPolicy.LatestEligible, out string failure), failure);
			KingdomPolityFoundationFacts next = EmptyCampProfileFixture.Foundation(record);
			next.RealmId = NextRealm; next.FactionId = NextRealm; next.SettlementId = NextSettlement; next.FoundedTick = 90;
			ClassicAssert.IsTrue(KingdomPolityRules.TryGetRealmTransitionLegacy(transition,
				out KingdomPolityLegacySnapshot legacy, out failure), failure);
			ClassicAssert.AreNotSame(transition.Legacy, legacy);
			ClassicAssert.AreNotSame(transition.Legacy.CanonicalBodyKeys, legacy.CanonicalBodyKeys);
			ClassicAssert.AreEqual(sourceDigest, legacy.SourceProfileDigest); ClassicAssert.AreEqual(proof, legacy.ProfileProvenanceDigest);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision, next, legacy,
				out KingdomPolityPublicationResult published, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision, 91,
				out KingdomPolityPublicationResult prepared, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 92, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition, transition.Revision,
				out KingdomPolityPublicationResult rebound, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Rebound, transition.Phase);
			ClassicAssert.IsNull(transition.ReturnLedgerEnvelope);
			ClassicAssert.AreEqual(NextRealm, ledger.RealmId); ClassicAssert.AreEqual(NextRealm, transition.ReboundRealmId);
			ClassicAssert.AreEqual(published.ImportedPolityId, rebound.ImportedPolityId);
			ClassicAssert.AreNotEqual(record.RealmId, transition.ReboundFactionId);
			KingdomPolityRecord imported = ledger.Polities.Find(p => p.Source == KingdomPolitySource.ImportedLegacy);
			ClassicAssert.IsNotNull(imported); ClassicAssert.AreNotEqual(record.RealmId, imported.PolityId);
			KingdomPolityProfileRevision profile = ledger.Profiles.Find(p => p.ProfileId == imported.ProfileId && p.Revision == imported.ProfileRevision);
			ClassicAssert.IsNotNull(profile); ClassicAssert.AreNotEqual(oldProfile, profile.ProfileId);
			ClassicAssert.AreEqual(6, profile.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "unresolved" }, profile.BodyKeys);
			CollectionAssert.IsEmpty(profile.GearKeys); ClassicAssert.AreEqual(0, profile.Loadout.ExpectedValueBudget);
			ClassicAssert.IsFalse(ledger.Polities.Exists(p => p.PolityId == record.RealmId));
			ClassicAssert.IsFalse(ledger.Profiles.Exists(p => p.ProfileId == oldProfile));
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition, out failure), failure);
			byte[] after = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition, 0,
				out KingdomPolityPublicationResult retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			CollectionAssert.AreEqual(after, KingdomPolityCodec.EncodeEnvelope(ledger));
			CollectionAssert.AreEqual(after, KingdomPolityCodec.EncodeEnvelope(KingdomPolityCodec.DecodeEnvelope(after)));
			EmptyCampProfileFixture.RefusesCohort(ledger, imported.PolityId, NextSettlement);
		}

		[TestCase("foreign-realm")]
		[TestCase("foreign-source")]
		[TestCase("torn-proof")]
		[TestCase("schema-zero")]
		public void NewExileCannotReplaceMissingOrForeignCurrentCommitment(string damage)
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = Revised(record, "population=none");
			KingdomPolityRealmExileFacts facts = Capture(ledger, record);
			if (damage == "foreign-realm") { facts.RealmId = NextRealm; facts.FactionId = NextRealm; }
			else if (damage == "foreign-source")
			{
				facts.Legacy.SourceProfileDigest = new string('a', 64);
				facts.Legacy.ProfileProvenanceDigest = KingdomPolityProfileRules.LegacyProfileProvenanceDigest(
					facts.Legacy.ProfileSchema, facts.Legacy.TechnologyBand, facts.Legacy.CanonicalBodyKeys, facts.Legacy.SourceProfileDigest);
				ClassicAssert.IsTrue(KingdomPolityProfileRules.ValidLegacy(facts.Legacy, out string valid), valid);
			}
			else if (damage == "torn-proof") facts.Legacy.ProfileProvenanceDigest = new string('b', 64);
			else
			{
				facts.Legacy.ProfileSchema = 0; facts.Legacy.TechnologyBand = 0; facts.Legacy.CanonicalBodyKeys.Clear();
				facts.Legacy.SourceProfileDigest = null; facts.Legacy.ProfileProvenanceDigest = null;
				ClassicAssert.IsTrue(KingdomPolityProfileRules.ValidLegacy(facts.Legacy, out string valid), valid);
			}
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			object profiles = ledger.Profiles, polities = ledger.Polities, projections = ledger.Projections;
			ClassicAssert.IsFalse(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision, facts,
				out KingdomPolityRealmTransition transition, out KingdomPolityPublicationResult refused, out string failure));
			ClassicAssert.IsNull(transition); ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			ClassicAssert.IsNotEmpty(failure);
			if (damage == "foreign-source" || damage == "schema-zero")
				StringAssert.Contains("lacks exact current profile provenance", failure);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.AreSame(profiles, ledger.Profiles); ClassicAssert.AreSame(polities, ledger.Polities); ClassicAssert.AreSame(projections, ledger.Projections);
		}

		// The current realm faction receipt is cut once at foundation and never re-cut on a
		// profile revision, which is why exile must prove the revision-1 profile.
		[Test]
		public void ProfileRevisionNeverReCutsTheCurrentRealmFoundationReceipt()
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			KingdomPolityProjectionReceipt receipt = FactionReceipt(ledger, record.RealmId);
			string projectionId = receipt.ProjectionId, applied = receipt.AppliedDigest;
			long preparedTick = receipt.PreparedTick, committedTick = receipt.CommittedTick;
			KingdomPolityProfileRevision root = EmptyCampProfileFixture.Current(ledger);
			ClassicAssert.AreEqual(1, root.Revision);
			string rootExpression = KingdomPolityRules.ProfileExpressionDigest(root);
			EmptyCampProfileFixture.Revise(ledger, record, "population=none");
			KingdomPolityProfileRevision current = EmptyCampProfileFixture.Current(ledger);
			ClassicAssert.AreEqual(2, current.Revision);
			ClassicAssert.AreNotEqual(rootExpression,
				KingdomPolityRules.ProfileExpressionDigest(current));
			KingdomPolityProjectionReceipt after = FactionReceipt(ledger, record.RealmId);
			// The ledger rebuilds its projection list on revision; the receipt is copied, never
			// re-cut, so every committed field must survive byte-for-byte.
			ClassicAssert.AreEqual(projectionId, after.ProjectionId);
			ClassicAssert.AreEqual(applied, after.AppliedDigest);
			ClassicAssert.AreEqual(preparedTick, after.PreparedTick);
			ClassicAssert.AreEqual(committedTick, after.CommittedTick);
			ClassicAssert.AreEqual(KingdomPolityProjectionPhase.Committed, after.Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryObserveCurrentFoundation(ledger,
				record.RealmId, record.RealmId, out string failure), failure);
		}

		private static KingdomPolityProjectionReceipt FactionReceipt(
			KingdomPolityLedger ledger, string realmId)
		{
			KingdomPolityProjectionReceipt receipt = ledger.Projections.Find(
				p => p.Kind == KingdomPolityProjectionKind.Faction && p.SourceRef == realmId);
			ClassicAssert.IsNotNull(receipt); return receipt;
		}

		private static KingdomPolityLedger Revised(KingdomSealRecord record, string population)
		{
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			EmptyCampProfileFixture.Revise(ledger, record, population);
			ClassicAssert.AreEqual(2, EmptyCampProfileFixture.Current(ledger).Revision); return ledger;
		}

		private static KingdomPolityRealmExileFacts Capture(KingdomPolityLedger ledger, KingdomSealRecord record)
		{
			return Capture(ledger, record, KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema);
		}

		private static KingdomPolityRealmExileFacts Capture(KingdomPolityLedger ledger, KingdomSealRecord record, int schema)
		{
			KingdomPolityLegacySnapshot legacy = EmptyCampProfileFixture.Snapshot();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(legacy,
				EmptyCampProfileFixture.Current(ledger), out string failure), failure);
			ClassicAssert.AreEqual(schema, legacy.ProfileSchema); ClassicAssert.AreEqual(6, legacy.TechnologyBand);
			return new KingdomPolityRealmExileFacts { RealmId = record.RealmId, FactionId = record.RealmId, ClosedTick = 80, Legacy = legacy };
		}

		// Pure durable phases only: these calls do not prove native faction tombstoning or save/load.
		private static KingdomPolityRealmTransition PrepareAndDetach(KingdomPolityLedger ledger,
			KingdomPolityRealmExileFacts facts, byte[] before)
		{
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision, facts,
				out KingdomPolityRealmTransition transition, out _, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Prepared, transition.Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition, out failure), failure);
			CollectionAssert.AreEqual(before, transition.ReturnLedgerEnvelope);
			ClassicAssert.IsTrue(KingdomPolityRules.TryMarkRealmExileTombstoned(ledger, ledger.Revision,
				transition, transition.Revision, out _, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Tombstoned, transition.Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition, out failure), failure);
			CollectionAssert.AreEqual(before, transition.ReturnLedgerEnvelope);
			ClassicAssert.IsTrue(KingdomPolityRules.TryDetachRealmExile(ledger, ledger.Revision,
				transition, transition.Revision, out _, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Detached, transition.Phase);
			ClassicAssert.IsFalse(ledger.IdentityBound); ClassicAssert.IsEmpty(ledger.Polities); ClassicAssert.IsEmpty(ledger.Profiles);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition, out failure), failure);
			CollectionAssert.AreEqual(before, transition.ReturnLedgerEnvelope); return transition;
		}
	}
}
#endif
