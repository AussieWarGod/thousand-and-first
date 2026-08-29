#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomDirectionalStandingTransactionSourceTests
	{
		[Test]
		public void BatchBuildsCanonicalPrivatePairRootsBeforeOnePublication()
		{
			string source = TestMain.ReadRepositoryText(
				"Core/KingdomSystem.z22a.DirectionalStandings.cs");
			string method = Slice(source, "public bool TryAdjustRegardForRealmBatch(",
				"internal bool TryGetRegardPair(");
			int copy = method.IndexOf("TryCopyRegardLedgers(", StringComparison.Ordinal);
			int arithmetic = method.IndexOf("KingdomStandingRules.TryAdjustPair(",
				StringComparison.Ordinal);
			int publication = method.IndexOf("TryPublishRegardLedgers(",
				StringComparison.Ordinal);
			Assert.That(copy, Is.GreaterThanOrEqualTo(0));
			Assert.That(arithmetic, Is.GreaterThan(copy));
			Assert.That(publication, Is.GreaterThan(arithmetic));
			StringAssert.Contains("!seen.Add(faction)", method);
			StringAssert.Contains("!CanOwnRelationship(faction)", method);
			StringAssert.Contains("nextStandings[faction] = after", method);
			StringAssert.Contains("nextRemainders[faction] = afterCarry", method);
			StringAssert.DoesNotContain("\tStandings[faction] =", method);
			StringAssert.DoesNotContain("\tRegardSpilloverRemainders[faction] =", method);
			StringAssert.Contains("catch (Exception ex)", method);
		}

		[Test]
		public void AbsoluteSetClearsCarryInsideSamePairRootPublication()
		{
			string source = TestMain.ReadRepositoryText(
				"Core/KingdomSystem.z22a.DirectionalStandings.cs");
			string method = Slice(source, "public bool TrySetRegardForRealm(",
				"/// <summary>Validates every edge");
			int standing = method.IndexOf("nextStandings[factionName] = value;",
				StringComparison.Ordinal);
			int carry = method.IndexOf("nextRemainders.Remove(factionName);",
				StringComparison.Ordinal);
			int publish = method.IndexOf("TryPublishRegardLedgers(",
				StringComparison.Ordinal);
			Assert.That(standing, Is.GreaterThanOrEqualTo(0));
			Assert.That(carry, Is.GreaterThan(standing));
			Assert.That(publish, Is.GreaterThan(carry));
		}

		[Test]
		public void AnnexeSnapshotsAndCompensatesEveryCoreEffect()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomAnnexe.Enrollment.cs");
			StringAssert.Contains("TryCaptureRegardLedger(out KingdomRegardLedgerSnapshot oldStanding)",
				source);
			StringAssert.Contains("TryAdjustRegardForRealmBatch(standing, mirror: false)",
				source);
			StringAssert.Contains("EnrollmentCut(\"annexe:standings\")", source);
			StringAssert.Contains("TryRestoreRegardLedger(oldStanding)", source);
			StringAssert.Contains("debit.Rollback() || debit.RestorationExact", source);
			StringAssert.Contains("rosterRestored &&", source);
			StringAssert.Contains("recordRestored && licensesRestored", source);
			StringAssert.Contains("QuarantineIdentity(", source);
			StringAssert.DoesNotContain("Realm.Standings?.Remove", source);
			StringAssert.DoesNotContain("Realm.AdjustRegardForRealm", source);
		}

		[Test]
		public void CreedRiteAndDeclarationPublishUnderCompensatedGovernanceBoundary()
		{
			string entry = TestMain.ReadRepositoryText(
				"Core/KingdomCreed.03.RiteAndDeclaration.cs");
			string effects = TestMain.ReadRepositoryText(
				"Core/KingdomCreed.03a.PublicationTransactions.cs");
			StringAssert.Contains("TryPublishRiteEffects(System, debit, temper", entry);
			StringAssert.Contains("TryPublishDeclarationEffects(System, CreedFactionName", entry);
			StringAssert.Contains("KingdomGovernanceScope.TryPublish(\"hold shared rite\"", effects);
			StringAssert.Contains("KingdomGovernanceScope.TryPublish(\"declare creed\"", effects);
			StringAssert.Contains("TryCaptureRegardLedger(out KingdomRegardLedgerSnapshot before)",
				effects);
			StringAssert.Contains("TryAdjustRegardForRealmBatch(standing, mirror: false)", effects);
			StringAssert.Contains("TryRestoreRegardLedger(before)", effects);
			StringAssert.Contains("debit.Rollback() || debit.RestorationExact", effects);
			StringAssert.Contains("PublicationCut(\"rite:water\")", effects);
			StringAssert.Contains("PublicationCut(\"declaration:standings\")", effects);
			StringAssert.Contains("QuarantineIdentity(", effects);
			int batch = effects.IndexOf("TryAdjustRegardForRealmBatch",
				StringComparison.Ordinal);
			int declared = effects.IndexOf("system.DeclaredCreed = creedFaction",
				StringComparison.Ordinal);
			Assert.That(declared, Is.GreaterThan(batch));
		}

		[Test]
		public void VillagePublicationUsesExactWriteAheadReceiptAndOwnedCutEvidence()
		{
			string effect = TestMain.ReadRepositoryText(
				"Core/KingdomFoundingTransaction.15aVillageStandingEffect.cs");
			int before = effect.IndexOf("basin.PendingVillageEffectBefore = before;",
				StringComparison.Ordinal);
			int prepared = effect.IndexOf("VillageStandingEffectPrepared;",
				StringComparison.Ordinal);
			int write = effect.IndexOf("system.TrySetRegardForRealm(",
				StringComparison.Ordinal);
			int applied = effect.IndexOf("VillageStandingEffectApplied;",
				StringComparison.Ordinal);
			Assert.That(before, Is.GreaterThanOrEqualTo(0));
			Assert.That(prepared, Is.GreaterThan(before));
			Assert.That(write, Is.GreaterThan(prepared));
			Assert.That(applied, Is.GreaterThan(write));
			StringAssert.Contains("Preexisting village regard cannot be attributed", effect);
			StringAssert.Contains("current == after && currentCarry == afterCarry", effect);
			StringAssert.Contains("site.GetZoneProperty(\"faction\", null) ==",
				effect);
			string proof = Slice(effect,
				"private static bool VillageStandingEffectProvesPublication(",
				"private static bool ExactVillagePublicationIdentity(");
			StringAssert.Contains("VillageStandingEffectReceiptValid", proof);
			StringAssert.Contains("VillageStandingEffectPrepared", proof);
			StringAssert.DoesNotContain("GetRegardForRealm", proof);
			StringAssert.DoesNotContain(">= KingdomRules.VillageCharterSealedStanding", proof);
		}

		[Test]
		public void UnsupportedLegacyCharterCannotBypassReceipt()
		{
			string source = TestMain.ReadRepositoryText(
				"Core/KingdomFounding.07.VillageCharter.cs");
			StringAssert.Contains("[Obsolete(", source);
			StringAssert.Contains("refused unsupported direct village charter publication", source);
			StringAssert.DoesNotContain("SetRegardForRealm", source);
			StringAssert.DoesNotContain("RecordChronicleAtomically", source);
		}

		private static string Slice(string source, string start, string end)
		{
			int from = source.IndexOf(start, StringComparison.Ordinal);
			Assert.That(from, Is.GreaterThanOrEqualTo(0));
			int to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
			Assert.That(to, Is.GreaterThan(from));
			return source.Substring(from, to - from);
		}
	}
}
#endif
