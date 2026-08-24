#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomFoundingTransactionRulesTests
	{
		[Test]
		public void EveryKindPhasePairHasOneExactPendingMeaning()
		{
			foreach (KingdomFoundingKind kind in Enum.GetValues(typeof(KingdomFoundingKind)))
			{
				foreach (KingdomFoundingPhase phase in Enum.GetValues(typeof(KingdomFoundingPhase)))
				{
					bool valid = KingdomFoundingTransactionRules.IsValidPair(kind, phase);
					bool pending = KingdomFoundingTransactionRules.IsPending(kind, phase);
					if (kind == KingdomFoundingKind.None)
					{
						Assert.AreEqual(phase == KingdomFoundingPhase.None, valid,
							kind + "/" + phase + " validity");
						Assert.IsFalse(pending, kind + "/" + phase + " pending");
					}
					else
					{
						Assert.IsTrue(valid,
							kind + "/" + phase + " validity");
						Assert.AreEqual(phase == KingdomFoundingPhase.WaterCommitted ||
							phase == KingdomFoundingPhase.PublicationCommitted ||
							phase == KingdomFoundingPhase.RecoveryRequired ||
							phase == KingdomFoundingPhase.Complete, pending,
							kind + "/" + phase + " pending");
					}
				}
			}
		}

		[Test]
		public void FailureContractIsExhaustive()
		{
			for (int published = 0; published <= 1; published++)
			{
				for (int changed = 0; changed <= 1; changed++)
				{
					for (int restored = 0; restored <= 1; restored++)
					{
						KingdomFoundingOutcome result =
							KingdomFoundingTransactionRules.FailureOutcome(
								published == 1, changed == 1, restored == 1);
						if (published == 1)
						{
							Assert.AreEqual(KingdomFoundingOutcome.RecoverableFailure, result);
						}
						else if (changed == 0)
						{
							Assert.AreEqual(KingdomFoundingOutcome.Refused, result);
						}
						else
						{
							Assert.AreEqual(restored == 1
								? KingdomFoundingOutcome.CompensatedFailure
								: KingdomFoundingOutcome.RecoverableFailure, result);
						}
						Assert.IsFalse(KingdomFoundingTransactionRules.ChargesEnergy(result));
						Assert.IsFalse(KingdomFoundingTransactionRules.RequestsInventoryExit(result));
					}
				}
			}
		}

		[Test]
		public void OnlyCommittedClosesAndCharges()
		{
			foreach (KingdomFoundingOutcome outcome in Enum.GetValues(typeof(KingdomFoundingOutcome)))
			{
				KingdomFoundingResult result = KingdomFoundingResult.From(outcome,
					KingdomFoundingTransactionRules.WaterDisposition(outcome, RestorationExact: true),
					KingdomFoundingProjection.Seal);
				Assert.AreEqual(outcome == KingdomFoundingOutcome.Committed, result.Committed);
				Assert.AreEqual(outcome == KingdomFoundingOutcome.Committed, result.ChargesEnergy);
				Assert.AreEqual(outcome == KingdomFoundingOutcome.Committed,
					result.RequestsInventoryExit);
			}
		}

		[Test]
		public void WaterDispositionNeverCallsLostWaterRestored()
		{
			Assert.AreEqual(KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.Refused, RestorationExact: false));
			Assert.AreEqual(KingdomFoundingWaterDisposition.RestoredExactly,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.CompensatedFailure, RestorationExact: true));
			Assert.AreEqual(KingdomFoundingWaterDisposition.RestorationFailed,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.CompensatedFailure, RestorationExact: false));
			Assert.AreEqual(KingdomFoundingWaterDisposition.HeldForRecovery,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.RecoverableFailure, RestorationExact: true));
			Assert.AreEqual(KingdomFoundingWaterDisposition.RestorationFailed,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.RecoverableFailure, RestorationExact: false));
			Assert.AreEqual(KingdomFoundingWaterDisposition.Spent,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.Committed, RestorationExact: false));
		}

		[Test]
		public void FailureAfterEveryProjectionCannotMasqueradeAsComplete()
		{
			int count = Enum.GetValues(typeof(KingdomFoundingProjection)).Length;
			for (int fail = (int)KingdomFoundingProjection.Water;
				fail <= (int)KingdomFoundingProjection.Seal; fail++)
			{
				bool[] succeeded = new bool[count];
				for (int step = (int)KingdomFoundingProjection.Water;
					step < fail; step++)
				{
					succeeded[step] = true;
				}
				Assert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
					succeeded, KingdomFoundingProjection.Seal),
					"failure at " + (KingdomFoundingProjection)fail);
			}
		}

		[Test]
		public void FailureOrderCrossesOneHonestPublicationBarrier()
		{
			for (int fail = (int)KingdomFoundingProjection.Water;
				fail <= (int)KingdomFoundingProjection.Seal; fail++)
			{
				KingdomFoundingProjection projection = (KingdomFoundingProjection)fail;
				bool published = fail >= (int)KingdomFoundingProjection.Identity;
				KingdomFoundingOutcome outcome =
					KingdomFoundingTransactionRules.FailureOutcome(
						published, WaterChanged: true, RestorationExact: true);
				Assert.AreEqual(published
					? KingdomFoundingOutcome.RecoverableFailure
					: KingdomFoundingOutcome.CompensatedFailure, outcome,
					"failure after " + projection);
				Assert.AreEqual(published
					? KingdomFoundingWaterDisposition.HeldForRecovery
					: KingdomFoundingWaterDisposition.RestoredExactly,
					KingdomFoundingTransactionRules.WaterDisposition(outcome,
						RestorationExact: true), "water after " + projection);
			}
		}

		[Test]
		public void FullProjectionAndOnlyFullProjectionCompletes()
		{
			int count = Enum.GetValues(typeof(KingdomFoundingProjection)).Length;
			bool[] succeeded = new bool[count];
			for (int step = (int)KingdomFoundingProjection.Water;
				step <= (int)KingdomFoundingProjection.Seal; step++)
			{
				succeeded[step] = true;
			}
			Assert.IsTrue(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
				succeeded, KingdomFoundingProjection.Seal));
			Assert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
				null, KingdomFoundingProjection.Seal));
			Assert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
				new bool[2], KingdomFoundingProjection.Seal));
			Assert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
				succeeded, KingdomFoundingProjection.None));
		}

		[TestCase(8, 8, true, 0)]
		[TestCase(16, 8, true, 8)]
		[TestCase(7, 8, false, 7)]
		[TestCase(8, 0, false, 8)]
		[TestCase(8, -1, false, 8)]
		[TestCase(-1, 1, false, -1)]
		public void SameVesselCommittedVolumeIsChecked(int original, int cost,
			bool expected, int expectedVolume)
		{
			Assert.AreEqual(expected, KingdomFoundingTransactionRules.TryCommittedVolume(
				original, cost, out var committed));
			Assert.AreEqual(expectedVolume, committed);
		}

		[Test]
		public void ReceiptHeadersClearOnlyProvenPreDebitOrTerminalState()
		{
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Clean,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.None,
					KingdomFoundingPhase.None));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Pending,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.FirstCity,
					KingdomFoundingPhase.WaterCommitted));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.ClearStaged,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.SecondCity,
					KingdomFoundingPhase.None));
				Assert.AreEqual(KingdomFoundingReceiptNormalization.Pending,
					KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.VillageCharter,
					KingdomFoundingPhase.Complete));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.None,
					KingdomFoundingPhase.WaterCommitted));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.Normalize((KingdomFoundingKind)99,
					KingdomFoundingPhase.WaterCommitted));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.FirstCity,
					(KingdomFoundingPhase)99));
		}

		[Test]
		public void ReceiptIdentityBindsExactBasinTransactionAndRealm()
		{
			Assert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Kavvat", null,
				KingdomFoundingKind.FirstCity));
			Assert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Kavvat", "Kavvat",
				KingdomFoundingKind.FirstCity));
			Assert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Sheol", "Kavvat",
				KingdomFoundingKind.SecondCity));

			Assert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "clone-2", "tx-1", "Kavvat", "Kavvat", "Kavvat",
				KingdomFoundingKind.FirstCity), "deep copy cannot spend copied receipt");
			Assert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "", "Kavvat", "Kavvat", "Kavvat",
				KingdomFoundingKind.FirstCity), "transaction id required");
			Assert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Other", "Kavvat", null,
				KingdomFoundingKind.FirstCity), "first intent is its realm binding");
			Assert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Sheol", "Other",
				KingdomFoundingKind.SecondCity), "later rites bind live realm");
		}

		[Test]
		public void SecondRecoveryCannotReplaceUnrelatedAwaySeatOrBreakCap()
		{
			Assert.IsTrue(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				1, 2, AwayIsNull: true, TargetIsExactSeat: false,
				AlreadyPublished: false));
			Assert.IsTrue(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 2, AwayIsNull: false, TargetIsExactSeat: true,
				AlreadyPublished: true));
			Assert.IsTrue(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 2, AwayIsNull: false, TargetIsExactSeat: false,
				TargetIsExactAway: true, AlreadyPublished: true),
				"the exact transaction city may recover from Away");

			Assert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 2, AwayIsNull: false, TargetIsExactSeat: false,
				AlreadyPublished: false), "unrelated second city blocks stale receipt");
			Assert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 2, AwayIsNull: false, TargetIsExactSeat: false,
				TargetIsExactAway: false, AlreadyPublished: true),
				"an unrelated Away city blocks the receipt");
			Assert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 2, AwayIsNull: true, TargetIsExactSeat: true,
				AlreadyPublished: true), "two-city projection requires retained old seat");
			Assert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				3, 2, AwayIsNull: false, TargetIsExactSeat: true,
				AlreadyPublished: true), "over-cap state is never trusted");
		}

		[Test]
		public void RawReceiptParserRejectsMissingUnknownAndFalseCleanHeaders()
		{
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Clean,
				KingdomFoundingTransactionRules.NormalizeRaw(false, 0, false, 0,
					AnyPayloadPresent: false));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(false, 0, false, 0,
					AnyPayloadPresent: true));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 1, false, 0,
					AnyPayloadPresent: true));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 99, true, 1,
					AnyPayloadPresent: true));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 1, true, 99,
					AnyPayloadPresent: true));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 0, true, 0,
					AnyPayloadPresent: true));
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Pending,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 2, true, 4,
					AnyPayloadPresent: true), "Complete remains paid until observed");
		}

		[Test]
		public void OnlyNamedEnumValuesAreAccepted()
		{
			Assert.IsFalse(KingdomFoundingTransactionRules.IsKnownKind(
				(KingdomFoundingKind)255));
			Assert.IsFalse(KingdomFoundingTransactionRules.IsKnownKind(
				(KingdomFoundingKind)4));
			Assert.IsFalse(KingdomFoundingTransactionRules.IsKnownPhase(
				(KingdomFoundingPhase)255));
			Assert.IsFalse(KingdomFoundingTransactionRules.IsKnownPhase(
				(KingdomFoundingPhase)5));
			Assert.IsFalse(KingdomFoundingTransactionRules.IsKnownOwnerKind(
				(KingdomFoundingOwnerKind)3));
			Assert.IsFalse(KingdomFoundingTransactionRules.IsKnownChronicleDisposition(
				(KingdomChronicleDisposition)255));
		}

		[Test]
		public void ChronicleDispositionFreezesOptionalJournalOutcome()
		{
			Assert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				0, KingdomChronicleDisposition.None, 0));
			Assert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				1, KingdomChronicleDisposition.Required, 0));
			Assert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				1, KingdomChronicleDisposition.Required, 1));
			Assert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Inserted, 1));
			Assert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Skipped, 0));
			Assert.IsFalse(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.None, 0));
			Assert.IsFalse(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Required, 1));
			Assert.IsFalse(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Skipped, 1));
		}

		[Test]
		public void LegacyChronicleMigrationIsConservativeAndTerminalOnceWritten()
		{
			Assert.IsTrue(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: false, Raw: 0, AccomplishmentCount: 1,
				ChronicleOptionIsNo: false, out var inserted, out var writeInserted));
			Assert.AreEqual(KingdomChronicleDisposition.Inserted, inserted);
			Assert.IsTrue(writeInserted);

			Assert.IsTrue(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: false, Raw: 0, AccomplishmentCount: 0,
				ChronicleOptionIsNo: true, out var skipped, out var writeSkipped));
			Assert.AreEqual(KingdomChronicleDisposition.Skipped, skipped);
			Assert.IsTrue(writeSkipped);
			Assert.IsFalse(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: false, Raw: 0, AccomplishmentCount: 0,
				ChronicleOptionIsNo: false, out var _, out var _),
				"an option change cannot invent the old decision");

			Assert.IsTrue(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: true, Raw: (int)KingdomChronicleDisposition.Skipped,
				AccomplishmentCount: 0, ChronicleOptionIsNo: false,
				out var persisted, out var rewrite));
			Assert.AreEqual(KingdomChronicleDisposition.Skipped, persisted,
				"persisted option-No disposition stays valid after option becomes Yes");
			Assert.IsFalse(rewrite);
		}

		[Test]
		public void AuthorityRoundTripsCanonicallyAndBindsEveryTupleMember()
		{
			KingdomFoundingAuthority authority = Authority("0123456789abcdef0123456789abcdef",
				"fedcba9876543210fedcba9876543210", "Kavvat", "JoppaWorld.1.1.1.1.10");
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			Assert.IsNotNull(encoded);
			Assert.IsTrue(KingdomFoundingTransactionRules.TryParseAuthority(encoded,
				out var parsed));
			Assert.AreEqual(encoded,
				KingdomFoundingTransactionRules.FormatAuthority(parsed));

			KingdomFoundingAuthority[] foreign = new KingdomFoundingAuthority[]
			{
				Authority("1123456789abcdef0123456789abcdef", authority.OwnerNonce,
					authority.RealmFaction, authority.ZoneID),
				Authority(authority.TransactionID,
					"eedcba9876543210fedcba9876543210", authority.RealmFaction,
					authority.ZoneID),
				Authority(authority.TransactionID, authority.OwnerNonce, "Other",
					authority.ZoneID),
				Authority(authority.TransactionID, authority.OwnerNonce,
					authority.RealmFaction, "JoppaWorld.2.2.1.1.10")
			};
			foreach (KingdomFoundingAuthority other in foreign)
			{
				Assert.IsFalse(KingdomFoundingTransactionRules.AuthorityMatches(
					encoded, other));
			}
			KingdomFoundingAuthority differentRite = authority;
			differentRite.RiteX++;
			Assert.IsFalse(KingdomFoundingTransactionRules.AuthorityMatches(
				encoded, differentRite));
			KingdomFoundingAuthority differentDigest = authority;
			differentDigest.PayloadDigest = new string('b', 64);
			Assert.IsFalse(KingdomFoundingTransactionRules.AuthorityMatches(
				encoded, differentDigest));
		}

		[Test]
		public void AuthorityParserRejectsMalformedClonesAndCoordinates()
		{
			KingdomFoundingAuthority authority = Authority("0123456789abcdef0123456789abcdef",
				"fedcba9876543210fedcba9876543210", "Kavvat", "zone");
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			Assert.IsFalse(KingdomFoundingTransactionRules.TryParseAuthority(
				encoded + "|extra", out var _));
			Assert.IsFalse(KingdomFoundingTransactionRules.TryParseAuthority(
				encoded.Replace("taf-founding-v1", "taf-founding-v2"), out var _));
			authority.RiteX = -1;
			Assert.IsNull(KingdomFoundingTransactionRules.FormatAuthority(authority));
			authority.RiteX = 256;
			Assert.IsNull(KingdomFoundingTransactionRules.FormatAuthority(authority));
			authority.RiteX = 1;
			authority.OwnerKind = (KingdomFoundingOwnerKind)99;
			Assert.IsNull(KingdomFoundingTransactionRules.FormatAuthority(authority));
		}

		[Test]
		public void CopyIdAndPolygelStyleNonceChangesCannotOwnReceipt()
		{
			string transaction = "0123456789abcdef0123456789abcdef";
			string original = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
			string clone = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
			Assert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				original, original, KingdomFoundingOwnerKind.Basin, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
			Assert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				original, clone, KingdomFoundingOwnerKind.Basin, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
			Assert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				original, original, KingdomFoundingOwnerKind.Direct, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
			Assert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"not-a-nonce", "not-a-nonce", KingdomFoundingOwnerKind.Basin, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
		}

		[Test]
		public void ComponentParserRejectsNoncanonicalCorruptAndOversizedPayloads()
		{
			Assert.IsTrue(KingdomFoundingTransactionRules.TryDecodeComponents(
				"d2F0ZXI=:1000", out var water));
			Assert.IsTrue(KingdomFoundingTransactionRules.ComponentsDescribePureWater(
				water, 8));
			Assert.IsTrue(KingdomFoundingTransactionRules.TryDecodeComponents("",
				out var empty));
			Assert.IsTrue(KingdomFoundingTransactionRules.ComponentsDescribePureWater(
				empty, 0));
			string[] malformed = new string[]
			{
				"d2F0ZXI=:0", "d2F0ZXI=:1001", "d2F0ZXI=:+1000",
				"d2F0ZXI=:01000", "%%%:1000", "d2F0ZXI=:1000;d2F0ZXI=:1000",
				"d2F0ZXI=:1000;YWNpZA==:1"
			};
			foreach (string encoded in malformed)
			{
				Assert.IsFalse(KingdomFoundingTransactionRules.TryDecodeComponents(
					encoded, out var _), encoded);
			}
			Assert.IsFalse(KingdomFoundingTransactionRules.TryDecodeComponents(
				new string('x', KingdomFoundingTransactionRules.MaximumComponentEncodingLength + 1),
				out var _));
		}

		[Test]
		public void WaterAlgebraRejectsEveryCorruptAxis()
		{
			Assert.IsTrue(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 20, 8, 20, 8, true, true));
			Assert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				15, 20, 8, 20, 8, true, true), "wrong debit");
			Assert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 15, 8, 15, 8, true, true), "volume exceeds max");
			Assert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 20, 8, 21, 8, true, true), "max changed");
			Assert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 20, 8, 20, 8, false, true), "original mixture");
			Assert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 20, 8, 20, 8, true, false), "committed mixture");
		}

		[Test]
		public void SaveLoadAtEveryPhaseKeepsPaidAndCorruptCompleteQuarantined()
		{
			foreach (KingdomFoundingPhase phase in Enum.GetValues(
				typeof(KingdomFoundingPhase)))
			{
				if (phase == KingdomFoundingPhase.RecoveryRequired)
				{
					Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
						KingdomFoundingTransactionRules.ValidatePhaseState(phase,
							PayloadValid: true, CurrentMatchesOriginal: false,
							CurrentMatchesCommitted: true, CompletionObserved: false));
					continue;
				}
				bool completion = phase == KingdomFoundingPhase.Complete;
				KingdomFoundingReceiptNormalization expected = phase == KingdomFoundingPhase.None
					? KingdomFoundingReceiptNormalization.ClearStaged
					: completion
						? KingdomFoundingReceiptNormalization.ClearStaged
						: KingdomFoundingReceiptNormalization.Pending;
				Assert.AreEqual(expected,
					KingdomFoundingTransactionRules.ValidatePhaseState(phase,
						PayloadValid: true,
						CurrentMatchesOriginal: phase == KingdomFoundingPhase.None,
						CurrentMatchesCommitted: phase != KingdomFoundingPhase.None,
						CompletionObserved: completion), phase.ToString());
			}
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.ValidatePhaseState(
					KingdomFoundingPhase.Complete, PayloadValid: true,
					CurrentMatchesOriginal: false, CurrentMatchesCommitted: true,
					CompletionObserved: false), "a false Complete cannot clear");
			Assert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.ValidatePhaseState(
					KingdomFoundingPhase.PublicationCommitted, PayloadValid: false,
					CurrentMatchesOriginal: false, CurrentMatchesCommitted: true,
					CompletionObserved: false), "corrupt paid state cannot clear");
		}

		private static KingdomFoundingAuthority Authority(string Transaction,
			string OwnerNonce, string Realm, string Zone)
		{
			return new KingdomFoundingAuthority
			{
				Kind = KingdomFoundingKind.SecondCity,
				TransactionID = Transaction,
				OwnerKind = KingdomFoundingOwnerKind.Basin,
				OwnerNonce = OwnerNonce,
				RealmFaction = Realm,
				ZoneID = Zone,
				RiteX = 1,
				RiteY = 2,
				PayloadDigest = new string('a', 64)
			};
		}
	}
}
#endif
