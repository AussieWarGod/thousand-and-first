using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomRoomEnclosureBoundsTests
	{
		private static KingdomAdoptRules.CellObservation Hall(int X, int Y)
		{
			return new KingdomAdoptRules.CellObservation(
				X == 10 && Y == 12 ? KingdomAdoptRules.EnclosureRegion.Ingress
				: X == 0 || X == 21 || Y == 0 || Y == 12 ? KingdomAdoptRules.EnclosureRegion.Shell
				: X < 0 || X > 21 || Y < 0 || Y > 12 ? KingdomAdoptRules.EnclosureRegion.Outside
				: KingdomAdoptRules.EnclosureRegion.Membership, true);
		}

		[Test]
		public void AdoptionRetainsItsDefaultBoundWhileDesignatedHallCanBeMeasured()
		{
			var adoption = KingdomAdoptRules.MeasureExactEnclosure(1, 1, Hall);
			ClassicAssert.IsFalse(adoption.Bounded);
			ClassicAssert.AreEqual(200, adoption.RoomCells);
			var designated = KingdomAdoptRules.MeasureExactEnclosure(1, 1, Hall, 220);
			ClassicAssert.IsTrue(designated.Bounded);
			ClassicAssert.AreEqual(220, designated.RoomCells);
			ClassicAssert.AreEqual(220, designated.UsableCells);
		}

		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(4001)]
		[TestCase(int.MaxValue)]
		public void InvalidBudgetsRefuseBeforeObservingGround(int Budget)
		{
			int reads = 0;
			var reading = KingdomAdoptRules.MeasureExactEnclosure(1, 1,
				(x, y) => { reads++; return Hall(x, y); }, Budget);
			ClassicAssert.IsFalse(reading.Bounded);
			ClassicAssert.AreEqual(0, reads);
		}
	}
}
