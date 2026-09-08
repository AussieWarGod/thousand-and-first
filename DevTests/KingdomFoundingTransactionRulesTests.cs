#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomFoundingTransactionRulesTests
	{
		private static void AssertByteEnum(Type type, string expected)
		{
			ClassicAssert.IsTrue(type.IsPublic && type.IsEnum, type.FullName);
			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(type), type.FullName);
			Array values = Enum.GetValues(type);
			string[] shape = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				object value = values.GetValue(i);
				shape[i] = Convert.ToInt32(value) + ":" + value;
			}
			ClassicAssert.AreEqual(expected, string.Join(",", shape), type.FullName);
		}

		private static void AssertPublicFields(Type type, string[] names, Type[] types)
		{
			System.Reflection.FieldInfo[] fields = type.GetFields(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
				System.Reflection.BindingFlags.DeclaredOnly);
			ClassicAssert.AreEqual(names.Length, fields.Length, type.FullName + " field count");
			for (int i = 0; i < names.Length; i++)
			{
				ClassicAssert.AreEqual(names[i], fields[i].Name, type.FullName + " field order " + i);
				ClassicAssert.AreEqual(types[i], fields[i].FieldType, type.FullName + "." + names[i]);
			}
		}

		[Test]
		public void FoundingDeclarationsKeepExactPublicAbiValuesAndDefaults()
		{
			AssertByteEnum(typeof(KingdomFoundingKind),
				"0:None,1:FirstCity,2:SecondCity,3:VillageCharter");
			AssertByteEnum(typeof(KingdomFoundingPhase),
				"0:None,1:WaterCommitted,2:PublicationCommitted,3:RecoveryRequired,4:Complete");
			AssertByteEnum(typeof(KingdomFoundingReceiptNormalization),
				"0:Clean,1:Pending,2:ClearStaged,3:Quarantine");
			AssertByteEnum(typeof(KingdomFoundingOwnerKind), "0:None,1:Basin,2:Direct");
			AssertByteEnum(typeof(KingdomFoundingOutcome),
				"0:Refused,1:CompensatedFailure,2:RecoverableFailure,3:Committed");
			AssertByteEnum(typeof(KingdomFoundingWaterDisposition),
				"0:Untouched,1:RestoredExactly,2:HeldForRecovery,3:Spent,4:RestorationFailed");
			AssertByteEnum(typeof(KingdomFoundingProjection),
				"0:None,1:Water,2:Identity,3:Claim,4:Seat,5:Ability,6:Placement,7:Seal");
			AssertByteEnum(typeof(KingdomChronicleDisposition),
				"0:None,1:Required,2:Inserted,3:Skipped");

			Type rules = typeof(KingdomFoundingTransactionRules);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomFoundingTransactionRules", rules.FullName);
			ClassicAssert.IsTrue(rules.IsPublic && rules.IsAbstract && rules.IsSealed);

			Type authority = typeof(KingdomFoundingAuthority);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomFoundingAuthority", authority.FullName);
			ClassicAssert.IsTrue(authority.IsPublic && authority.IsValueType);
			AssertPublicFields(authority,
				new[] { "Kind", "TransactionID", "OwnerKind", "OwnerNonce", "RealmFaction",
					"ZoneID", "RiteX", "RiteY", "PayloadDigest" },
				new[] { typeof(KingdomFoundingKind), typeof(string),
					typeof(KingdomFoundingOwnerKind), typeof(string), typeof(string), typeof(string),
					typeof(int), typeof(int), typeof(string) });
			KingdomFoundingAuthority emptyAuthority = default(KingdomFoundingAuthority);
			ClassicAssert.AreEqual(KingdomFoundingKind.None, emptyAuthority.Kind);
			ClassicAssert.AreEqual(KingdomFoundingOwnerKind.None, emptyAuthority.OwnerKind);
			ClassicAssert.IsNull(emptyAuthority.TransactionID);
			ClassicAssert.AreEqual(0, emptyAuthority.RiteX);

			Type result = typeof(KingdomFoundingResult);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomFoundingResult", result.FullName);
			ClassicAssert.IsTrue(result.IsPublic && result.IsValueType);
			AssertPublicFields(result,
				new[] { "Outcome", "Water", "Projection", "Failure" },
				new[] { typeof(KingdomFoundingOutcome), typeof(KingdomFoundingWaterDisposition),
					typeof(KingdomFoundingProjection), typeof(string) });
			KingdomFoundingResult emptyResult = default(KingdomFoundingResult);
			ClassicAssert.AreEqual(KingdomFoundingOutcome.Refused, emptyResult.Outcome);
			ClassicAssert.AreEqual(KingdomFoundingWaterDisposition.Untouched, emptyResult.Water);
			ClassicAssert.AreEqual(KingdomFoundingProjection.None, emptyResult.Projection);
			ClassicAssert.IsNull(emptyResult.Failure);
			ClassicAssert.AreEqual("", KingdomFoundingResult.From(KingdomFoundingOutcome.Refused,
				KingdomFoundingWaterDisposition.Untouched, KingdomFoundingProjection.None).Failure);
		}

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
						ClassicAssert.AreEqual(phase == KingdomFoundingPhase.None, valid,
							kind + "/" + phase + " validity");
						ClassicAssert.IsFalse(pending, kind + "/" + phase + " pending");
					}
					else
					{
						ClassicAssert.IsTrue(valid,
							kind + "/" + phase + " validity");
						ClassicAssert.AreEqual(phase == KingdomFoundingPhase.WaterCommitted ||
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
							ClassicAssert.AreEqual(KingdomFoundingOutcome.RecoverableFailure, result);
						}
						else if (changed == 0)
						{
							ClassicAssert.AreEqual(KingdomFoundingOutcome.Refused, result);
						}
						else
						{
							ClassicAssert.AreEqual(restored == 1
								? KingdomFoundingOutcome.CompensatedFailure
								: KingdomFoundingOutcome.RecoverableFailure, result);
						}
						ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ChargesEnergy(result));
						ClassicAssert.IsFalse(KingdomFoundingTransactionRules.RequestsInventoryExit(result));
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
				ClassicAssert.AreEqual(outcome == KingdomFoundingOutcome.Committed, result.Committed);
				ClassicAssert.AreEqual(outcome == KingdomFoundingOutcome.Committed, result.ChargesEnergy);
				ClassicAssert.AreEqual(outcome == KingdomFoundingOutcome.Committed,
					result.RequestsInventoryExit);
			}
		}

		[Test]
		public void WaterDispositionNeverCallsLostWaterRestored()
		{
			ClassicAssert.AreEqual(KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.Refused, RestorationExact: false));
			ClassicAssert.AreEqual(KingdomFoundingWaterDisposition.RestoredExactly,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.CompensatedFailure, RestorationExact: true));
			ClassicAssert.AreEqual(KingdomFoundingWaterDisposition.RestorationFailed,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.CompensatedFailure, RestorationExact: false));
			ClassicAssert.AreEqual(KingdomFoundingWaterDisposition.HeldForRecovery,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.RecoverableFailure, RestorationExact: true));
			ClassicAssert.AreEqual(KingdomFoundingWaterDisposition.RestorationFailed,
				KingdomFoundingTransactionRules.WaterDisposition(
					KingdomFoundingOutcome.RecoverableFailure, RestorationExact: false));
			ClassicAssert.AreEqual(KingdomFoundingWaterDisposition.Spent,
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
				ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
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
				ClassicAssert.AreEqual(published
					? KingdomFoundingOutcome.RecoverableFailure
					: KingdomFoundingOutcome.CompensatedFailure, outcome,
					"failure after " + projection);
				ClassicAssert.AreEqual(published
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
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
				succeeded, KingdomFoundingProjection.Seal));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
				null, KingdomFoundingProjection.Seal));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
				new bool[2], KingdomFoundingProjection.Seal));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ProjectionSequenceComplete(
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
			ClassicAssert.AreEqual(expected, KingdomFoundingTransactionRules.TryCommittedVolume(
				original, cost, out var committed));
			ClassicAssert.AreEqual(expectedVolume, committed);
		}

		[Test]
		public void ReceiptHeadersClearOnlyProvenPreDebitOrTerminalState()
		{
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Clean,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.None,
					KingdomFoundingPhase.None));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Pending,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.FirstCity,
					KingdomFoundingPhase.WaterCommitted));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.ClearStaged,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.SecondCity,
					KingdomFoundingPhase.None));
				ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Pending,
					KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.VillageCharter,
					KingdomFoundingPhase.Complete));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.None,
					KingdomFoundingPhase.WaterCommitted));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.Normalize((KingdomFoundingKind)99,
					KingdomFoundingPhase.WaterCommitted));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.Normalize(KingdomFoundingKind.FirstCity,
					(KingdomFoundingPhase)99));
		}

		[Test]
		public void ReceiptIdentityBindsExactBasinTransactionAndRealm()
		{
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Kavvat", null,
				KingdomFoundingKind.FirstCity));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Kavvat", "Kavvat",
				KingdomFoundingKind.FirstCity));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Sheol", "Kavvat",
				KingdomFoundingKind.SecondCity));

			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "clone-2", "tx-1", "Kavvat", "Kavvat", "Kavvat",
				KingdomFoundingKind.FirstCity), "deep copy cannot spend copied receipt");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "", "Kavvat", "Kavvat", "Kavvat",
				KingdomFoundingKind.FirstCity), "transaction id required");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Other", "Kavvat", null,
				KingdomFoundingKind.FirstCity), "first intent is its realm binding");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"basin-1", "basin-1", "tx-1", "Kavvat", "Sheol", "Other",
				KingdomFoundingKind.SecondCity), "later rites bind live realm");
		}

		[Test]
		public void LaterRecoveryUsesEitherOpenSlotWithoutReplacingUnrelatedCities()
		{
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				1, 3, HasOpenNonSeatSlot: true, TargetIsExactSeat: false,
				AlreadyPublished: false));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 3, HasOpenNonSeatSlot: true, TargetIsExactSeat: false,
				AlreadyPublished: false), "third city may use second open non-seat slot");
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 3, HasOpenNonSeatSlot: true, TargetIsExactSeat: true,
				AlreadyPublished: true));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				3, 3, HasOpenNonSeatSlot: false, TargetIsExactSeat: false,
				TargetIsExactNonSeat: true, AlreadyPublished: true),
				"exact transaction city may recover from either non-seat row");

			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				3, 3, HasOpenNonSeatSlot: false, TargetIsExactSeat: false,
				AlreadyPublished: false), "full realm blocks new publication");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				2, 3, HasOpenNonSeatSlot: true, TargetIsExactSeat: false,
				TargetIsExactNonSeat: false, AlreadyPublished: true),
				"unrelated city blocks published receipt");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				3, 3, HasOpenNonSeatSlot: true, TargetIsExactSeat: true,
				AlreadyPublished: true), "room flag must agree with bounded count");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				4, 3, HasOpenNonSeatSlot: false, TargetIsExactSeat: true,
				AlreadyPublished: true), "over-cap state is never trusted");
		}

		[Test]
		public void RawReceiptParserRejectsMissingUnknownAndFalseCleanHeaders()
		{
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Clean,
				KingdomFoundingTransactionRules.NormalizeRaw(false, 0, false, 0,
					AnyPayloadPresent: false));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(false, 0, false, 0,
					AnyPayloadPresent: true));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 1, false, 0,
					AnyPayloadPresent: true));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 99, true, 1,
					AnyPayloadPresent: true));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 1, true, 99,
					AnyPayloadPresent: true));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 0, true, 0,
					AnyPayloadPresent: true));
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Pending,
				KingdomFoundingTransactionRules.NormalizeRaw(true, 2, true, 4,
					AnyPayloadPresent: true), "Complete remains paid until observed");
		}

		[Test]
		public void OnlyNamedEnumValuesAreAccepted()
		{
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.IsKnownKind(
				(KingdomFoundingKind)255));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.IsKnownKind(
				(KingdomFoundingKind)4));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.IsKnownPhase(
				(KingdomFoundingPhase)255));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.IsKnownPhase(
				(KingdomFoundingPhase)5));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.IsKnownOwnerKind(
				(KingdomFoundingOwnerKind)3));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.IsKnownChronicleDisposition(
				(KingdomChronicleDisposition)255));
		}

		[Test]
		public void ChronicleDispositionFreezesOptionalJournalOutcome()
		{
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				0, KingdomChronicleDisposition.None, 0));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				1, KingdomChronicleDisposition.Required, 0));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				1, KingdomChronicleDisposition.Required, 1));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Inserted, 1));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Skipped, 0));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.None, 0));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Required, 1));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ChronicleDispositionValid(
				2, KingdomChronicleDisposition.Skipped, 1));
		}

		[Test]
		public void LegacyChronicleMigrationIsConservativeAndTerminalOnceWritten()
		{
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: false, Raw: 0, AccomplishmentCount: 1,
				ChronicleOptionIsNo: false, out var inserted, out var writeInserted));
			ClassicAssert.AreEqual(KingdomChronicleDisposition.Inserted, inserted);
			ClassicAssert.IsTrue(writeInserted);

			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: false, Raw: 0, AccomplishmentCount: 0,
				ChronicleOptionIsNo: true, out var skipped, out var writeSkipped));
			ClassicAssert.AreEqual(KingdomChronicleDisposition.Skipped, skipped);
			ClassicAssert.IsTrue(writeSkipped);
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: false, Raw: 0, AccomplishmentCount: 0,
				ChronicleOptionIsNo: false, out var _, out var _),
				"an option change cannot invent the old decision");

			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				2, RawPresent: true, Raw: (int)KingdomChronicleDisposition.Skipped,
				AccomplishmentCount: 0, ChronicleOptionIsNo: false,
				out var persisted, out var rewrite));
			ClassicAssert.AreEqual(KingdomChronicleDisposition.Skipped, persisted,
				"persisted option-No disposition stays valid after option becomes Yes");
			ClassicAssert.IsFalse(rewrite);
		}

		[Test]
		public void AuthorityRoundTripsCanonicallyAndBindsEveryTupleMember()
		{
			KingdomFoundingAuthority authority = Authority("0123456789abcdef0123456789abcdef",
				"fedcba9876543210fedcba9876543210", "Kavvat", "JoppaWorld.1.1.1.1.10");
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			ClassicAssert.IsNotNull(encoded);
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.TryParseAuthority(encoded,
				out var parsed));
			ClassicAssert.AreEqual(encoded,
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
				ClassicAssert.IsFalse(KingdomFoundingTransactionRules.AuthorityMatches(
					encoded, other));
			}
			KingdomFoundingAuthority differentRite = authority;
			differentRite.RiteX++;
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.AuthorityMatches(
				encoded, differentRite));
			KingdomFoundingAuthority differentDigest = authority;
			differentDigest.PayloadDigest = new string('b', 64);
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.AuthorityMatches(
				encoded, differentDigest));
		}

		[Test]
		public void AuthorityParserRejectsMalformedClonesAndCoordinates()
		{
			KingdomFoundingAuthority authority = Authority("0123456789abcdef0123456789abcdef",
				"fedcba9876543210fedcba9876543210", "Kavvat", "zone");
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.TryParseAuthority(
				encoded + "|extra", out var _));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.TryParseAuthority(
				encoded.Replace("taf-founding-v1", "taf-founding-v2"), out var _));
			authority.RiteX = -1;
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.FormatAuthority(authority));
			authority.RiteX = 256;
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.FormatAuthority(authority));
			authority.RiteX = 1;
			authority.OwnerKind = (KingdomFoundingOwnerKind)99;
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.FormatAuthority(authority));
		}

		[Test]
		public void VillageStandingEffectBindsExactOwnedTransitionAndRejectsPreexistingHigh()
		{
			string transaction = "0123456789abcdef0123456789abcdef";
			string owner = "fedcba9876543210fedcba9876543210";
			string zone = "JoppaWorld.1.1.1.1.10";
			KingdomFoundingAuthority authority = Authority(transaction, owner, "Kavvat", zone);
			authority.Kind = KingdomFoundingKind.VillageCharter;
			string encoded = KingdomFoundingTransactionRules.FormatAuthority(authority);
			string digest = KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				transaction, encoded, "village-faction", "the village", zone,
				599, 99, 600, 0);
			ClassicAssert.IsNotNull(digest);
			ClassicAssert.AreEqual(digest,
				KingdomFoundingTransactionRules.VillageStandingEffectDigest(
					transaction, encoded, "village-faction", "the village", zone,
					599, 99, 600, 0), "canonical input is deterministic");
			ClassicAssert.AreNotEqual(digest,
				KingdomFoundingTransactionRules.VillageStandingEffectDigest(
					transaction, encoded, "other-faction", "the village", zone,
					599, 99, 600, 0), "faction is receipt-bound");
			ClassicAssert.AreNotEqual(digest,
				KingdomFoundingTransactionRules.VillageStandingEffectDigest(
					transaction, encoded, "village-faction", "other display", zone,
					599, 99, 600, 0), "display is receipt-bound");
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				transaction, encoded, "village-faction", "the village", zone,
				600, 0, 600, 0), "preexisting sealed standing is not this transaction's effect");
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				transaction, encoded, "village-faction", "the village", zone,
				601, 0, 600, 0), "preexisting higher standing is not this transaction's effect");
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				transaction, encoded, "village-faction", "the village", zone,
				599, -1, 600, 0), "noncanonical before pair is refused");
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				transaction, encoded, "village-faction", "the village", zone,
				599, 99, 600, 1), "after pair must be exact whole standing");
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				transaction, encoded, "village-faction", "the village", zone,
				599, 99, 601, 0), "after pair must be the sealed covenant target");
			KingdomFoundingAuthority wrongKind = authority;
			wrongKind.Kind = KingdomFoundingKind.SecondCity;
			ClassicAssert.IsNull(KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				transaction, KingdomFoundingTransactionRules.FormatAuthority(wrongKind),
				"village-faction", "the village", zone, 599, 99, 600, 0));
		}

		[Test]
		public void CopyIdAndPolygelStyleNonceChangesCannotOwnReceipt()
		{
			string transaction = "0123456789abcdef0123456789abcdef";
			string original = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
			string clone = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				original, original, KingdomFoundingOwnerKind.Basin, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				original, clone, KingdomFoundingOwnerKind.Basin, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				original, original, KingdomFoundingOwnerKind.Direct, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.ReceiptBindingMatches(
				"not-a-nonce", "not-a-nonce", KingdomFoundingOwnerKind.Basin, transaction,
				"Kavvat", "Kavvat", "Kavvat", KingdomFoundingKind.FirstCity));
		}

		[Test]
		public void ComponentParserRejectsNoncanonicalCorruptAndOversizedPayloads()
		{
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.TryDecodeComponents(
				"d2F0ZXI=:1000", out var water));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ComponentsDescribePureWater(
				water, 8));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.TryDecodeComponents("",
				out var empty));
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.ComponentsDescribePureWater(
				empty, 0));
			string[] malformed = new string[]
			{
				"d2F0ZXI=:0", "d2F0ZXI=:1001", "d2F0ZXI=:+1000",
				"d2F0ZXI=:01000", "%%%:1000", "d2F0ZXI=:1000;d2F0ZXI=:1000",
				"d2F0ZXI=:1000;YWNpZA==:1"
			};
			foreach (string encoded in malformed)
			{
				ClassicAssert.IsFalse(KingdomFoundingTransactionRules.TryDecodeComponents(
					encoded, out var _), encoded);
			}
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.TryDecodeComponents(
				new string('x', KingdomFoundingTransactionRules.MaximumComponentEncodingLength + 1),
				out var _));
		}

		[Test]
		public void WaterAlgebraRejectsEveryCorruptAxis()
		{
			ClassicAssert.IsTrue(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 20, 8, 20, 8, true, true));
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				15, 20, 8, 20, 8, true, true), "wrong debit");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 15, 8, 15, 8, true, true), "volume exceeds max");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 20, 8, 21, 8, true, true), "max changed");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
				16, 20, 8, 20, 8, false, true), "original mixture");
			ClassicAssert.IsFalse(KingdomFoundingTransactionRules.WaterAlgebraValid(
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
					ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
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
				ClassicAssert.AreEqual(expected,
					KingdomFoundingTransactionRules.ValidatePhaseState(phase,
						PayloadValid: true,
						CurrentMatchesOriginal: phase == KingdomFoundingPhase.None,
						CurrentMatchesCommitted: phase != KingdomFoundingPhase.None,
						CompletionObserved: completion), phase.ToString());
			}
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
				KingdomFoundingTransactionRules.ValidatePhaseState(
					KingdomFoundingPhase.Complete, PayloadValid: true,
					CurrentMatchesOriginal: false, CurrentMatchesCommitted: true,
					CompletionObserved: false), "a false Complete cannot clear");
			ClassicAssert.AreEqual(KingdomFoundingReceiptNormalization.Quarantine,
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
