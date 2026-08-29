using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSaveSystemRosterRuntimePlanTests
	{
		[Test]
		public void LegacyBootstrapEnsuresMandatoryAndOnlyObservedOptionals()
		{
			KingdomSaveSystemRosterCounts counts = Counts(1, 1, 0, 1, 0);
			KingdomSaveSystemRosterRuntimePlan plan =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.LegacyDecodedRealm, false, 0, counts);
			Assert.That(plan.Decision.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.Bootstrap));
			Assert.That(plan.EnsureMask, Is.EqualTo(
				KingdomSaveSystemRosterRules.MandatoryMask
					| KingdomSaveSystemRosterRules.SuccessionBit));

			KingdomSaveSystemRosterCounts post = Counts(1, 1, 1, 1, 0);
			Assert.That(plan.ExactAfterEnsure(post, out KingdomSaveSystemRosterSystem system,
				out int expected, out int actual, out string failure), Is.True, failure);
			Assert.That(system, Is.EqualTo(KingdomSaveSystemRosterSystem.None));
			Assert.That(expected, Is.Zero);
			Assert.That(actual, Is.Zero);
		}

		[Test]
		public void EveryMissingExpectedCarrierProducesTheWholeFrozenRecoveryMask()
		{
			int raw = Encode(KingdomSaveSystemRosterRules.KnownMask);
			for (int missing = 1; missing <= 5; missing++)
			{
				KingdomSaveSystemRosterCounts counts = Counts(1, 1, 1, 1, 1);
				Set(counts, (KingdomSaveSystemRosterSystem)missing, 0);
				KingdomSaveSystemRosterRuntimePlan plan =
					KingdomSaveSystemRosterRuntimePlan.Create(
						KingdomSaveSystemRosterContext.UnprovenAbsence, true, raw, counts);
				Assert.That(plan.RecoveryRequired, Is.True);
				Assert.That(plan.EnsureMask,
					Is.EqualTo(KingdomSaveSystemRosterRules.KnownMask));
				Assert.That(plan.Decision.System,
					Is.EqualTo((KingdomSaveSystemRosterSystem)missing));
			}
		}

		[Test]
		public void UnreadableMarkerCanCreateOnlyMandatoryRecoveryShells()
		{
			int[] raw =
			{
				0, -1, 2 << KingdomSaveSystemRosterRules.VersionShift,
				(1 << KingdomSaveSystemRosterRules.VersionShift)
					| KingdomSaveSystemRosterRules.KnownMask | 32
			};
			for (int i = 0; i < raw.Length; i++)
			{
				KingdomSaveSystemRosterRuntimePlan plan =
					KingdomSaveSystemRosterRuntimePlan.Create(
						KingdomSaveSystemRosterContext.UnprovenAbsence, true, raw[i],
						new KingdomSaveSystemRosterCounts());
				Assert.That(plan.RecoveryRequired, Is.True);
				Assert.That(plan.EnsureMask,
					Is.EqualTo(KingdomSaveSystemRosterRules.MandatoryMask));
			}
		}

		[Test]
		public void PostEnsureProofRejectsEveryMissingExtraDuplicateAndNegativeCount()
		{
			KingdomSaveSystemRosterRuntimePlan plan =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.ExplicitNewGame, false, 0,
					Counts(0, 0, 0, 1, 0));
			Assert.That(plan.EnsureMask, Is.EqualTo(15));

			for (int system = 1; system <= 5; system++)
			{
				int expected = system <= 4 ? 1 : 0;
				KingdomSaveSystemRosterCounts wrong = Counts(1, 1, 1, 1, 0);
				Set(wrong, (KingdomSaveSystemRosterSystem)system, expected == 0 ? 1 : 0);
				Assert.That(plan.ExactAfterEnsure(wrong,
					out KingdomSaveSystemRosterSystem named, out int wanted,
					out int actual, out string failure), Is.False);
				Assert.That(named, Is.EqualTo((KingdomSaveSystemRosterSystem)system));
				Assert.That(wanted, Is.EqualTo(expected));
				Assert.That(actual, Is.EqualTo(expected == 0 ? 1 : 0));
				StringAssert.Contains("post-require", failure);
			}

			KingdomSaveSystemRosterCounts duplicate = Counts(2, 1, 1, 1, 0);
			Assert.That(plan.ExactAfterEnsure(duplicate,
				out KingdomSaveSystemRosterSystem duplicateSystem, out int _, out int duplicateCount,
				out string _), Is.False);
			Assert.That(duplicateSystem, Is.EqualTo(KingdomSaveSystemRosterSystem.Realm));
			Assert.That(duplicateCount, Is.EqualTo(2));

			KingdomSaveSystemRosterCounts negative = Counts(1, -1, 1, 1, 0);
			Assert.That(plan.ExactAfterEnsure(negative,
				out KingdomSaveSystemRosterSystem negativeSystem, out int _, out int negativeCount,
				out string _), Is.False);
			Assert.That(negativeSystem, Is.EqualTo(KingdomSaveSystemRosterSystem.Seal));
			Assert.That(negativeCount, Is.EqualTo(-1));
		}

		[Test]
		public void VerifiedAndPreparedPlansNeverRequestShells()
		{
			int raw = Encode(KingdomSaveSystemRosterRules.MandatoryMask);
			KingdomSaveSystemRosterRuntimePlan verified =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.UnprovenAbsence, true, raw,
					Counts(1, 1, 1, 0, 0));
			Assert.That(verified.Decision.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.Verified));
			Assert.That(verified.EnsureMask, Is.Zero);

			KingdomSaveSystemRosterRuntimePlan removal =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.PreparedRemoval, true, raw,
					Counts(1, 1, 1, 0, 0));
			Assert.That(removal.Decision.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.ClearForPreparedRemoval));
			Assert.That(removal.EnsureMask, Is.Zero);

			KingdomSaveSystemRosterRuntimePlan absent =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.PreparedRemoval, false, 0,
					new KingdomSaveSystemRosterCounts());
			Assert.That(absent.Decision.Disposition,
				Is.EqualTo(KingdomSaveSystemRosterDisposition.LeaveAbsent));
			Assert.That(absent.EnsureMask, Is.Zero);
		}

		[Test]
		public void PlanAndEmptyPredicatesDoNotRetainMutableCounts()
		{
			KingdomSaveSystemRosterCounts counts = Counts(0, 0, 0, 1, 0);
			KingdomSaveSystemRosterRuntimePlan plan =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.ExplicitNewGame, false, 0, counts);
			counts.Succession = 0;
			Assert.That(plan.EnsureMask, Is.EqualTo(15));
			Assert.That(KingdomSaveSystemRosterRuntimePlan.Empty(counts), Is.True);
			Assert.That(KingdomSaveSystemRosterRuntimePlan.Empty(null), Is.False);
		}

		private static int Encode(int mask)
		{
			Assert.That(KingdomSaveSystemRosterRules.TryEncode(mask,
				out int raw, out string failure), Is.True, failure);
			return raw;
		}

		private static KingdomSaveSystemRosterCounts Counts(int realm, int seal,
			int memory, int succession, int inheritance)
		{
			return new KingdomSaveSystemRosterCounts
			{
				Realm = realm, Seal = seal, CivicMemory = memory,
				Succession = succession, Inheritance = inheritance
			};
		}

		private static void Set(KingdomSaveSystemRosterCounts counts,
			KingdomSaveSystemRosterSystem system, int value)
		{
			switch (system)
			{
				case KingdomSaveSystemRosterSystem.Realm: counts.Realm = value; break;
				case KingdomSaveSystemRosterSystem.Seal: counts.Seal = value; break;
				case KingdomSaveSystemRosterSystem.CivicMemory: counts.CivicMemory = value; break;
				case KingdomSaveSystemRosterSystem.Succession: counts.Succession = value; break;
				case KingdomSaveSystemRosterSystem.Inheritance: counts.Inheritance = value; break;
			}
		}
	}
}
