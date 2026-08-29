#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The zone-levelling pass's giver/taker choice
	/// (<see cref="KingdomNetworkRules.TrySelectLevellingPair"/>), proven directly rather than by
	/// scanning source text for a call literal. This is the pure half of the fix for the
	/// conservation gap in <c>KingdomNetworks.Attend</c>: a mixed-purity network's levelling loop
	/// must never offer a brine or otherwise incompatible store as donor or landing candidate.
	/// </summary>
	public class KingdomNetworkPurityRulesTests
	{
		private static KingdomNetworkStoreLevel Level(int volume, int maxVolume, bool receivable)
		{
			return new KingdomNetworkStoreLevel(volume, maxVolume, receivable);
		}

		[Test]
		public void RuntimeFeedsTheExactFreshWaterPredicateAndMeasuresBothMutations()
		{
			string source = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomNetworks.AttendanceAndStar.cs");
			StringAssert.Contains("KingdomLiquids.CanReceiveFreshWater(store)", source);
			StringAssert.Contains("KingdomLiquids.Drain(fullest, want)", source);
			StringAssert.Contains("KingdomLiquids.Fill(emptiest, \"water\", drawn)", source);
			StringAssert.Contains("KingdomLiquids.Fill(fullest, \"water\", drawn - landed)", source);
		}

		/// <summary>
		/// The whole point of the guard. A brine store that reads completely empty is, by fill
		/// fraction alone, the most attractive possible landing candidate — and is exactly the one
		/// the selection must refuse. A half-full but receivable store must win instead, even
		/// though it is the comparatively FULLER of the two candidates the naive fill-fraction rule
		/// would have compared.
		/// </summary>
		[Test]
		public void TheReceivableCandidateWinsOverAFullerButUnreceivableCandidate()
		{
			KingdomNetworkStoreLevel[] stores = new KingdomNetworkStoreLevel[]
			{
				Level(volume: 100, maxVolume: 100, receivable: true),  // 0: the giver
				Level(volume: 0,   maxVolume: 100, receivable: false), // 1: brine, reads emptiest, must be refused
				Level(volume: 50,  maxVolume: 100, receivable: true),  // 2: half-full, but the only lawful taker
			};
			int fullestIndex;
			int emptiestIndex;
			Assert.IsTrue(KingdomNetworkRules.TrySelectLevellingPair(stores, stores.Length,
				out fullestIndex, out emptiestIndex));
			Assert.AreEqual(0, fullestIndex, "the full pure store is the giver");
			Assert.AreEqual(2, emptiestIndex,
				"the receivable half-full store must be chosen over the emptier, unreceivable brine store");
		}

		/// <summary>
		/// A network whose every store refuses fresh water fills nothing at all — not the fullest
		/// store, not the least-full one, nothing. The caller must not report a fill that never
		/// happened.
		/// </summary>
		[Test]
		public void ANetworkOfEntirelyUnreceivableStoresFillsNothing()
		{
			KingdomNetworkStoreLevel[] stores = new KingdomNetworkStoreLevel[]
			{
				Level(volume: 90, maxVolume: 100, receivable: false),
				Level(volume: 10, maxVolume: 100, receivable: false),
				Level(volume: 50, maxVolume: 50,  receivable: false),
			};
			int fullestIndex;
			int emptiestIndex;
			Assert.IsFalse(KingdomNetworkRules.TrySelectLevellingPair(stores, stores.Length,
				out fullestIndex, out emptiestIndex));
			Assert.AreEqual(-1, fullestIndex);
			Assert.AreEqual(-1, emptiestIndex);
		}

		/// <summary>
		/// A store with no volume at all (the zero-<c>MaxVolume</c> stand-in for a null entry, or a
		/// genuine unbuilt vessel) is skipped exactly as the original loop skipped a null store —
		/// its receivability never enters into it.
		/// </summary>
		[Test]
		public void AZeroCapacityEntryIsSkippedRegardlessOfReceivability()
		{
			KingdomNetworkStoreLevel[] stores = new KingdomNetworkStoreLevel[]
			{
				default(KingdomNetworkStoreLevel),
				Level(volume: 100, maxVolume: 100, receivable: true),
				Level(volume: 0,   maxVolume: 100, receivable: true),
			};
			int fullestIndex;
			int emptiestIndex;
			Assert.IsTrue(KingdomNetworkRules.TrySelectLevellingPair(stores, stores.Length,
				out fullestIndex, out emptiestIndex));
			Assert.AreEqual(1, fullestIndex);
			Assert.AreEqual(2, emptiestIndex);
		}

		[TestCase(0)]
		[TestCase(1)]
		public void FewerThanTwoEntriesPicksNothing(int count)
		{
			KingdomNetworkStoreLevel[] stores = new KingdomNetworkStoreLevel[]
			{
				Level(volume: 100, maxVolume: 100, receivable: true),
			};
			int fullestIndex;
			int emptiestIndex;
			Assert.IsFalse(KingdomNetworkRules.TrySelectLevellingPair(stores, count,
				out fullestIndex, out emptiestIndex));
		}

		[Test]
		public void ANullArrayPicksNothing()
		{
			int fullestIndex;
			int emptiestIndex;
			Assert.IsFalse(KingdomNetworkRules.TrySelectLevellingPair(null, 0,
				out fullestIndex, out emptiestIndex));
		}

		[TestCase(-1)]
		[TestCase(3)]
		public void CountOutsideArrayBoundsIsTotalAndPicksNothing(int count)
		{
			KingdomNetworkStoreLevel[] stores = { Level(1, 2, true), Level(0, 2, true) };
			Assert.IsFalse(KingdomNetworkRules.TrySelectLevellingPair(stores, count,
				out int fullest, out int emptiest));
			Assert.AreEqual(-1, fullest); Assert.AreEqual(-1, emptiest);
		}

		[TestCase(-1, 100)]
		[TestCase(101, 100)]
		[TestCase(1, 0)]
		[TestCase(0, -1)]
		public void MalformedVolumeFailsClosedWithoutPartialIndices(int volume, int capacity)
		{
			KingdomNetworkStoreLevel[] stores =
			{
				Level(100, 100, true), Level(volume, capacity, true), Level(0, 100, true)
			};
			Assert.IsFalse(KingdomNetworkRules.TrySelectLevellingPair(stores, stores.Length,
				out int fullest, out int emptiest));
			Assert.AreEqual(-1, fullest); Assert.AreEqual(-1, emptiest);
		}

		[Test]
		public void UnreceivableFullStoreCannotDonateAndTransmuteItsContents()
		{
			KingdomNetworkStoreLevel[] stores =
			{
				Level(100, 100, false), Level(80, 100, true), Level(0, 100, true)
			};
			Assert.IsTrue(KingdomNetworkRules.TrySelectLevellingPair(stores, stores.Length,
				out int fullest, out int emptiest));
			Assert.AreEqual(1, fullest); Assert.AreEqual(2, emptiest);
		}

		/// <summary>
		/// Levelling only. Two stores at the same fill fraction, or a "fullest" that is not
		/// actually fuller than the chosen "emptiest" by fill fraction, must pick nothing — a main
		/// pushing a cask past the one it draws from would be running uphill.
		/// </summary>
		[Test]
		public void EqualFillFractionsPickNothingEvenWhenBothAreReceivable()
		{
			KingdomNetworkStoreLevel[] stores = new KingdomNetworkStoreLevel[]
			{
				Level(volume: 50, maxVolume: 100, receivable: true),
				Level(volume: 25, maxVolume: 50,  receivable: true),
			};
			int fullestIndex;
			int emptiestIndex;
			Assert.IsFalse(KingdomNetworkRules.TrySelectLevellingPair(stores, stores.Length,
				out fullestIndex, out emptiestIndex));
		}

		/// <summary>
		/// The mutation-resistant heart of the fix: on a single-purity network — every store either
		/// pure or empty, exactly the case that shipped before this guard existed — the guard must
		/// never change the outcome. This mirrors the pre-fix loop (fill-fraction comparison, no
		/// receivability check at all) as an independent reference and asserts the guarded
		/// selection agrees with it on every case, receivability included only because a
		/// single-purity network makes it universally true.
		/// </summary>
		[TestCase(new int[] { 100, 0 }, new int[] { 100, 100 }, true, 0, 1)]
		[TestCase(new int[] { 30, 90, 10 }, new int[] { 100, 100, 100 }, true, 1, 2)]
		[TestCase(new int[] { 0, 100, 40 }, new int[] { 50, 100, 100 }, true, 1, 0)]
		[TestCase(new int[] { 20, 20 }, new int[] { 100, 100 }, false, -1, -1)]
		public void SinglePurityNetworksMatchThePreGuardSelectionExactly(
			int[] volumes, int[] maxVolumes, bool expectSelected, int expectedFullest, int expectedEmptiest)
		{
			KingdomNetworkStoreLevel[] stores = new KingdomNetworkStoreLevel[volumes.Length];
			for (int i = 0; i < volumes.Length; i++)
			{
				stores[i] = Level(volumes[i], maxVolumes[i], receivable: true);
			}
			int referenceFullest;
			int referenceEmptiest;
			bool referenceSelected = NaivePreGuardSelection(volumes, maxVolumes, out referenceFullest, out referenceEmptiest);
			Assert.AreEqual(expectSelected, referenceSelected, "test case's own reference disagrees with its expectation");
			if (expectSelected)
			{
				Assert.AreEqual(expectedFullest, referenceFullest);
				Assert.AreEqual(expectedEmptiest, referenceEmptiest);
			}

			int fullestIndex;
			int emptiestIndex;
			bool selected = KingdomNetworkRules.TrySelectLevellingPair(stores, stores.Length,
				out fullestIndex, out emptiestIndex);
			Assert.AreEqual(referenceSelected, selected);
			if (selected)
			{
				Assert.AreEqual(referenceFullest, fullestIndex);
				Assert.AreEqual(referenceEmptiest, emptiestIndex);
			}
		}

		/// <summary>
		/// A direct transcription of the original <c>KingdomNetworks.Attend</c> loop, before this
		/// fix, kept here only as an independent oracle for the parity test above — it must never
		/// be reused by production code, and it deliberately has no receivability concept at all.
		/// </summary>
		private static bool NaivePreGuardSelection(int[] volumes, int[] maxVolumes,
			out int fullestIndex, out int emptiestIndex)
		{
			fullestIndex = -1;
			emptiestIndex = -1;
			for (int i = 0; i < volumes.Length; i++)
			{
				if (maxVolumes[i] <= 0)
				{
					continue;
				}
				if (fullestIndex < 0 || (long)volumes[i] * maxVolumes[fullestIndex]
					> (long)volumes[fullestIndex] * maxVolumes[i])
				{
					fullestIndex = i;
				}
				if (emptiestIndex < 0 || (long)volumes[i] * maxVolumes[emptiestIndex]
					< (long)volumes[emptiestIndex] * maxVolumes[i])
				{
					emptiestIndex = i;
				}
			}
			if (fullestIndex < 0 || emptiestIndex < 0 || fullestIndex == emptiestIndex)
			{
				return false;
			}
			if ((long)volumes[fullestIndex] * maxVolumes[emptiestIndex]
				<= (long)volumes[emptiestIndex] * maxVolumes[fullestIndex])
			{
				return false;
			}
			return true;
		}
	}
}
#endif
