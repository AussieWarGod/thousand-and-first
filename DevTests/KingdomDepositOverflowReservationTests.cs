#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The distinct-ground reservation, driven without a game. This is the regression for a real
	/// defect: three cases each asked a "first bare cell in the zone" helper for ground, before any
	/// of them had placed anything, and all three were handed the SAME cell. Nothing failed at the
	/// time &mdash; each case simply inherited the previous one's hold, which for a suite about
	/// totals silently changes the number under test.
	/// </summary>
	public class KingdomDepositOverflowReservationTests
	{
		/// <summary>The defect exactly: an unchanging candidate stream. Reserving from it three
		/// times must yield three different pieces of ground, or fail outright rather than repeat
		/// one.</summary>
		[Test]
		public void ReservingFromAnUnchangingScanNeverHandsOutTheSameGroundTwice()
		{
			long[] scan = { 11L, 12L, 13L };
			List<long> taken = new List<long>();
			List<long> picked = new List<long>();
			for (int i = 0; i < 3; i++)
			{
				long reserved;
				ClassicAssert.IsTrue(
					KingdomDepositOverflowReservation.TryReserve(taken, scan, out reserved),
					"reservation " + i + " found no free ground");
				picked.Add(reserved);
			}
			ClassicAssert.AreEqual(new[] { 11L, 12L, 13L }, picked.ToArray());
			ClassicAssert.IsTrue(KingdomDepositOverflowReservation.AllDistinct(picked));
		}

		/// <summary>The shape of the original bug: a helper that always answers with the first
		/// candidate. The reservation refuses rather than repeating, and the distinctness check
		/// rejects the picks it would have produced.</summary>
		[Test]
		public void AHelperThatAlwaysAnswersWithTheFirstCellIsRefusedRatherThanRepeated()
		{
			long[] alwaysFirst = { 11L };
			List<long> taken = new List<long>();
			long reserved;
			ClassicAssert.IsTrue(
				KingdomDepositOverflowReservation.TryReserve(taken, alwaysFirst, out reserved));
			ClassicAssert.AreEqual(11L, reserved);
			ClassicAssert.IsFalse(
				KingdomDepositOverflowReservation.TryReserve(taken, alwaysFirst, out reserved),
				"ground already reserved must never be handed out a second time");
			ClassicAssert.AreEqual(0L, reserved, "a refused reservation names no ground");
			ClassicAssert.IsFalse(
				KingdomDepositOverflowReservation.AllDistinct(new[] { 11L, 11L, 11L }),
				"three picks of one cell is exactly the defect and must not read as distinct");
		}

		/// <summary>
		/// The key names a cell and nothing else, and it is INJECTIVE over every coordinate a zone
		/// has: no two cells in the eighty-by-twenty-five grid share a key. That is the property a
		/// hash could not have offered, and a colliding key is exactly the defect this helper
		/// exists for &mdash; two cases handed the same ground.
		/// </summary>
		[Test]
		public void EveryCellInAZoneKeysApartFromEveryOtherOne()
		{
			HashSet<long> keys = new HashSet<long>();
			for (int y = 0; y < 25; y++)
				for (int x = 0; x < 80; x++)
					ClassicAssert.IsTrue(
						keys.Add(KingdomDepositOverflowReservation.Key(x, y)),
						"cell " + x + "," + y + " collided with an earlier cell");
			ClassicAssert.AreEqual(2000, keys.Count);
		}

		/// <summary>Two cells that a naive packing would fold together stay apart, and the same
		/// cell keys the same way twice.</summary>
		[TestCase(3, 4, 4, 3)]
		[TestCase(0, 1, 1, 0)]
		[TestCase(65536, 0, 0, 65536)]
		[TestCase(-1, 0, 0, -1)]
		public void TransposedAndSignedCoordinatesNeverShareAKey(int ax, int ay, int bx, int by)
		{
			long a = KingdomDepositOverflowReservation.Key(ax, ay);
			long b = KingdomDepositOverflowReservation.Key(bx, by);
			ClassicAssert.AreNotEqual(a, b);
			ClassicAssert.AreEqual(a, KingdomDepositOverflowReservation.Key(ax, ay),
				"the same cell must key the same way twice");
			ClassicAssert.IsTrue(KingdomDepositOverflowReservation.AllDistinct(new[] { a, b }));
		}

		/// <summary>Nothing to reserve from, and nothing to check.</summary>
		[Test]
		public void AbsentInputsAreRefusedRatherThanGuessed()
		{
			ClassicAssert.IsFalse(KingdomDepositOverflowReservation.AllDistinct(null));
			List<long> taken = null;
			long reserved;
			ClassicAssert.IsFalse(
				KingdomDepositOverflowReservation.TryReserve(taken, new[] { 1L }, out reserved));
			ClassicAssert.IsFalse(KingdomDepositOverflowReservation.TryReserve(
				new List<long>(), null, out reserved));
			ClassicAssert.AreEqual(0L, reserved);
		}
	}
}
#endif
