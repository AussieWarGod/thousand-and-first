#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomPetitionLifecycleTests
	{
		[Test]
		public void LifecycleOrdinals_AreAppendOnlySaveValues()
		{
			Assert.AreEqual(0, (int)PetitionLifecycle.None);
			Assert.AreEqual(1, (int)PetitionLifecycle.Offered);
			Assert.AreEqual(2, (int)PetitionLifecycle.Accepted);
			Assert.AreEqual(3, (int)PetitionLifecycle.Declined);
			Assert.AreEqual(4, (int)PetitionLifecycle.Resolved);
			Assert.AreEqual(5, (int)PetitionLifecycle.Expired);
		}

		[Test]
		public void TransitionMatrix_IsExact()
		{
			foreach (PetitionLifecycle from in Enum.GetValues(typeof(PetitionLifecycle)))
			{
				foreach (PetitionLifecycle to in Enum.GetValues(typeof(PetitionLifecycle)))
				{
					bool expected = (to == PetitionLifecycle.Offered
						&& (from == PetitionLifecycle.None || KingdomPetitionRules.IsTerminal(from)))
						|| (from == PetitionLifecycle.Offered
							&& (to == PetitionLifecycle.Accepted || to == PetitionLifecycle.Declined
								|| to == PetitionLifecycle.Expired))
						|| (from == PetitionLifecycle.Accepted
							&& (to == PetitionLifecycle.Resolved || to == PetitionLifecycle.Expired));
					Assert.AreEqual(expected, KingdomPetitionRules.CanTransition(from, to),
						from + " -> " + to);
				}
			}
		}

		[Test]
		public void CalendarBuckets_MatchEveryQudMonthBoundaryIncludingUtYara()
		{
			long[] starts = new long[13]
			{
				0L, 36001L, 72001L, 108001L, 144001L, 180001L, 216001L,
				222001L, 258001L, 294001L, 330001L, 366001L, 402001L
			};
			for (int month = 0; month < starts.Length; month++)
			{
				Assert.AreEqual(month, KingdomPetitionRules.CanonicalMonthOrdinal(starts[month]));
				if (month > 0)
				{
					Assert.AreEqual(month - 1,
						KingdomPetitionRules.CanonicalMonthOrdinal(starts[month] - 1L));
				}
			}
			Assert.AreEqual(12L, KingdomPetitionRules.CanonicalMonthOrdinal(437999L));
			Assert.AreEqual(13L, KingdomPetitionRules.CanonicalMonthOrdinal(438000L));
			Assert.AreEqual(19L, KingdomPetitionRules.CanonicalMonthOrdinal(438000L + 216001L));
		}

		[Test]
		public void CalendarOrdinal_IsMonotoneAcrossTwoWholeYears()
		{
			long previous = -1L;
			for (long tick = 0L; tick <= KingdomPetitionRules.TicksPerYear * 2L; tick += 97L)
			{
				long current = KingdomPetitionRules.CanonicalMonthOrdinal(tick);
				Assert.GreaterOrEqual(current, previous);
				previous = current;
			}
		}

		[Test]
		public void OfferGate_AllowsAtMostOneOfferInEachCanonicalMonth()
		{
			long offered = KingdomPetitionRules.CanonicalMonthOrdinal(216001L);
			Assert.IsFalse(KingdomPetitionRules.CanOffer(216001L, offered, 0L,
				PetitionLifecycle.Declined, KingdomRules.PetitionKind.None));
			Assert.IsFalse(KingdomPetitionRules.CanOffer(222000L, offered, 0L,
				PetitionLifecycle.Expired, KingdomRules.PetitionKind.None));
			Assert.IsTrue(KingdomPetitionRules.CanOffer(222001L, offered, 0L,
				PetitionLifecycle.Resolved, KingdomRules.PetitionKind.None));
		}

		[Test]
		public void OfferGate_UsesLegacyTickWhenNewMonthFieldIsAbsent()
		{
			Assert.IsFalse(KingdomPetitionRules.CanOffer(50000L, -1L, 40000L,
				PetitionLifecycle.None, KingdomRules.PetitionKind.None));
			Assert.IsTrue(KingdomPetitionRules.CanOffer(72001L, -1L, 40000L,
				PetitionLifecycle.None, KingdomRules.PetitionKind.None));
		}

		[Test]
		public void ActivePetition_AlwaysBlocksAnotherOffer()
		{
			Assert.IsFalse(KingdomPetitionRules.CanOffer(999999L, -1L, 0L,
				PetitionLifecycle.Offered, KingdomRules.PetitionKind.Thirst));
			Assert.IsFalse(KingdomPetitionRules.CanOffer(999999L, -1L, 0L,
				PetitionLifecycle.Accepted, KingdomRules.PetitionKind.Thirst));
		}

		[Test]
		public void EvidenceCannotResolveBeforeAcceptance()
		{
			foreach (PetitionLifecycle state in Enum.GetValues(typeof(PetitionLifecycle)))
			{
				bool expected = state == PetitionLifecycle.Accepted;
				Assert.AreEqual(expected, KingdomPetitionRules.CanResolve(state,
					KingdomRules.PetitionKind.Thirst, 10, 999, 999, 0, 999, true), state.ToString());
			}
		}

		[Test]
		public void ShelterTarget_DoesNotMoveWhenPopulationLaterChanges()
		{
			int target = KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Shelter, 8);
			Assert.AreEqual(9, target);
			Assert.IsFalse(KingdomPetitionRules.IsMet(KingdomRules.PetitionKind.Shelter,
				target, 0, 8, 0, 0, false));
			Assert.IsTrue(KingdomPetitionRules.IsMet(KingdomRules.PetitionKind.Shelter,
				target, 0, 9, 0, 0, false));
		}

		[Test]
		public void EveryPetitionKindHasStableTargetSemantics()
		{
			Assert.Greater(KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Thirst, 8), 0);
			Assert.AreEqual(-100, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Peace, 8));
			Assert.AreEqual(0, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Craft, 8));
			Assert.AreEqual(1, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Memorial, 8));
			Assert.AreEqual(1, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Flesh, 8));
			Assert.AreEqual(1, KingdomPetitionRules.SnapshotTarget(KingdomRules.PetitionKind.Chrome, 8));
		}

		[Test]
		public void Expiry_IsExactAndOverflowSafe()
		{
			Assert.IsFalse(KingdomPetitionRules.IsExpired(25000L, 1000L, 24000L));
			Assert.IsTrue(KingdomPetitionRules.IsExpired(25001L, 1000L, 24000L));
			Assert.IsFalse(KingdomPetitionRules.IsExpired(long.MaxValue, long.MaxValue - 10L, 10L));
			Assert.IsTrue(KingdomPetitionRules.IsExpired(long.MaxValue, long.MaxValue - 11L, 10L));
		}

		[Test]
		public void OriginMatch_IsStrictAndNullSafe()
		{
			Assert.IsTrue(KingdomPetitionRules.OriginMatches("taf:city:a", "taf:city:a"));
			Assert.IsFalse(KingdomPetitionRules.OriginMatches("taf:city:a", "taf:city:b"));
			Assert.IsFalse(KingdomPetitionRules.OriginMatches(null, "taf:city:a"));
			Assert.IsFalse(KingdomPetitionRules.OriginMatches("", ""));
		}

		[Test]
		public void LegacyActivePetition_MigratesToOfferedNeverAccepted()
		{
			Assert.AreEqual(PetitionLifecycle.Offered,
				KingdomPetitionRules.NormalizeLegacy(PetitionLifecycle.None,
					KingdomRules.PetitionKind.Thirst));
			Assert.AreNotEqual(PetitionLifecycle.Accepted,
				KingdomPetitionRules.NormalizeLegacy(PetitionLifecycle.Resolved,
					KingdomRules.PetitionKind.Flesh));
			Assert.AreEqual(PetitionLifecycle.Expired,
				KingdomPetitionRules.NormalizeLegacy(PetitionLifecycle.Accepted,
					KingdomRules.PetitionKind.None));
			Assert.AreEqual(PetitionLifecycle.Offered,
				KingdomPetitionRules.NormalizeLegacy((PetitionLifecycle)255,
					KingdomRules.PetitionKind.Thirst));
			Assert.AreEqual(PetitionLifecycle.None,
				KingdomPetitionRules.NormalizeLegacy((PetitionLifecycle)255,
					KingdomRules.PetitionKind.None));
		}

		[Test]
		public void CorruptTargets_AreRepairedOnlyWhereTheyCouldInventOrEraseTruth()
		{
			Assert.IsTrue(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Thirst, -1));
			Assert.IsTrue(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Shelter, 0));
			Assert.IsTrue(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Peace, 0));
			Assert.IsFalse(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Peace, -100));
			Assert.IsFalse(KingdomPetitionRules.TargetNeedsRepair(KingdomRules.PetitionKind.Craft, 0));
		}
	}
}
#endif
