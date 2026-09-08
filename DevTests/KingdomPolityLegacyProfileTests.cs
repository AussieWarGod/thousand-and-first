using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityLegacyProfileTests
	{
		private const string Realm =
			"taf:realm:v1:1111111111111111111111111111111111111111111111111111111111111111";
		private const string Settlement =
			"taf:settlement:v1:2222222222222222222222222222222222222222222222222222222222222222";
		private const string Imported =
			"taf:polity:legacy:v1:3333333333333333333333333333333333333333333333333333333333333333";

		[Test]
		public void CurrentSealCapturesAndRegeneratesExactCanonicalProfile()
		{
			KingdomPolityFoundationFacts facts = Foundation();
			facts.Stage = 5; facts.TechnologyBand = 6;
			facts.OriginKeys = new List<string> { "human", "mechanical" };
			facts.SpeciesKeys = new List<string> { "goatfolk" };
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision source, out string failure), failure);
			KingdomPolityLegacySnapshot seal = OldSnapshot();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(seal, source,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				seal.ProfileSchema);
			ClassicAssert.AreEqual(6, seal.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "goatfolk" }, seal.CanonicalBodyKeys);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.ValidLegacy(seal, out failure), failure);

			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, seal, 30L,
				out KingdomPolityProfileRevision imported, out failure), failure);
			ClassicAssert.AreEqual(6, imported.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "goatfolk" }, imported.BodyKeys);
			CollectionAssert.Contains(imported.GearKeys, "steel-sword");
			StringAssert.DoesNotContain("legacy-profile-unresolved",
				string.Join(",", imported.PracticeTags));

			KingdomPolityLegacySnapshot copy = seal.Copy();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, copy, 30L,
				out KingdomPolityProfileRevision retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRules.ProfileExpressionDigest(imported),
				KingdomPolityRules.ProfileExpressionDigest(retry));
			ClassicAssert.AreEqual(seal.ProfileProvenanceDigest, copy.ProfileProvenanceDigest);
			CollectionAssert.AreEqual(seal.CanonicalBodyKeys, copy.CanonicalBodyKeys);

			ClassicAssert.IsTrue(KingdomPolityRules.TryCreate(Realm,
				KingdomPolityImportPolicy.LatestEligible, out KingdomPolityLedger ledger,
				out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, seal, out KingdomPolityPublicationResult _, out failure), failure);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelope(ledger));
			KingdomPolityRecord importedPolity = null;
			for (int i = 0; i < decoded.Polities.Count; i++)
				if (decoded.Polities[i].Source == KingdomPolitySource.ImportedLegacy)
					importedPolity = decoded.Polities[i];
			ClassicAssert.IsNotNull(importedPolity);
			KingdomPolityProfileRevision decodedProfile = null;
			for (int i = 0; i < decoded.Profiles.Count; i++)
				if (decoded.Profiles[i].ProfileId == importedPolity.ProfileId)
					decodedProfile = decoded.Profiles[i];
			ClassicAssert.IsNotNull(decodedProfile);
			ClassicAssert.AreEqual(6, decodedProfile.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "goatfolk" }, decodedProfile.BodyKeys);
			CollectionAssert.AreEqual(KingdomPolityCodec.EncodeEnvelope(ledger),
				KingdomPolityCodec.EncodeEnvelope(decoded));
		}

		[Test]
		public void OldSchemaRemainsPinnedUnresolvedAndNeverInfersFromOriginOrStage()
		{
			KingdomPolityLegacySnapshot old = OldSnapshot();
			old.Stage = 5; old.Defence = 100000;
			old.OriginKeys = new List<string> { "goatfolk", "mechanical robot" };
			old.OriginCounts = new List<int> { 8, 4 };
			ClassicAssert.IsTrue(KingdomPolityProfileRules.ValidLegacy(old, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, old, 40L,
				out KingdomPolityProfileRevision imported, out failure), failure);
			ClassicAssert.AreEqual(0, imported.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "unresolved" }, imported.BodyKeys);
			CollectionAssert.IsEmpty(imported.GearKeys);
			CollectionAssert.Contains(imported.PracticeTags, "legacy-profile-unresolved");
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(imported, "guard", 0, 1, 4,
				out KingdomPolityNpcSpec _, out failure));
			StringAssert.Contains("no admissible manifested body", failure);
			ClassicAssert.AreEqual(KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,
				old.ProfileSchema);
			ClassicAssert.AreEqual(0, old.TechnologyBand);
			CollectionAssert.IsEmpty(old.CanonicalBodyKeys);
			ClassicAssert.IsNull(old.SourceProfileDigest);
			ClassicAssert.IsNull(old.ProfileProvenanceDigest);
		}

		[Test]
		public void CanonicalProfileCommitmentRejectsTamperAndMixedUnresolvedCapture()
		{
			KingdomPolityFoundationFacts facts = Foundation();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision source, out string failure), failure);
			KingdomPolityLegacySnapshot seal = OldSnapshot();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(seal, source,
				out failure), failure);

			KingdomPolityLegacySnapshot technology = seal.Copy(); technology.TechnologyBand++;
			ClassicAssert.IsFalse(KingdomPolityProfileRules.ValidLegacy(technology, out failure));
			KingdomPolityLegacySnapshot body = seal.Copy(); body.CanonicalBodyKeys[0] = "snapjaw";
			ClassicAssert.IsFalse(KingdomPolityProfileRules.ValidLegacy(body, out failure));
			KingdomPolityLegacySnapshot sourceDigest = seal.Copy();
			sourceDigest.SourceProfileDigest = KingdomPolityTestData.DigestB;
			ClassicAssert.IsFalse(KingdomPolityProfileRules.ValidLegacy(sourceDigest, out failure));
			KingdomPolityLegacySnapshot proof = seal.Copy();
			proof.ProfileProvenanceDigest = KingdomPolityTestData.DigestB;
			ClassicAssert.IsFalse(KingdomPolityProfileRules.ValidLegacy(proof, out failure));
			KingdomPolityLegacySnapshot duplicate = seal.Copy();
			duplicate.CanonicalBodyKeys.Add(duplicate.CanonicalBodyKeys[0]);
			ClassicAssert.IsFalse(KingdomPolityProfileRules.ValidLegacy(duplicate, out failure));

			source.BodyKeys = new List<string> { "human", "unresolved" };
			ClassicAssert.IsFalse(KingdomPolityProfileRules.TryCaptureLegacyProfile(OldSnapshot(), source,
				out failure));
			StringAssert.Contains("lacks canonical", failure);
		}

		[Test]
		public void OriginChangesNarrativeDigestButNeverCanonicalPhenotype()
		{
			KingdomPolityFoundationFacts facts = Foundation();
			facts.SpeciesKeys = new List<string> { "mechanical" };
			facts.TechnologyBand = 8;
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision source, out string failure), failure);
			KingdomPolityLegacySnapshot a = OldSnapshot();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(a, source,
				out failure), failure);
			KingdomPolityLegacySnapshot b = a.Copy();
			b.OriginKeys = new List<string> { "human village" };
			b.OriginCounts = new List<int> { 12 };
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, a, 40L,
				out KingdomPolityProfileRevision first, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, b, 40L,
				out KingdomPolityProfileRevision second, out failure), failure);
			CollectionAssert.AreEqual(new[] { "mechanical" }, first.BodyKeys);
			CollectionAssert.AreEqual(first.BodyKeys, second.BodyKeys);
			ClassicAssert.AreEqual(8, first.TechnologyBand);
			ClassicAssert.AreEqual(first.TechnologyBand, second.TechnologyBand);
			ClassicAssert.AreNotEqual(first.FactsDigest, second.FactsDigest);
		}

		private static KingdomPolityFoundationFacts Foundation()
		{
			return new KingdomPolityFoundationFacts
			{
				RealmId = Realm, FactionId = Realm, DisplayName = "The Water Compact",
				FounderName = "Ari", SettlementId = Settlement, Vocation = "holding",
				Style = "salt dunes", Creed = "the covenant", Stage = 1,
				TechnologyBand = 2, Population = 7, FoundedTick = 30L,
				OriginKeys = new List<string> { "human" },
				CultureKeys = new List<string> { "Joppa" },
				SpeciesKeys = new List<string> { "human" }
			};
		}

		private static KingdomPolityLegacySnapshot OldSnapshot()
		{
			return new KingdomPolityLegacySnapshot
			{
				LegacyToken = "lgc-a-0001", LineageToken = "lin-a-0001",
				FounderName = "Nara", RealmName = "The Returned Brass",
				SettlementName = "Old Ux", Vocation = "foundry", Style = "deep caves",
				Stage = 2, Population = 12, Defence = 5, StoredWater = 100,
				InheritedState = 1, RollNames = new List<string> { "Nara", "Otho" },
				OriginKeys = new List<string> { "goatfolk" }, OriginCounts = new List<int> { 12 },
				CreedKeys = new List<string> { "brass oath" }, CreedCounts = new List<int> { 12 }
			};
		}
	}
}
