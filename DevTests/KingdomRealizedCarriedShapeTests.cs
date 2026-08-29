#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The upgrade-retention marker: proved, then deliberately left out of the differential.
	/// <para>
	/// The shipped same-lot upgrade path restamps a retained placement and then writes this marker,
	/// and no completion path removes it. Treating it as corruption made every completed upgraded
	/// building permanently uncapturable as ordinary-play evidence - the same defect as carrying the
	/// lot-bearing component token, one field over: provenance mistaken for identity.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomRealizedCarriedShapeTests
	{
		private static KingdomRealizedCarriedShape Carried(bool hasInt, int value, bool hasText)
		{
			return KingdomRealizedAuthorityShape.Carried(hasInt, value, hasText);
		}

		/// <summary>A freshly stamped component that was never retained has no marker at all.</summary>
		[Test]
		public void NoCarriedKeyIsAbsent()
		{
			Assert.AreEqual(KingdomRealizedCarriedShape.Absent, Carried(false, 0, false));
		}

		/// <summary>The lawful case the old rejection made impossible.</summary>
		[Test]
		public void ExactlyOneIntKeyHoldingOneIsCarried()
		{
			Assert.AreEqual(KingdomRealizedCarriedShape.Carried, Carried(true, 1, false));
		}

		[Test]
		public void AStoredZeroIsInvalidRatherThanAbsent()
		{
			Assert.AreEqual(KingdomRealizedCarriedShape.Invalid, Carried(true, 0, false),
				"something wrote that key; a default read would call it absent");
		}

		[TestCase(2)]
		[TestCase(-1)]
		[TestCase(int.MaxValue)]
		public void AnUnknownIntValueIsInvalid(int value)
		{
			Assert.AreEqual(KingdomRealizedCarriedShape.Invalid, Carried(true, value, false));
		}

		[Test]
		public void AStringTypedMarkerIsInvalid()
		{
			Assert.AreEqual(KingdomRealizedCarriedShape.Invalid, Carried(false, 0, true));
		}

		[Test]
		public void ADualTypedMarkerIsInvalid()
		{
			Assert.AreEqual(KingdomRealizedCarriedShape.Invalid, Carried(true, 1, true),
				"a key under two tables is never resolved in either direction");
		}

		// ----- and then it leaves the comparison --------------------------------------------------

		/// <summary>
		/// A fresh gallery realization and a lawful upgraded realization with identical final
		/// placements are the same realized result. The marker says how the piece arrived, not what
		/// stands there, so it must not reach the digest.
		/// </summary>
		[Test]
		public void CarriedAndFreshComponentsDigestAlike()
		{
			Assert.AreEqual(Digest(KingdomRealizedCarriedShape.Absent),
				Digest(KingdomRealizedCarriedShape.Carried));
		}

		/// <summary>
		/// The measured row for a component whose only difference is its retention provenance. The
		/// shape is proved by the capture and then reaches no recorded field.
		/// </summary>
		private static string Digest(KingdomRealizedCarriedShape carried)
		{
			Assert.AreNotEqual(KingdomRealizedCarriedShape.Invalid, carried);
			List<KingdomRealizedCellFact> cells = new List<KingdomRealizedCellFact>
			{
				new KingdomRealizedCellFact { X = 0, Y = 0, Components = 1, Blocking = true }
			};
			List<KingdomRealizedObjectFact> objects = new List<KingdomRealizedObjectFact>
			{
				new KingdomRealizedObjectFact
				{
					X = 0,
					Y = 0,
					Blueprint = "r_KingdomWall",
					Slot = "wall",
					Layer = 1,
					Anchor = null,
					AuthorityProved = true,
					Existing = false,
					Owner = false,
					PhysicsPresent = true,
					Solid = true,
					BlueprintSolid = true,
					Door = false,
					Liquid = null,
					Tile = "Terrain/sw_wall.bmp",
					RenderString = "#",
					ColorString = "&y",
					DetailColor = "K",
					TileColor = "&y",
					RenderLayer = 5,
					PathState = 0
				}
			};
			return KingdomRealizedCaptureRules.Digest(1, 1, cells, objects);
		}
	}
}
#endif
