#if TAF_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;
using Rect = ThousandAndFirst.Harness.KingdomForageNativeGeometry.Rect;

namespace ThousandAndFirst.Tests
{
	/// <summary>Real execution against fakes, not source pins: every predicate the forage native
	/// fixture uses to keep planted objects out of plots and to witness a once-only ledger
	/// notice is engine-free and runs here directly.</summary>
	public class KingdomForageNativeGeometryTests
	{
		[TestCase(5, 5, true)]
		[TestCase(0, 0, true)]
		[TestCase(10, 10, true)]
		[TestCase(11, 5, false)]
		[TestCase(5, -1, false)]
		[TestCase(20, 20, true)]
		public void InsideAnyRectHonoursInclusiveBoundsAcrossMultipleRects(int x, int y, bool expected)
		{
			var rects = new List<Rect> { new Rect(0, 0, 10, 10), new Rect(15, 15, 25, 25) };
			Assert.That(KingdomForageNativeGeometry.InsideAnyRect(x, y, rects), Is.EqualTo(expected));
		}

		[Test]
		public void InsideAnyRectIsFalseForNoRectsOrNullList()
		{
			Assert.That(KingdomForageNativeGeometry.InsideAnyRect(0, 0, new List<Rect>()), Is.False);
			Assert.That(KingdomForageNativeGeometry.InsideAnyRect(0, 0, null), Is.False);
		}

		[Test]
		public void RingAtRadiusOneIsExactlyTheEightNeighbours()
		{
			var offsets = KingdomForageNativeGeometry.Ring(1).ToList();
			var expected = new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };
			Assert.That(offsets, Is.EquivalentTo(expected));
			Assert.That(offsets, Has.Count.EqualTo(8));
		}

		[Test]
		public void RingAtRadiusTwoExcludesRadiusOneAndZero()
		{
			var offsets = KingdomForageNativeGeometry.Ring(2).ToList();
			Assert.That(offsets, Has.Count.EqualTo(16));
			foreach ((int dx, int dy) in offsets)
				Assert.That(System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)), Is.EqualTo(2));
		}

		[Test]
		public void RingAtRadiusZeroIsOnlyTheOrigin()
		{
			var offsets = KingdomForageNativeGeometry.Ring(0).ToList();
			Assert.That(offsets, Is.EquivalentTo(new[] { (0, 0) }));
		}

		[Test]
		public void CountContainingCountsEveryMatchingLineNotJustPresence()
		{
			var notes = new List<string> { "the scrub is cut out.", "unrelated note",
				"the scrub is cut out.", "the scrub is cut out." };
			Assert.That(KingdomForageNativeGeometry.CountContaining(notes, "the scrub is cut out."),
				Is.EqualTo(3));
		}

		[TestCase(null, "x")]
		[TestCase("marker", null)]
		[TestCase("marker", "")]
		public void CountContainingIsNullSafe(string marker, string ignored)
		{
			var notes = marker == null ? null : new List<string> { marker };
			Assert.That(KingdomForageNativeGeometry.CountContaining(notes, ignored), Is.EqualTo(0));
		}

		[Test]
		public void CountContainingFindsZeroWhenAbsent()
		{
			var notes = new List<string> { "nothing here", null, "still nothing" };
			Assert.That(KingdomForageNativeGeometry.CountContaining(notes, "exhausted"), Is.EqualTo(0));
		}
	}
}
#endif
