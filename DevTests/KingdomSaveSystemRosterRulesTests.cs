using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSaveSystemRosterRulesTests
	{
		private static readonly KingdomSaveSystemRosterSystem[] Systems =
		{
			KingdomSaveSystemRosterSystem.Realm,
			KingdomSaveSystemRosterSystem.Seal,
			KingdomSaveSystemRosterSystem.CivicMemory,
			KingdomSaveSystemRosterSystem.Succession,
			KingdomSaveSystemRosterSystem.Inheritance
		};

		[Test]
		public void WireKeyVersionAndBitsAreExact()
		{
			Assert.That(KingdomSaveSystemRosterRules.StateKey,
				Is.EqualTo("r_TAF_SaveSystemRoster_v1"));
			Assert.That(KingdomSaveSystemRosterRules.CurrentVersion, Is.EqualTo(1));
			Assert.That(KingdomSaveSystemRosterRules.VersionShift, Is.EqualTo(16));
			Assert.That(KingdomSaveSystemRosterRules.MaskBits, Is.EqualTo(16));
			Assert.That(KingdomSaveSystemRosterRules.RealmBit, Is.EqualTo(1));
			Assert.That(KingdomSaveSystemRosterRules.SealBit, Is.EqualTo(2));
			Assert.That(KingdomSaveSystemRosterRules.CivicMemoryBit, Is.EqualTo(4));
			Assert.That(KingdomSaveSystemRosterRules.SuccessionBit, Is.EqualTo(8));
			Assert.That(KingdomSaveSystemRosterRules.InheritanceBit, Is.EqualTo(16));
			Assert.That(KingdomSaveSystemRosterRules.MandatoryMask, Is.EqualTo(7));
			Assert.That(KingdomSaveSystemRosterRules.OptionalMask, Is.EqualTo(24));
			Assert.That(KingdomSaveSystemRosterRules.KnownMask, Is.EqualTo(31));
			Assert.That(Encode(7), Is.EqualTo(65543));
			Assert.That(Encode(31), Is.EqualTo(65567));
		}

		[Test]
		public void EveryLowSixteenBitMaskHasOneExactVerdict()
		{
			int accepted = 0;
			for (int mask = 0; mask <= ushort.MaxValue; mask++)
			{
				bool expected = (mask & ~KingdomSaveSystemRosterRules.KnownMask) == 0
					&& (mask & KingdomSaveSystemRosterRules.MandatoryMask)
						== KingdomSaveSystemRosterRules.MandatoryMask;
				bool encoded = KingdomSaveSystemRosterRules.TryEncode(mask,
					out int raw, out string failure);
				Assert.That(encoded, Is.EqualTo(expected), "mask " + mask + ": " + failure);
				if (!encoded) continue;
				accepted++;
				Assert.That(KingdomSaveSystemRosterRules.TryDecode(raw,
					out int version, out int decoded, out KingdomSaveSystemRosterFault fault,
					out KingdomSaveSystemRosterSystem system, out failure), Is.True, failure);
				Assert.That(version, Is.EqualTo(1));
				Assert.That(decoded, Is.EqualTo(mask));
				Assert.That(fault, Is.EqualTo(KingdomSaveSystemRosterFault.None));
				Assert.That(system, Is.EqualTo(KingdomSaveSystemRosterSystem.None));
			}
			Assert.That(accepted, Is.EqualTo(4),
				"v1 permits mandatory plus either independent optional bit");
		}

		[Test]
		public void NonPositivePastFutureUnknownAndAdditiveMarkersFailByFirstCause()
		{
			AssertDecodeFault(0, KingdomSaveSystemRosterFault.NonPositiveMarker);
			AssertDecodeFault(-1, KingdomSaveSystemRosterFault.NonPositiveMarker);
			AssertDecodeFault(KingdomSaveSystemRosterRules.MandatoryMask,
				KingdomSaveSystemRosterFault.UnsupportedMarkerVersion);
			int future = (2 << 16) | KingdomSaveSystemRosterRules.KnownMask;
			AssertDecodeFault(future, KingdomSaveSystemRosterFault.FutureMarkerVersion);
			AssertDecodeFault(future | (1 << 5),
				KingdomSaveSystemRosterFault.FutureMarkerVersion);
			AssertDecodeFault((1 << 16) | KingdomSaveSystemRosterRules.KnownMask | (1 << 5),
				KingdomSaveSystemRosterFault.UnknownSystemBits);
		}

		[Test]
		public void EveryMissingMandatoryBitNamesFirstMissingSystem()
		{
			KingdomSaveSystemRosterSystem[] mandatory =
			{
				KingdomSaveSystemRosterSystem.Realm,
				KingdomSaveSystemRosterSystem.Seal,
				KingdomSaveSystemRosterSystem.CivicMemory
			};
			int[] bits =
			{
				KingdomSaveSystemRosterRules.RealmBit,
				KingdomSaveSystemRosterRules.SealBit,
				KingdomSaveSystemRosterRules.CivicMemoryBit
			};
			for (int i = 0; i < mandatory.Length; i++)
			{
				int raw = (1 << 16) | (KingdomSaveSystemRosterRules.KnownMask & ~bits[i]);
				Assert.That(KingdomSaveSystemRosterRules.TryDecode(raw, out int _, out int _,
					out KingdomSaveSystemRosterFault fault,
					out KingdomSaveSystemRosterSystem system, out string _), Is.False);
				Assert.That(fault,
					Is.EqualTo(KingdomSaveSystemRosterFault.MarkerMissingMandatorySystem));
				Assert.That(system, Is.EqualTo(mandatory[i]));
			}
			Assert.That(KingdomSaveSystemRosterRules.TryDecode(1 << 16, out int _, out int _,
				out KingdomSaveSystemRosterFault _, out KingdomSaveSystemRosterSystem first,
				out string _), Is.False);
			Assert.That(first, Is.EqualTo(KingdomSaveSystemRosterSystem.Realm));
		}

		[Test]
		public void EveryMarkerExpectedSystemMissingRequiresRecovery()
		{
			int raw = Encode(KingdomSaveSystemRosterRules.KnownMask);
			for (int i = 0; i < Systems.Length; i++)
			{
				KingdomSaveSystemRosterCounts counts = Counts(
					KingdomSaveSystemRosterRules.KnownMask);
				Set(counts, Systems[i], 0);
				KingdomSaveSystemRosterDecision decision = Decide(raw, counts);
				Assert.That(decision.Disposition,
					Is.EqualTo(KingdomSaveSystemRosterDisposition.RecoveryRequired));
				Assert.That(decision.Fault,
					Is.EqualTo(KingdomSaveSystemRosterFault.MarkerExpectedSystemMissing));
				Assert.That(decision.System, Is.EqualTo(Systems[i]));
				Assert.That(decision.ExpectedCount, Is.EqualTo(1));
				Assert.That(decision.ActualCount, Is.EqualTo(0));
				StringAssert.Contains(Systems[i].ToString(), decision.Failure);
			}
		}

		[Test]
		public void EveryDuplicateAndNegativeCountIsRejectedInStableOrder()
		{
			int raw = Encode(KingdomSaveSystemRosterRules.KnownMask);
			for (int i = 0; i < Systems.Length; i++)
			{
				KingdomSaveSystemRosterCounts duplicate = Counts(
					KingdomSaveSystemRosterRules.KnownMask);
				Set(duplicate, Systems[i], 2);
				KingdomSaveSystemRosterDecision decision = Decide(raw, duplicate);
				Assert.That(decision.Fault,
					Is.EqualTo(KingdomSaveSystemRosterFault.UnexpectedMultiplicity));
				Assert.That(decision.System, Is.EqualTo(Systems[i]));
				Assert.That(decision.ExpectedCount, Is.EqualTo(1));
				Assert.That(decision.ActualCount, Is.EqualTo(2));

				KingdomSaveSystemRosterCounts negative = Counts(
					KingdomSaveSystemRosterRules.KnownMask);
				Set(negative, Systems[i], -1);
				decision = Decide(raw, negative);
				Assert.That(decision.Fault,
					Is.EqualTo(KingdomSaveSystemRosterFault.InvalidObservation));
				Assert.That(decision.System, Is.EqualTo(Systems[i]));
			}
			KingdomSaveSystemRosterCounts twoFaults = Counts(
				KingdomSaveSystemRosterRules.KnownMask);
			twoFaults.Realm = 2;
			twoFaults.Seal = 2;
			Assert.That(Decide(raw, twoFaults).System,
				Is.EqualTo(KingdomSaveSystemRosterSystem.Realm));
		}

		[Test]
		public void MarkerRejectsUnexpectedOptionalSystemNotNamedByItsMask()
		{
			int raw = Encode(KingdomSaveSystemRosterRules.MandatoryMask);
			KingdomSaveSystemRosterCounts succession = Counts(
				KingdomSaveSystemRosterRules.MandatoryMask);
			succession.Succession = 1;
			KingdomSaveSystemRosterDecision decision = Decide(raw, succession);
			Assert.That(decision.Fault,
				Is.EqualTo(KingdomSaveSystemRosterFault.UnexpectedMultiplicity));
			Assert.That(decision.System,
				Is.EqualTo(KingdomSaveSystemRosterSystem.Succession));
			Assert.That(decision.ExpectedCount, Is.EqualTo(0));
			Assert.That(decision.ActualCount, Is.EqualTo(1));

			KingdomSaveSystemRosterCounts inheritance = Counts(
				KingdomSaveSystemRosterRules.MandatoryMask);
			inheritance.Inheritance = 1;
			decision = Decide(raw, inheritance);
			Assert.That(decision.System,
				Is.EqualTo(KingdomSaveSystemRosterSystem.Inheritance));
		}

		[Test]
		public void AllFourLawfulV1MasksVerifyExactObservedRoster()
		{
			int[] masks = { 7, 15, 23, 31 };
			for (int i = 0; i < masks.Length; i++)
			{
				int raw = Encode(masks[i]);
				KingdomSaveSystemRosterDecision decision = Decide(raw, Counts(masks[i]));
				Assert.That(decision.Disposition,
					Is.EqualTo(KingdomSaveSystemRosterDisposition.Verified));
				Assert.That(decision.Fault, Is.EqualTo(KingdomSaveSystemRosterFault.None));
				Assert.That(decision.ExpectedMarkerRaw, Is.EqualTo(raw));
				Assert.That(decision.NextMarkerRaw, Is.EqualTo(raw));
			}
		}

		[Test]
		public void ExplicitNewGameBootstrapsMandatoryAndObservedOptionalSystems()
		{
			KingdomSaveSystemRosterCounts empty = new KingdomSaveSystemRosterCounts();
			KingdomSaveSystemRosterDecision mandatory = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.ExplicitNewGame, false, 0, empty);
			AssertBootstrap(mandatory, KingdomSaveSystemRosterRules.MandatoryMask);

			KingdomSaveSystemRosterCounts optional = new KingdomSaveSystemRosterCounts
			{
				Realm = 1,
				Seal = 1,
				CivicMemory = 1,
				Succession = 1,
				Inheritance = 1
			};
			AssertBootstrap(KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.ExplicitNewGame, false, 0, optional),
				KingdomSaveSystemRosterRules.KnownMask);
		}

		[Test]
		public void LegacyRealmAndSealPermitOnlyCivicMemoryBootstrapGap()
		{
			KingdomSaveSystemRosterCounts legacy = new KingdomSaveSystemRosterCounts
			{
				Realm = 1,
				Seal = 1,
				CivicMemory = 0,
				Succession = 1
			};
			AssertBootstrap(KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.LegacyDecodedRealm, false, 0, legacy),
				KingdomSaveSystemRosterRules.MandatoryMask
					| KingdomSaveSystemRosterRules.SuccessionBit);
			legacy.CivicMemory = 1;
			AssertBootstrap(KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.LegacyDecodedRealm, false, 0, legacy),
				KingdomSaveSystemRosterRules.MandatoryMask
					| KingdomSaveSystemRosterRules.SuccessionBit);

			legacy.Realm = 0;
			KingdomSaveSystemRosterDecision missing = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.LegacyDecodedRealm, false, 0, legacy);
			Assert.That(missing.Fault, Is.EqualTo(KingdomSaveSystemRosterFault.LegacyRealmMissing));
			legacy.Realm = 1;
			legacy.Seal = 0;
			missing = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.LegacyDecodedRealm, false, 0, legacy);
			Assert.That(missing.Fault, Is.EqualTo(KingdomSaveSystemRosterFault.LegacySealMissing));
		}

		[Test]
		public void PreparedRemovalLeavesAbsentMarkerAndClearsExactPresentMarker()
		{
			KingdomSaveSystemRosterDecision absent = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.PreparedRemoval, false, 0,
				new KingdomSaveSystemRosterCounts());
			Assert.That(absent.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.LeaveAbsent));
			Assert.That(absent.NextMarkerPresent, Is.False);

			int raw = Encode(KingdomSaveSystemRosterRules.MandatoryMask);
			KingdomSaveSystemRosterDecision present = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.PreparedRemoval, true, raw,
				Counts(KingdomSaveSystemRosterRules.MandatoryMask));
			Assert.That(present.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.ClearForPreparedRemoval));
			Assert.That(present.ExpectedMarkerPresent, Is.True);
			Assert.That(present.NextMarkerPresent, Is.False);
		}

		[Test]
		public void UnprovenAbsenceNeverMasqueradesAsNewOrLegacySave()
		{
			KingdomSaveSystemRosterDecision decision = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.UnprovenAbsence, false, 0,
				Counts(KingdomSaveSystemRosterRules.MandatoryMask));
			Assert.That(decision.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.RecoveryRequired));
			Assert.That(decision.Fault,
				Is.EqualTo(KingdomSaveSystemRosterFault.MissingMarkerUnproven));
			Assert.That(decision.Committable, Is.False);
			StringAssert.Contains("without new-game, legacy, or removal proof", decision.Failure);
		}

		[Test]
		public void DecisionsDetachInputsAndCasRequiresExactReadValue()
		{
			KingdomSaveSystemRosterCounts counts = new KingdomSaveSystemRosterCounts
			{
				Succession = 1
			};
			KingdomSaveSystemRosterDecision decision = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.ExplicitNewGame, false, 0, counts);
			KingdomSaveSystemRosterDecision clone = decision.Clone();
			counts.Succession = 0;
			Assert.That(clone, Is.Not.SameAs(decision));
			Assert.That(decision.NextMarkerRaw, Is.EqualTo(Encode(
				KingdomSaveSystemRosterRules.MandatoryMask
					| KingdomSaveSystemRosterRules.SuccessionBit)));

			Assert.That(KingdomSaveSystemRosterRules.TryResolveCas(decision,
				false, 0, out bool nextPresent, out int nextRaw,
				out KingdomSaveSystemRosterFault fault, out string failure), Is.True, failure);
			Assert.That(nextPresent, Is.True);
			Assert.That(nextRaw, Is.EqualTo(decision.NextMarkerRaw));
			Assert.That(fault, Is.EqualTo(KingdomSaveSystemRosterFault.None));

			Assert.That(KingdomSaveSystemRosterRules.TryResolveCas(decision,
				true, Encode(KingdomSaveSystemRosterRules.MandatoryMask),
				out nextPresent, out nextRaw, out fault, out failure), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomSaveSystemRosterFault.CasChanged));
		}

		[Test]
		public void CasSupportsNoOpAndRemovalButNeverCommitsRecoveryDecision()
		{
			int raw = Encode(KingdomSaveSystemRosterRules.MandatoryMask);
			KingdomSaveSystemRosterCounts exact = Counts(
				KingdomSaveSystemRosterRules.MandatoryMask);
			KingdomSaveSystemRosterDecision verified = Decide(raw, exact);
			Assert.That(KingdomSaveSystemRosterRules.TryResolveCas(verified, true, raw,
				out bool present, out int next, out KingdomSaveSystemRosterFault _,
				out string failure), Is.True, failure);
			Assert.That(present, Is.True);
			Assert.That(next, Is.EqualTo(raw));

			KingdomSaveSystemRosterDecision removal = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.PreparedRemoval, true, raw, exact);
			Assert.That(KingdomSaveSystemRosterRules.TryResolveCas(removal, true, raw,
				out present, out next, out KingdomSaveSystemRosterFault _, out failure),
				Is.True, failure);
			Assert.That(present, Is.False);
			Assert.That(next, Is.EqualTo(0));

			exact.CivicMemory = 0;
			KingdomSaveSystemRosterDecision recovery = Decide(raw, exact);
			Assert.That(KingdomSaveSystemRosterRules.TryResolveCas(recovery, true, raw,
				out present, out next, out KingdomSaveSystemRosterFault fault, out failure),
				Is.False);
			Assert.That(fault,
				Is.EqualTo(KingdomSaveSystemRosterFault.DecisionNotCommittable));
		}

		[Test]
		public void InvalidContextNullCountsAndMalformedMarkerStayFirstCauseFriendly()
		{
			KingdomSaveSystemRosterDecision invalid = KingdomSaveSystemRosterRules.Decide(
				(KingdomSaveSystemRosterContext)255, false, 0,
				new KingdomSaveSystemRosterCounts());
			Assert.That(invalid.Fault, Is.EqualTo(KingdomSaveSystemRosterFault.InvalidContext));
			invalid = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.ExplicitNewGame, false, 0, null);
			Assert.That(invalid.Fault,
				Is.EqualTo(KingdomSaveSystemRosterFault.InvalidObservation));
			KingdomSaveSystemRosterCounts duplicate = new KingdomSaveSystemRosterCounts
			{
				Realm = 2
			};
			invalid = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.UnprovenAbsence, true, 0, duplicate);
			Assert.That(invalid.Fault,
				Is.EqualTo(KingdomSaveSystemRosterFault.NonPositiveMarker),
				"malformed authenticated marker is diagnosed before secondary roster damage");
		}

		private static KingdomSaveSystemRosterDecision Decide(int Raw,
			KingdomSaveSystemRosterCounts Counts)
		{
			return KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.UnprovenAbsence, true, Raw, Counts);
		}

		private static int Encode(int Mask)
		{
			Assert.That(KingdomSaveSystemRosterRules.TryEncode(Mask,
				out int raw, out string failure), Is.True, failure);
			return raw;
		}

		private static void AssertDecodeFault(int Raw, KingdomSaveSystemRosterFault Expected)
		{
			Assert.That(KingdomSaveSystemRosterRules.TryDecode(Raw,
				out int _, out int _, out KingdomSaveSystemRosterFault fault,
				out KingdomSaveSystemRosterSystem _, out string _), Is.False);
			Assert.That(fault, Is.EqualTo(Expected));
		}

		private static void AssertBootstrap(KingdomSaveSystemRosterDecision Decision,
			int ExpectedMask)
		{
			Assert.That(Decision.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.Bootstrap));
			Assert.That(Decision.ExpectedMarkerPresent, Is.False);
			Assert.That(Decision.NextMarkerPresent, Is.True);
			Assert.That(Decision.NextMarkerRaw, Is.EqualTo(Encode(ExpectedMask)));
		}

		private static KingdomSaveSystemRosterCounts Counts(int Mask)
		{
			return new KingdomSaveSystemRosterCounts
			{
				Realm = (Mask & KingdomSaveSystemRosterRules.RealmBit) == 0 ? 0 : 1,
				Seal = (Mask & KingdomSaveSystemRosterRules.SealBit) == 0 ? 0 : 1,
				CivicMemory = (Mask & KingdomSaveSystemRosterRules.CivicMemoryBit) == 0 ? 0 : 1,
				Succession = (Mask & KingdomSaveSystemRosterRules.SuccessionBit) == 0 ? 0 : 1,
				Inheritance = (Mask & KingdomSaveSystemRosterRules.InheritanceBit) == 0 ? 0 : 1
			};
		}

		private static void Set(KingdomSaveSystemRosterCounts Counts,
			KingdomSaveSystemRosterSystem System, int Value)
		{
			switch (System)
			{
				case KingdomSaveSystemRosterSystem.Realm: Counts.Realm = Value; break;
				case KingdomSaveSystemRosterSystem.Seal: Counts.Seal = Value; break;
				case KingdomSaveSystemRosterSystem.CivicMemory: Counts.CivicMemory = Value; break;
				case KingdomSaveSystemRosterSystem.Succession: Counts.Succession = Value; break;
				case KingdomSaveSystemRosterSystem.Inheritance: Counts.Inheritance = Value; break;
			}
		}
	}
}
