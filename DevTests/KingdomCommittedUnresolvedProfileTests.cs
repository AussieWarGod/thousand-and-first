#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCommittedUnresolvedProfileTests
	{
		private const string Imported = "taf:polity:legacy:v1:3333333333333333333333333333333333333333333333333333333333333333";

		[TestCase(-1, false)]
		[TestCase(0, false)]
		[TestCase(1, true)]
		[TestCase(2, true)]
		[TestCase(3, false)]
		[TestCase(int.MaxValue, false)]
		public void CommittedSchemaClassificationIsExact(int schema, bool expected)
		{
			ClassicAssert.AreEqual(2, KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema);
			ClassicAssert.AreEqual(expected, KingdomPolityProfileRules.IsCommittedLegacyProfileSchema(schema));
		}

		[TestCase(null, false)]
		[TestCase("", false)]
		[TestCase("unresolved", true)]
		[TestCase("Unresolved", false)]
		[TestCase("unresolved ", false)]
		[TestCase("unresolved|unresolved", false)]
		[TestCase("human|unresolved", false)]
		[TestCase("unresolved|human", false)]
		[TestCase("unknown", false)]
		[TestCase("unresolved\u0000", false)]
		public void UnresolvedBodyPoolAdmitsOnlyTheExactSingleToken(string body, bool expected)
		{
			IList<string> values = body == null ? null : body.Length == 0 ? new string[0] : body.Split('|');
			ClassicAssert.AreEqual(expected, KingdomPolityProfileRules.IsUnresolvedBodyPool(values));
			ClassicAssert.IsFalse(KingdomPolityProfileRules.IsUnresolvedBodyPool(new string[] { null }));
		}

		[TestCase("schema-negative")]
		[TestCase("schema-zero")]
		[TestCase("schema-one")]
		[TestCase("schema-future")]
		[TestCase("technology-negative")]
		[TestCase("technology-over")]
		[TestCase("body-null")]
		[TestCase("body-empty")]
		[TestCase("body-human")]
		[TestCase("body-duplicate")]
		[TestCase("body-mixed")]
		[TestCase("body-case")]
		[TestCase("body-control")]
		[TestCase("body-surrogate")]
		[TestCase("source-null")]
		[TestCase("source-empty")]
		[TestCase("source-upper")]
		[TestCase("source-short")]
		[TestCase("source-long")]
		[TestCase("source-changed")]
		[TestCase("proof-null")]
		[TestCase("proof-empty")]
		[TestCase("proof-upper")]
		[TestCase("proof-changed")]
		public void MalformedCommittedProfileRefusesWithoutRepair(string damage)
		{
			KingdomPolityLegacySnapshot snapshot = Captured(6);
			switch (damage)
			{
				case "schema-negative": snapshot.ProfileSchema = -1; break;
				case "schema-zero": snapshot.ProfileSchema = 0; break;
				case "schema-one": snapshot.ProfileSchema = 1; break;
				case "schema-future": snapshot.ProfileSchema = 3; break;
				case "technology-negative": snapshot.TechnologyBand = -1; break;
				case "technology-over": snapshot.TechnologyBand = 11; break;
				case "body-null": snapshot.CanonicalBodyKeys = null; break;
				case "body-empty": snapshot.CanonicalBodyKeys.Clear(); break;
				case "body-human": snapshot.CanonicalBodyKeys[0] = "human"; break;
				case "body-duplicate": snapshot.CanonicalBodyKeys.Add("unresolved"); break;
				case "body-mixed": snapshot.CanonicalBodyKeys.Add("human"); break;
				case "body-case": snapshot.CanonicalBodyKeys[0] = "Unresolved"; break;
				case "body-control": snapshot.CanonicalBodyKeys[0] = "unresolved\0"; break;
				case "body-surrogate": snapshot.CanonicalBodyKeys[0] = "\ud800"; break;
				case "source-null": snapshot.SourceProfileDigest = null; break;
				case "source-empty": snapshot.SourceProfileDigest = ""; break;
				case "source-upper": snapshot.SourceProfileDigest = new string('A', 64); break;
				case "source-short": snapshot.SourceProfileDigest = new string('a', 63); break;
				case "source-long": snapshot.SourceProfileDigest = new string('a', 65); break;
				case "source-changed": snapshot.SourceProfileDigest = Other(snapshot.SourceProfileDigest); break;
				case "proof-null": snapshot.ProfileProvenanceDigest = null; break;
				case "proof-empty": snapshot.ProfileProvenanceDigest = ""; break;
				case "proof-upper": snapshot.ProfileProvenanceDigest = new string('B', 64); break;
				case "proof-changed": snapshot.ProfileProvenanceDigest = Other(snapshot.ProfileProvenanceDigest); break;
				default: Assert.Fail("unknown damage"); break;
			}
			// Shape negatives carry recomputed evidence: refusal must not merely be stale checksum.
			if ((damage.StartsWith("schema-", StringComparison.Ordinal) || damage.StartsWith("technology-", StringComparison.Ordinal)
				|| damage.StartsWith("body-", StringComparison.Ordinal)) && damage != "body-null" && damage != "body-surrogate")
				snapshot.ProfileProvenanceDigest = KingdomPolityProfileRules.LegacyProfileProvenanceDigest(
					snapshot.ProfileSchema, snapshot.TechnologyBand, snapshot.CanonicalBodyKeys, snapshot.SourceProfileDigest);
			List<string> bodies = snapshot.CanonicalBodyKeys;
			string[] values = bodies?.ToArray();
			int schema = snapshot.ProfileSchema, technology = snapshot.TechnologyBand;
			string source = snapshot.SourceProfileDigest, proof = snapshot.ProfileProvenanceDigest;
			ClassicAssert.IsFalse(KingdomPolityProfileRules.ValidLegacy(snapshot, out _));
			ClassicAssert.IsFalse(KingdomPolityProfileRules.TryCreateLegacy(Imported, snapshot, 100,
				out KingdomPolityProfileRevision result, out string failure));
			ClassicAssert.IsNull(result); ClassicAssert.IsNotEmpty(failure);
			ClassicAssert.AreSame(bodies, snapshot.CanonicalBodyKeys);
			if (values != null) CollectionAssert.AreEqual(values, snapshot.CanonicalBodyKeys);
			ClassicAssert.AreEqual(schema, snapshot.ProfileSchema); ClassicAssert.AreEqual(technology, snapshot.TechnologyBand);
			ClassicAssert.AreEqual(source, snapshot.SourceProfileDigest); ClassicAssert.AreEqual(proof, snapshot.ProfileProvenanceDigest);
		}

		[TestCase(0)]
		[TestCase(6)]
		[TestCase(10)]
		public void ImportRetainsRealTechnologyButNeverGrantsBodiesOrLoadout(int technology)
		{
			KingdomPolityLegacySnapshot snapshot = Captured(technology);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, snapshot, 100,
				out KingdomPolityProfileRevision imported, out string failure), failure);
			ClassicAssert.AreEqual(technology, imported.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "unresolved" }, imported.BodyKeys);
			CollectionAssert.Contains(imported.PracticeTags, "legacy-profile-unresolved");
			CollectionAssert.IsEmpty(imported.GearKeys); ClassicAssert.AreEqual(0, imported.Loadout.ExpectedValueBudget);
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(imported, "guard", 0, 1, 4, out _, out failure));
			StringAssert.Contains("no admissible manifested body", failure);
			KingdomPolityLegacySnapshot copy = snapshot.Copy();
			ClassicAssert.AreNotSame(snapshot.CanonicalBodyKeys, copy.CanonicalBodyKeys);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, copy, 100,
				out KingdomPolityProfileRevision retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRules.ProfileExpressionDigest(imported), KingdomPolityRules.ProfileExpressionDigest(retry));
		}

		[Test]
		public void CommitmentExcludesPrivateIdentityButDistinguishesActualPhenotypeAndOldAbsence()
		{
			KingdomPolityFoundationFacts a = EmptyCampProfileFixture.Foundation(EmptyCampProfileFixture.Record());
			a.SpeciesKeys.Clear(); a.IdentityKeys.Clear();
			KingdomPolityFoundationFacts b = EmptyCampProfileFixture.Foundation(EmptyCampProfileFixture.Record());
			b.SpeciesKeys.Clear(); b.IdentityKeys.Clear(); b.RealmId = "taf:realm:v1:" + new string('b', 64);
			b.FactionId = b.RealmId; b.SettlementId = "taf:settlement:v1:" + new string('c', 64);
			b.FounderName = "Private Founder"; b.DisplayName = "Private Realm"; b.FoundedTick = 999;
			b.OriginKeys[0] = "private-origin"; b.CultureKeys[0] = "private-culture";
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(a, out KingdomPolityProfileRevision first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(b, out KingdomPolityProfileRevision second, out failure), failure);
			ClassicAssert.AreNotEqual(KingdomPolityRules.ProfileExpressionDigest(first), KingdomPolityRules.ProfileExpressionDigest(second));
			KingdomPolityLegacySnapshot one = EmptyCampProfileFixture.Snapshot(), two = EmptyCampProfileFixture.Snapshot();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(one, first, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(two, second, out failure), failure);
			ClassicAssert.AreEqual(one.SourceProfileDigest, two.SourceProfileDigest);
			ClassicAssert.AreEqual(one.ProfileProvenanceDigest, two.ProfileProvenanceDigest);
			b.TechnologyBand++;
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(b, out second, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(two, second, out failure), failure);
			ClassicAssert.AreNotEqual(one.SourceProfileDigest, two.SourceProfileDigest);
			ClassicAssert.AreNotEqual(one.ProfileProvenanceDigest, two.ProfileProvenanceDigest);
			b.TechnologyBand--; b.SpeciesKeys.Add("human");
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(b, out second, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(two, second, out failure), failure);
			ClassicAssert.AreEqual(1, two.ProfileSchema); ClassicAssert.AreNotEqual(one.SourceProfileDigest, two.SourceProfileDigest);
			KingdomPolityLegacySnapshot old = EmptyCampProfileFixture.Snapshot();
			KingdomPolityLegacySnapshot committedZero = Captured(0);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, old, 100, out first, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, committedZero, 100, out second, out failure), failure);
			ClassicAssert.AreEqual(first.TechnologyBand, second.TechnologyBand);
			CollectionAssert.AreEqual(first.BodyKeys, second.BodyKeys);
			ClassicAssert.AreNotEqual(first.FactsDigest, second.FactsDigest);
			ClassicAssert.AreNotEqual(first.ProfileId, second.ProfileId);
		}

		[TestCase("body")]
		[TestCase("technology")]
		[TestCase("source")]
		[TestCase("proof")]
		public void FramedSealRejectsTornSchemaTwoProfile(string damage)
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			EmptyCampProfileFixture.Revise(ledger, record, "population=none");
			ClassicAssert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(ledger, record.RealmId, record, out _, out string failure), failure);
			if (damage == "body") record.CanonicalBodyKeys[0] = "human";
			else if (damage == "technology") record.TechnologyBand++;
			else if (damage == "source") record.SourceProfileDigest = Other(record.SourceProfileDigest);
			else record.ProfileProvenanceDigest = Other(record.ProfileProvenanceDigest);
			ClassicAssert.IsFalse(KingdomSealRecord.TryParse(record.Compose(), out KingdomSealRecord parsed,
				out KingdomSealFault fault, out _));
			ClassicAssert.IsNull(parsed); ClassicAssert.AreEqual(KingdomSealFault.OutOfBounds, fault);
		}

		[TestCase(0)]
		[TestCase(1)]
		public void HistoricalSchemasKeepTheirOriginalDigestDomainsAndFieldOrder(int schema)
		{
			ClassicAssert.AreEqual(0, KingdomPolityProfileRules.UnresolvedLegacyProfileSchema);
			ClassicAssert.AreEqual(1, KingdomPolityProfileRules.CurrentLegacyProfileSchema);
			KingdomPolityLegacySnapshot snapshot = EmptyCampProfileFixture.Snapshot();
			snapshot.RollNames.Add("Nara"); snapshot.OriginKeys.Add("salt"); snapshot.OriginCounts.Add(2);
			snapshot.CreedKeys.Add("oath"); snapshot.CreedCounts.Add(1);
			var values = new List<string> { "empty-legacy", "empty-lineage", "Nara", "oath=1", "salt=2",
				"Empty Compact", "Empty Camp", "holding", "salt stone", "0", "0", "0", "0", "0" };
			if (schema == 1)
			{
				KingdomPolityFoundationFacts facts = EmptyCampProfileFixture.Foundation(EmptyCampProfileFixture.Record());
				ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts, out KingdomPolityProfileRevision source, out string reason), reason);
				ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(snapshot, source, out reason), reason);
				string phenotype = HistoricalDigest("polity-profile-seal-phenotype-v1",
					new[] { "schema=1", "rules=3", "technology=6", "bodies#1", "human" });
				string provenance = HistoricalDigest("polity-legacy-profile-provenance-v1",
					new[] { "1", "6", phenotype, "body=human" });
				ClassicAssert.AreEqual(phenotype, snapshot.SourceProfileDigest);
				ClassicAssert.AreEqual(provenance, snapshot.ProfileProvenanceDigest);
				values.Add("profile=" + provenance); values.Add("technology=6"); values.Add("body=human");
			}
			string expected = HistoricalDigest(schema == 0 ? "polity-profile-legacy-v1" : "polity-profile-legacy-v2", values);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy(Imported, snapshot, 100,
				out KingdomPolityProfileRevision imported, out string failure), failure);
			ClassicAssert.AreEqual(expected, imported.FactsDigest);
			ClassicAssert.AreEqual("taf:polity-profile:v1:" + HistoricalDigest("polity-profile-id-v1",
				new[] { Imported, expected }), imported.ProfileId);
			ClassicAssert.AreEqual(schema, snapshot.ProfileSchema);
		}

		[TestCase("null")]
		[TestCase("old-rules")]
		[TestCase("empty")]
		[TestCase("mixed")]
		public void RejectedCaptureLeavesPreviousCommittedTargetUntouched(string damage)
		{
			KingdomPolityLegacySnapshot target = Captured(6);
			KingdomPolityFoundationFacts facts = EmptyCampProfileFixture.Foundation(EmptyCampProfileFixture.Record());
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts, out KingdomPolityProfileRevision source, out string failure), failure);
			if (damage == "null") source = null;
			else if (damage == "old-rules") source.RulesVersion = 1;
			else if (damage == "empty") source.BodyKeys.Clear();
			else source.BodyKeys.Add("unresolved");
			List<string> bodies = target.CanonicalBodyKeys; string proof = target.ProfileProvenanceDigest, digest = target.SourceProfileDigest;
			ClassicAssert.IsFalse(KingdomPolityProfileRules.TryCaptureLegacyProfile(target, source, out failure));
			ClassicAssert.AreSame(bodies, target.CanonicalBodyKeys); CollectionAssert.AreEqual(new[] { "unresolved" }, bodies);
			ClassicAssert.AreEqual(2, target.ProfileSchema); ClassicAssert.AreEqual(6, target.TechnologyBand);
			ClassicAssert.AreEqual(proof, target.ProfileProvenanceDigest); ClassicAssert.AreEqual(digest, target.SourceProfileDigest);
		}

		// Independent historical framing oracle; no production hash/profile helper computes expected bytes.
		private static string HistoricalDigest(string domain, IList<string> values)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
			{
				byte[] text = Encoding.UTF8.GetBytes(domain); writer.Write(text.Length); writer.Write(text);
				writer.Write(values.Count);
				foreach (string value in values) { text = Encoding.UTF8.GetBytes(value); writer.Write(text.Length); writer.Write(text); }
				writer.Flush(); using (SHA256 hash = SHA256.Create())
				{
					var result = new StringBuilder();
					foreach (byte b in hash.ComputeHash(stream.ToArray())) result.Append(b.ToString("x2", CultureInfo.InvariantCulture));
					return result.ToString();
				}
			}
		}

		private static KingdomPolityLegacySnapshot Captured(int technology)
		{
			KingdomPolityFoundationFacts facts = EmptyCampProfileFixture.Foundation(EmptyCampProfileFixture.Record());
			facts.SpeciesKeys.Clear(); facts.IdentityKeys.Clear(); facts.TechnologyBand = technology;
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts, out KingdomPolityProfileRevision source, out string failure), failure);
			KingdomPolityLegacySnapshot snapshot = EmptyCampProfileFixture.Snapshot();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(snapshot, source, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.ValidLegacy(snapshot, out failure), failure); return snapshot;
		}

		private static string Other(string digest) { return digest == new string('a', 64) ? new string('b', 64) : new string('a', 64); }
	}
}
#endif
