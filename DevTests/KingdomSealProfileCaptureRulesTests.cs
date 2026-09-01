#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSealProfileCaptureRulesTests
	{
		private const string Realm =
			"taf:realm:v1:5151515151515151515151515151515151515151515151515151515151515151";
		private const string Settlement =
			"taf:settlement:v1:6161616161616161616161616161616161616161616161616161616161616161";

		[Test]
		public void ValidCurrentAuthorityProjectsOnceAndDetectsDrift()
		{
			KingdomPolityLedger ledger = Published(new List<string> { "goatfolk", "human" }, 6);
			KingdomSealRecord record = new KingdomSealRecord();
			Assert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(ledger, Realm, record,
				out long revision, out string failure), failure);
			Assert.AreEqual(ledger.Revision, revision);
			Assert.AreEqual(KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				record.ProfileSchema);
			Assert.AreEqual(6, record.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "goatfolk", "human" },
				record.CanonicalBodyKeys);
			Assert.IsTrue(KingdomPolityRules.Digest(record.SourceProfileDigest));
			Assert.IsTrue(KingdomSealProfileCaptureRules.StillMatches(ledger, Realm,
				record, revision, out failure), failure);

			record.CanonicalBodyKeys[0] = "snapjaw";
			Assert.IsFalse(KingdomSealProfileCaptureRules.StillMatches(ledger, Realm,
				record, revision, out failure));
			StringAssert.Contains("changed during seal capture", failure);
		}

		[Test]
		public void AbsentPrePolityAuthorityIsExplicitlyUnresolved()
		{
			KingdomSealRecord record = new KingdomSealRecord
			{
				ProfileSchema = 1, TechnologyBand = 9,
				CanonicalBodyKeys = new List<string> { "human" },
				SourceProfileDigest = new string('a', 64),
				ProfileProvenanceDigest = new string('b', 64)
			};
			Assert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(null, Realm, record,
				out long revision, out string failure), failure);
			Assert.AreEqual(-1L, revision);
			Assert.AreEqual(0, record.ProfileSchema);
			Assert.AreEqual(0, record.TechnologyBand);
			CollectionAssert.IsEmpty(record.CanonicalBodyKeys);
			Assert.AreEqual("", record.SourceProfileDigest);
			Assert.AreEqual("", record.ProfileProvenanceDigest);
		}

		[Test]
		public void UnresolvedCurrentBodyFailsClosedInsteadOfGuessingFromStage()
		{
			KingdomPolityLedger ledger = Published(new List<string> { "unknown species" }, 8);
			Assert.IsFalse(KingdomSealProfileCaptureRules.TryCapture(ledger, Realm,
				new KingdomSealRecord(), out long _, out string failure));
			StringAssert.Contains("lacks canonical", failure);
		}

		[Test]
		public void SealSourceDigestCommitsPhenotypeWithoutCrossRunIdentity()
		{
			KingdomPolityFoundationFacts first = Foundation(Realm, Settlement, 6);
			KingdomPolityFoundationFacts second = Foundation(
				"taf:realm:v1:7171717171717171717171717171717171717171717171717171717171717171",
				"taf:settlement:v1:8181818181818181818181818181818181818181818181818181818181818181",
				6);
			second.DisplayName = "A Different Realm"; second.FounderName = "Otho";
			second.FoundedTick = 900L; second.OriginKeys[0] = "unrelated-origin";
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(first,
				out KingdomPolityProfileRevision a, out string failure), failure);
			Assert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(second,
				out KingdomPolityProfileRevision b, out failure), failure);
			Assert.AreNotEqual(KingdomPolityRules.ProfileExpressionDigest(a),
				KingdomPolityRules.ProfileExpressionDigest(b));
			Assert.AreEqual(KingdomPolityRules.LegacySealPhenotypeDigest(a),
				KingdomPolityRules.LegacySealPhenotypeDigest(b));
			b.TechnologyBand = 7;
			Assert.AreNotEqual(KingdomPolityRules.LegacySealPhenotypeDigest(a),
				KingdomPolityRules.LegacySealPhenotypeDigest(b));
		}

		private static KingdomPolityLedger Published(List<string> Species, int Technology)
		{
			Assert.IsTrue(KingdomPolityRules.TryCreate(Realm, KingdomPolityImportPolicy.Off,
				out KingdomPolityLedger ledger, out string failure), failure);
			KingdomPolityFoundationFacts facts = new KingdomPolityFoundationFacts
			{
				RealmId = Realm, FactionId = Realm, DisplayName = "The Exact Compact",
				FounderName = "Ari", SettlementId = Settlement, Vocation = "holding",
				Style = "salt stone", Creed = "water covenant", Stage = 5,
				TechnologyBand = Technology, Population = 8, FoundedTick = 40L,
				OriginKeys = new List<string> { "salt-born" },
				CultureKeys = new List<string> { "Joppa" }, SpeciesKeys = Species
			};
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, null, out KingdomPolityPublicationResult _, out failure), failure);
			return ledger;
		}

		private static KingdomPolityFoundationFacts Foundation(string RealmId,
			string SettlementId, int Technology)
		{
			return new KingdomPolityFoundationFacts
			{
				RealmId = RealmId, FactionId = RealmId, DisplayName = "The Exact Compact",
				FounderName = "Ari", SettlementId = SettlementId, Vocation = "holding",
				Style = "salt stone", Creed = "water covenant", Stage = 5,
				TechnologyBand = Technology, Population = 8, FoundedTick = 40L,
				OriginKeys = new List<string> { "salt-born" },
				CultureKeys = new List<string> { "Joppa" },
				SpeciesKeys = new List<string> { "goatfolk", "human" }
			};
		}
	}
}
#endif
