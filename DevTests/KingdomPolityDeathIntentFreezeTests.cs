#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The ROOT conditions on freeze-at-first-read for migrated v1 death intents: the freeze is
	/// durable and write-once so later replays reuse it and never re-derive; the frozen value
	/// carries frozen-at-read provenance that is permanently distinguishable from frozen-at-death
	/// and can never be restated as a death-time claim; a torn first-read freeze refuses replay.
	/// Also pins the Representative/Ordinal coupling that alone makes the two incident gates
	/// (TryFreeze's <c>Ordinal != 0</c> and ValidIncidentBinding's <c>Representative</c>) agree.
	/// </summary>
	[TestFixture]
	public sealed class KingdomPolityDeathIntentFreezeTests
	{
		private const string ReadPlan = "taf:incident-plan:frozen-at-read";
		private const string ReadIncident = "taf:incident:frozen-at-read";

		[Test]
		public void FirstReadFreezeStampsMigratedProvenanceAndSecondReadReusesIt()
		{
			string legacy = KingdomPolityDeathIntentRules.EncodeV1Fixture(Record());
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(legacy,
				out KingdomPolityDeathIntentRecord first, out string failure), failure);
			Assert.AreEqual(KingdomPolityDeathIntentProvenance.LegacyV1, first.Provenance);
			Assert.AreEqual("", first.IncidentPlanId, "v1 bytes carry no plan id to recover");

			first.IncidentPlanId = ReadPlan; first.IncidentId = ReadIncident;
			first.IncidentDigest = new string('b', 64);
			first.Provenance = KingdomPolityDeathIntentProvenance.FrozenAtFirstRead;
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(first, out string frozen,
				out failure), failure);

			// Second read: the durable bytes already carry the frozen tuple and are no longer
			// LegacyV1, so the migration branch cannot fire again and nothing is re-derived.
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(frozen,
				out KingdomPolityDeathIntentRecord second, out failure), failure);
			Assert.AreEqual(KingdomPolityDeathIntentProvenance.FrozenAtFirstRead,
				second.Provenance);
			Assert.AreNotEqual(KingdomPolityDeathIntentProvenance.LegacyV1, second.Provenance);
			Assert.AreEqual(ReadPlan, second.IncidentPlanId);
			Assert.AreEqual(ReadIncident, second.IncidentId);
			Assert.AreEqual(new string('b', 64), second.IncidentDigest);
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(second, out string repeated,
				out failure), failure);
			Assert.AreEqual(frozen, repeated, "a reused freeze must re-emit byte-identically");

			// The only re-derivation site is gated on LegacyV1 provenance, so a migrated record
			// can never re-enter it.
			StringAssert.Contains(
				"if (decoded && record.Provenance == KingdomPolityDeathIntentProvenance.LegacyV1)",
				TestMain.ReadRepositoryText(
					"Polity/KingdomPolityEndpointRuntime.DeathIntent.cs"));
		}

		[Test]
		public void MigratedProvenanceCanNeverBeRestatedAsADeathTimeClaim()
		{
			KingdomPolityDeathIntentRecord record = Record();
			record.IncidentPlanId = ReadPlan; record.IncidentId = ReadIncident;
			record.IncidentDigest = new string('b', 64);
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(record, out string atDeath,
				out string failure), failure);
			record.Provenance = KingdomPolityDeathIntentProvenance.FrozenAtFirstRead;
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(record, out string atRead,
				out failure), failure);

			StringAssert.StartsWith(KingdomPolityDeathIntentRules.WirePrefix, atDeath);
			StringAssert.StartsWith(KingdomPolityDeathIntentRules.MigratedWirePrefix, atRead);
			StringAssert.DoesNotStartWith(KingdomPolityDeathIntentRules.WirePrefix, atRead);
			Assert.AreNotEqual(atDeath, atRead,
				"the two provenances must never share durable bytes");

			// Same payload, disjoint digest domains: relabelling either form as the other fails
			// its exact digest, so no stored migrated record can be promoted to a death claim.
			string promoted = KingdomPolityDeathIntentRules.WirePrefix + atRead.Substring(
				KingdomPolityDeathIntentRules.MigratedWirePrefix.Length);
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(promoted, out _, out failure));
			StringAssert.Contains("digest", failure);
			string demoted = KingdomPolityDeathIntentRules.MigratedWirePrefix + atDeath.Substring(
				KingdomPolityDeathIntentRules.WirePrefix.Length);
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(demoted, out _, out failure));
			StringAssert.Contains("digest", failure);

			// Negative control: neither untouched form is refused.
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(atDeath, out _, out failure),
				failure);
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(atRead, out _, out failure),
				failure);
		}

		[Test]
		public void TornFirstReadFreezeRefusesReplayInsteadOfRederiving()
		{
			// Only byte-exact new bytes count as applied. Preserved old bytes and anything else
			// are refusals, never a licence to re-derive the freeze.
			Assert.AreEqual(KingdomPolityLegacyRewriteRecovery.Applied,
				KingdomPolityPhysicalCustodyRules.ClassifyLegacyRewriteRecovery(
					true, true, true, true, false));
			Assert.AreEqual(KingdomPolityLegacyRewriteRecovery.OldBytesPreserved,
				KingdomPolityPhysicalCustodyRules.ClassifyLegacyRewriteRecovery(
					true, true, true, false, true));
			Assert.AreEqual(KingdomPolityLegacyRewriteRecovery.Ambiguous,
				KingdomPolityPhysicalCustodyRules.ClassifyLegacyRewriteRecovery(
					true, true, true, false, false));
			Assert.AreEqual(KingdomPolityLegacyRewriteRecovery.Ambiguous,
				KingdomPolityPhysicalCustodyRules.ClassifyLegacyRewriteRecovery(
					false, false, false, false, false));

			string rewrite = TestMain.ReadRepositoryText(
				"Polity/KingdomPolityEndpointRuntime.DeathLegacy.cs");
			// Write-once: the rewrite fires only while the slot still holds exactly the legacy
			// bytes, so a completed freeze can never be overwritten by a second migration.
			StringAssert.Contains("actual != LegacyWire", rewrite);
			StringAssert.Contains("legacy death intent changed before migration", rewrite);
			StringAssert.Contains("legacy death intent migration failed before write", rewrite);
			StringAssert.Contains("legacy death intent migration left ambiguous bytes", rewrite);
			StringAssert.Contains(
				"legacy death intent migration did not install exact current bytes", rewrite);

			// A refused rewrite refuses the whole read, which refuses the replay above it.
			string read = TestMain.ReadRepositoryText(
				"Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			StringAssert.Contains(
				"if (!TryRewriteLegacyDeathIntent(Zone, wire, record, out Failure)) return false;",
				read);
			StringAssert.Contains("out KingdomPolityDeathIntentRecord intent, out Failure)) "
				+ "return false;", read);
		}

		/// <summary>
		/// TryFreeze decides on <c>Ordinal != 0</c> while ValidIncidentBinding decides on
		/// <c>Representative</c>. They agree only because Valid pins one to the other; if that
		/// coupling is ever dropped, a PhysicalOnly or non-representative death could carry (or
		/// be refused for lacking) an incident binding it never froze.
		/// </summary>
		[Test]
		public void RepresentativeIsPinnedToOrdinalZeroSoBothIncidentGatesAgree()
		{
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(Record(), out _,
				out string failure), failure);

			KingdomPolityDeathIntentRecord notRepresentative = Record();
			notRepresentative.Representative = false;
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(notRepresentative, out _,
				out _), "ordinal 0 must always be the representative");

			KingdomPolityDeathIntentRecord laterOrdinal = Record();
			laterOrdinal.Ordinal = 1;
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(laterOrdinal, out _, out _),
				"a non-zero ordinal must never claim to be the representative");

			// Negative control: the same non-zero ordinal encodes once it stops claiming
			// representation and drops the incident binding only a representative may carry.
			KingdomPolityDeathIntentRecord follower = Record();
			follower.Ordinal = 1; follower.Representative = false;
			follower.IncidentPlanId = follower.IncidentId = follower.IncidentDigest = "";
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(follower, out _, out failure),
				failure);
		}

		private static KingdomPolityDeathIntentRecord Record()
		{
			return new KingdomPolityDeathIntentRecord
			{
				Kind = KingdomPolityPhysicalCustodyRules.DeathRemovalKind,
				RealmId = "taf:realm:v1:freeze-wire", CohortId = "taf:cohort:freeze-wire",
				ProjectionId = "taf:projection:freeze-wire", ZoneId = "zone/freeze-wire",
				ObjectId = "taf:object:freeze-wire", Ordinal = 0,
				Purpose = KingdomPolityCohortPurpose.Envoy, Representative = true,
				Tick = 150L, Attribution = KingdomPolityDeathAttribution.PlayerWitnessed,
				Visibility = KingdomPolityDeathVisibility.PlayerVisible,
				IncidentPlanId = "taf:incident-plan:freeze-wire",
				IncidentId = "taf:incident:freeze-wire",
				IncidentDigest = new string('a', 64)
			};
		}
	}
}
#endif
