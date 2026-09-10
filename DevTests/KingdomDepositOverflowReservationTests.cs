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

		/// <summary>Ground is a zone AND a cell. The same coordinates in another zone are
		/// different ground, and must never collide with it.</summary>
		[TestCase(0, 0)]
		[TestCase(40, 12)]
		[TestCase(79, 24)]
		public void TheSameCoordinatesInAnotherZoneAreNeverTheSameGround(int x, int y)
		{
			long here = KingdomDepositOverflowReservation.Key(1234, x, y);
			long there = KingdomDepositOverflowReservation.Key(5678, x, y);
			ClassicAssert.AreNotEqual(here, there);
			ClassicAssert.AreEqual(here, KingdomDepositOverflowReservation.Key(1234, x, y),
				"the same ground must key the same way twice");
			ClassicAssert.IsTrue(
				KingdomDepositOverflowReservation.AllDistinct(new[] { here, there }));
		}

		/// <summary>Two different cells in one zone are different ground too.</summary>
		[Test]
		public void TwoCellsInOneZoneKeyApart()
		{
			ClassicAssert.AreNotEqual(KingdomDepositOverflowReservation.Key(7, 3, 4),
				KingdomDepositOverflowReservation.Key(7, 4, 3));
			ClassicAssert.IsFalse(KingdomDepositOverflowReservation.AllDistinct(null));
			List<long> taken = null;
			long reserved;
			ClassicAssert.IsFalse(
				KingdomDepositOverflowReservation.TryReserve(taken, new[] { 1L }, out reserved));
		}
	}
}
#endif
