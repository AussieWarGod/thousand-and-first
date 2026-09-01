using System.Collections.Generic;
using NUnit.Framework;

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
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision source, out string failure), failure);
			KingdomPolityLegacySnapshot seal = OldSnapshot();
			Assert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(seal, source,
				out failure), failure);
			Assert.AreEqual(KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				seal.ProfileSchema);
			Assert.AreEqual(6, seal.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "goatfolk" }, seal.CanonicalBodyKeys);
			Assert.IsTrue(KingdomPolityProfileRules.ValidLegacy(seal, out failure), failure);

			Assert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, seal, 30L,
				out KingdomPolityProfileRevision imported, out failure), failure);
			Assert.AreEqual(6, imported.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "goatfolk" }, imported.BodyKeys);
			CollectionAssert.Contains(imported.GearKeys, "steel-sword");
			StringAssert.DoesNotContain("legacy-profile-unresolved",
				string.Join(",", imported.PracticeTags));

			KingdomPolityLegacySnapshot copy = seal.Copy();
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, copy, 30L,
				out KingdomPolityProfileRevision retry, out failure), failure);
			Assert.AreEqual(KingdomPolityRules.ProfileExpressionDigest(imported),
				KingdomPolityRules.ProfileExpressionDigest(retry));
			Assert.AreEqual(seal.ProfileProvenanceDigest, copy.ProfileProvenanceDigest);
			CollectionAssert.AreEqual(seal.CanonicalBodyKeys, copy.CanonicalBodyKeys);

			Assert.IsTrue(KingdomPolityRules.TryCreate(Realm,
				KingdomPolityImportPolicy.LatestEligible, out KingdomPolityLedger ledger,
				out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, seal, out KingdomPolityPublicationResult _, out failure), failure);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelope(ledger));
			KingdomPolityRecord importedPolity = null;
			for (int i = 0; i < decoded.Polities.Count; i++)
				if (decoded.Polities[i].Source == KingdomPolitySource.ImportedLegacy)
					importedPolity = decoded.Polities[i];
			Assert.IsNotNull(importedPolity);
			KingdomPolityProfileRevision decodedProfile = null;
			for (int i = 0; i < decoded.Profiles.Count; i++)
				if (decoded.Profiles[i].ProfileId == importedPolity.ProfileId)
					decodedProfile = decoded.Profiles[i];
			Assert.IsNotNull(decodedProfile);
			Assert.AreEqual(6, decodedProfile.TechnologyBand);
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
			Assert.IsTrue(KingdomPolityProfileRules.ValidLegacy(old, out string failure), failure);
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, old, 40L,
				out KingdomPolityProfileRevision imported, out failure), failure);
			Assert.AreEqual(0, imported.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "unresolved" }, imported.BodyKeys);
			CollectionAssert.IsEmpty(imported.GearKeys);
			CollectionAssert.Contains(imported.PracticeTags, "legacy-profile-unresolved");
			Assert.IsFalse(KingdomPolityNpcRules.TryResolve(imported, "guard", 0, 1, 4,
				out KingdomPolityNpcSpec _, out failure));
			StringAssert.Contains("no admissible manifested body", failure);
			Assert.AreEqual(KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,
				old.ProfileSchema);
			Assert.AreEqual(0, old.TechnologyBand);
			CollectionAssert.IsEmpty(old.CanonicalBodyKeys);
			Assert.IsNull(old.SourceProfileDigest);
			Assert.IsNull(old.ProfileProvenanceDigest);
		}

		[Test]
		public void CanonicalProfileCommitmentRejectsTamperAndUnresolvedCapture()
		{
			KingdomPolityFoundationFacts facts = Foundation();
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision source, out string failure), failure);
			KingdomPolityLegacySnapshot seal = OldSnapshot();
			Assert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(seal, source,
				out failure), failure);

			KingdomPolityLegacySnapshot technology = seal.Copy(); technology.TechnologyBand++;
			Assert.IsFalse(KingdomPolityProfileRules.ValidLegacy(technology, out failure));
			KingdomPolityLegacySnapshot body = seal.Copy(); body.CanonicalBodyKeys[0] = "snapjaw";
			Assert.IsFalse(KingdomPolityProfileRules.ValidLegacy(body, out failure));
			KingdomPolityLegacySnapshot sourceDigest = seal.Copy();
			sourceDigest.SourceProfileDigest = KingdomPolityTestData.DigestB;
			Assert.IsFalse(KingdomPolityProfileRules.ValidLegacy(sourceDigest, out failure));
			KingdomPolityLegacySnapshot proof = seal.Copy();
			proof.ProfileProvenanceDigest = KingdomPolityTestData.DigestB;
			Assert.IsFalse(KingdomPolityProfileRules.ValidLegacy(proof, out failure));
			KingdomPolityLegacySnapshot duplicate = seal.Copy();
			duplicate.CanonicalBodyKeys.Add(duplicate.CanonicalBodyKeys[0]);
			Assert.IsFalse(KingdomPolityProfileRules.ValidLegacy(duplicate, out failure));

			source.BodyKeys = new List<string> { "unresolved" };
			Assert.IsFalse(KingdomPolityProfileRules.TryCaptureLegacyProfile(OldSnapshot(), source,
				out failure));
			StringAssert.Contains("lacks canonical", failure);
		}

		[Test]
		public void OriginChangesNarrativeDigestButNeverCanonicalPhenotype()
		{
			KingdomPolityFoundationFacts facts = Foundation();
			facts.SpeciesKeys = new List<string> { "mechanical" };
			facts.TechnologyBand = 8;
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision source, out string failure), failure);
			KingdomPolityLegacySnapshot a = OldSnapshot();
			Assert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(a, source,
				out failure), failure);
			KingdomPolityLegacySnapshot b = a.Copy();
			b.OriginKeys = new List<string> { "human village" };
			b.OriginCounts = new List<int> { 12 };
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, a, 40L,
				out KingdomPolityProfileRevision first, out failure), failure);
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, b, 40L,
				out KingdomPolityProfileRevision second, out failure), failure);
			CollectionAssert.AreEqual(new[] { "mechanical" }, first.BodyKeys);
			CollectionAssert.AreEqual(first.BodyKeys, second.BodyKeys);
			Assert.AreEqual(8, first.TechnologyBand);
			Assert.AreEqual(first.TechnologyBand, second.TechnologyBand);
			Assert.AreNotEqual(first.FactsDigest, second.FactsDigest);
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
