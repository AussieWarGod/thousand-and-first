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
	/// <summary>Checked-in seals written by writer code byte-identical to tag v0.3.1 (see
	/// DevTests/Fixtures/SealProfile/README.md). They prove the forward read is an identity:
	/// profile_schema 0 and 1 keep their bytes, digests and meaning under the widened bound.
	/// They do not prove any native save/load path.</summary>
	[TestFixture]
	public sealed class KingdomSealProfileHistoricalFixtureTests
	{
		private const string Folder = "DevTests/Fixtures/SealProfile/";
		private const string SchemaZeroSha =
			"cb7f08c053d06715c19fc7459640dea5c2c0060e9eba1eeed939881c59bcc8a9";
		private const string SchemaOneSha =
			"3df0f48836f656b7ed4bc2d039557afea3c36a62e6f8b9287150bf9cf1a6d6fd";
		private const string PromotedSha =
			"b9a1ca8c5f8eecee6d37fe24c733de89583e0bddfcd9083e6f9bd22c1ae23262";
		private const string ReceiptSha =
			"50e27ae390c743d9586f88536f1fe5d0c68d7448ba653aff7259f29c137a549d";
		private const string SourceDigest =
			"0e1317cd5cb6c714846efaab944e9cdc0b111bdb1498c2d20cdf183bde448632";
		private const string ProvenanceDigest =
			"ae186ed3055c4b5af441e021e458aa5fd4558c90fc2afe42116d752e1705476e";

		private static string Fixture(string name, string expectedSha)
		{
			string text = TestMain.ReadRepositoryText(Folder + name);
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(File.ReadAllBytes(Path.Combine(
					TestMain.RepositoryRoot, (Folder + name).Replace('/', Path.DirectorySeparatorChar))));
				StringBuilder text16 = new StringBuilder(64);
				for (int i = 0; i < hash.Length; i++)
					text16.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				ClassicAssert.AreEqual(expectedSha, text16.ToString(),
					"fixture bytes changed; historical fixtures are never regenerated");
			}
			return text;
		}

		[Test]
		public void HistoricalSchemaOneSealFromTheZeroThreeOneWriterParsesUnchanged()
		{
			string text = Fixture("schema1-0.3.1.seal", SchemaOneSha);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(text, out KingdomSealRecord parsed,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			ClassicAssert.AreEqual(text, parsed.Compose());
			ClassicAssert.AreEqual(KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				parsed.ProfileSchema);
			ClassicAssert.AreEqual(6, parsed.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "human", "snapjaw" }, parsed.CanonicalBodyKeys);
			ClassicAssert.AreEqual(SourceDigest, parsed.SourceProfileDigest);
			ClassicAssert.AreEqual(ProvenanceDigest, parsed.ProfileProvenanceDigest);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.ValidLegacyProfile(Snapshot(parsed)));
		}

		// Mutating the payload text would fail the frame digest first, so the guard mutates
		// record fields and re-composes: only then does the profile bound decide.
		[TestCase("schema-three")]
		[TestCase("unresolved-pool-at-schema-one")]
		public void HistoricalSchemaOneSealRefusesWidenedOrMixedProfiles(string damage)
		{
			string text = Fixture("schema1-0.3.1.seal", SchemaOneSha);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(text, out KingdomSealRecord parsed,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			if (damage == "schema-three")
				parsed.ProfileSchema = KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema + 1;
			else parsed.CanonicalBodyKeys = new List<string> { "unresolved" };
			string damaged = parsed.Compose();
			ClassicAssert.AreNotEqual(text, damaged);
			ClassicAssert.IsFalse(KingdomSealRecord.TryParse(damaged, out KingdomSealRecord _,
				out fault, out detail));
			ClassicAssert.AreEqual(KingdomSealFault.OutOfBounds, fault);
			ClassicAssert.IsNotEmpty(detail);
		}

		[Test]
		public void HistoricalSchemaZeroSealFromTheZeroThreeOneWriterParsesUnchanged()
		{
			string text = Fixture("schema0-0.3.1.seal", SchemaZeroSha);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(text, out KingdomSealRecord parsed,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			ClassicAssert.AreEqual(text, parsed.Compose());
			ClassicAssert.AreEqual(KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,
				parsed.ProfileSchema);
			ClassicAssert.AreEqual(0, parsed.TechnologyBand);
			CollectionAssert.IsEmpty(parsed.CanonicalBodyKeys);
			ClassicAssert.AreEqual("", parsed.SourceProfileDigest);
			ClassicAssert.AreEqual("", parsed.ProfileProvenanceDigest);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.ValidLegacyProfile(Snapshot(parsed)));
		}

		// Real historical bytes reproved by today rules. This is not an independent oracle:
		// byte-identity to v0.3.1 rests on the writer diff cited in the fixture README.
		[Test]
		public void HistoricalFixtureProvenanceReprovesUnderCurrentRules()
		{
			string text = Fixture("schema1-0.3.1.seal", SchemaOneSha);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(text, out KingdomSealRecord parsed,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			ClassicAssert.AreEqual(parsed.ProfileProvenanceDigest,
				KingdomPolityProfileRules.LegacyProfileProvenanceDigest(parsed.ProfileSchema,
					parsed.TechnologyBand, parsed.CanonicalBodyKeys, parsed.SourceProfileDigest));
			KingdomPolityLegacySnapshot snapshot = Snapshot(parsed);
			// A living stage carries no inherited state (-1). The refound reader only ever sees a
			// committed seal, so supply that one field and leave every profile field verbatim.
			snapshot.InheritedState = (int)KingdomRules.InheritedState.Held;
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy("taf:polity:v1:historic",
				snapshot, 500L, out KingdomPolityProfileRevision profile, out string failure), failure);
			ClassicAssert.AreEqual(6, profile.TechnologyBand);
			CollectionAssert.AreEqual(new[] { "human", "snapjaw" }, profile.BodyKeys);
			// Schema 1 keeps its own digest domain: the schema-2 domain must not collide with it.
			KingdomPolityLegacySnapshot unresolved = Snapshot(parsed);
			unresolved.InheritedState = (int)KingdomRules.InheritedState.Held;
			unresolved.ProfileSchema =
				KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema;
			unresolved.CanonicalBodyKeys = new List<string> { "unresolved" };
			unresolved.ProfileProvenanceDigest =
				KingdomPolityProfileRules.LegacyProfileProvenanceDigest(unresolved.ProfileSchema,
					unresolved.TechnologyBand, unresolved.CanonicalBodyKeys,
					unresolved.SourceProfileDigest);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateLegacy("taf:polity:v1:historic",
				unresolved, 500L, out KingdomPolityProfileRevision widened, out failure), failure);
			ClassicAssert.AreNotEqual(profile.FactsDigest, widened.FactsDigest);
			ClassicAssert.AreEqual(6, widened.TechnologyBand);
		}

		[Test]
		public void TransitionCopyOfHistoricalSealKeepsSchemaOneProfile()
		{
			string text = Fixture("schema1-0.3.1.seal", SchemaOneSha);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(text, out KingdomSealRecord parsed,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			string copied = KingdomSealRules.Copy(parsed).Compose();
			StringAssert.StartsWith("taf-seal 6\n", copied);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(copied, out KingdomSealRecord read,
				out fault, out detail), fault + ": " + detail);
			ClassicAssert.AreEqual(KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				read.ProfileSchema);
			ClassicAssert.AreEqual(parsed.TechnologyBand, read.TechnologyBand);
			CollectionAssert.AreEqual(parsed.CanonicalBodyKeys, read.CanonicalBodyKeys);
			ClassicAssert.AreEqual(parsed.SourceProfileDigest, read.SourceProfileDigest);
			ClassicAssert.AreEqual(parsed.ProfileProvenanceDigest, read.ProfileProvenanceDigest);
		}

		[Test]
		public void SavedShapeWithHistoricalLegacyTextValidatesWithoutRepair()
		{
			string legacyText = Fixture("schema1-promoted-0.3.1.seal", PromotedSha);
			string receiptText = Fixture("reserved-receipt-0.3.1.seal", ReceiptSha);
			KingdomInheritanceSavedShape shape = new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)KingdomInheritancePhase.Reserved,
				LegacyText = legacyText, ReceiptText = receiptText
			};
			ClassicAssert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out string failure), failure);
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(legacyText,
				out KingdomSealRecord legacy, out KingdomSealFault fault, out string detail),
				fault + ": " + detail);
			ClassicAssert.AreEqual(legacyText, legacy.Compose());
			ClassicAssert.AreEqual(KingdomSealStatus.Promoted, legacy.Status);
			ClassicAssert.IsTrue(legacy.IsResolved);
			ClassicAssert.AreEqual(KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				legacy.ProfileSchema);
		}

		// Documentation-grade: the outer frame cannot catch a widened nested value. What refuses
		// a schema-2 record on 0.3.1 is the profile bound at Core/KingdomSealRecord.Profile.cs:13,
		// which read [0, CurrentLegacyProfileSchema] at tag v0.3.1.
		[Test]
		public void CommittedUnresolvedSealPassesTheOuterGateButNotTheHistoricalProfileBound()
		{
			KingdomSealRecord record = EmptyCampProfileFixture.Record();
			KingdomPolityLedger ledger = EmptyCampProfileFixture.Published(record);
			EmptyCampProfileFixture.Revise(ledger, record, "population=none");
			ClassicAssert.IsTrue(KingdomSealProfileCaptureRules.TryCapture(ledger, record.RealmId,
				record, out long _, out string failure), failure);
			string text = record.Compose();
			ClassicAssert.IsTrue(KingdomSealFormat.TryParse(text, KingdomSealRecord.FirstSchema,
				KingdomSealRecord.CurrentSchema, out int schema, out KingdomSealBody body,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			ClassicAssert.AreEqual(KingdomSealRecord.CurrentSchema, schema);
			ClassicAssert.AreEqual(
				(long)KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema,
				body.Number("profile_schema", -1L));
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(text, out KingdomSealRecord parsed,
				out fault, out detail), fault + ": " + detail);
			ClassicAssert.AreEqual(text, parsed.Compose());
		}

		// Companion to DevTests/KingdomInheritanceSpatialSourceTests.cs "CurrentSchema = 6":
		// the envelope is unchanged; only the nested profile bound widened.
		[Test]
		public void SealReaderBoundsArePinned()
		{
			string source = KingdomSealRecordLogicalSource.Read();
			StringAssert.Contains("CurrentSchema = 6", source);
			int low = source.IndexOf("KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,",
				StringComparison.Ordinal);
			ClassicAssert.Greater(low, -1);
			int high = source.IndexOf(
				"KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema,",
				low, StringComparison.Ordinal);
			ClassicAssert.Greater(high, low);
			StringAssert.DoesNotContain(
				"KingdomPolityProfileRules.CurrentLegacyProfileSchema,\n\t\t\t\tout Record.ProfileSchema",
				source);
		}

		// Mirrors the refound import reader at
		// World/KingdomInheritanceState.z13.PolityFacts.cs:32-49, which copies the profile
		// fields verbatim out of a committed inheritance seal.
		private static KingdomPolityLegacySnapshot Snapshot(KingdomSealRecord Legacy)
		{
			return new KingdomPolityLegacySnapshot
			{
				ProfileSchema = Legacy.ProfileSchema,
				TechnologyBand = Legacy.TechnologyBand,
				CanonicalBodyKeys = new List<string>(Legacy.CanonicalBodyKeys),
				SourceProfileDigest = Legacy.SourceProfileDigest,
				ProfileProvenanceDigest = Legacy.ProfileProvenanceDigest,
				LegacyToken = Legacy.LegacyId, LineageToken = Legacy.LineageId,
				FounderName = Legacy.FounderName, RealmName = Legacy.RealmName,
				SettlementName = Legacy.SettlementName, Vocation = Legacy.Vocation,
				Style = Legacy.Style, Stage = Legacy.Stage, Population = Legacy.Population,
				Defence = Legacy.Defence, StoredWater = Legacy.StoredWater,
				InheritedState = Legacy.InheritedState,
				RollNames = new List<string>(Legacy.RollNames),
				OriginKeys = new List<string>(Legacy.OriginKeys),
				OriginCounts = new List<int>(Legacy.OriginCounts),
				CreedKeys = new List<string>(Legacy.CreedKeys),
				CreedCounts = new List<int>(Legacy.CreedCounts)
			};
		}
	}
}
#endif
