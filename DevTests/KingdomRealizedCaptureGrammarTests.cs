#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Injectivity, bounds, and coverage for the realized-lot grammar.
	/// <para>
	/// Split from the field-differential fixture only to hold the house line cap. These are the
	/// cases a separator-joined grammar folds together: an absent value against the literal
	/// sentinel, a live value spelled like a field boundary, malformed UTF-16 that a default
	/// encoder replaces, and a nested subgrammar joined with its own separators.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomRealizedCaptureGrammarTests
	{
		private static List<KingdomRealizedCellFact> Cells(int width, int height)
		{
			List<KingdomRealizedCellFact> cells = new List<KingdomRealizedCellFact>();
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
					cells.Add(new KingdomRealizedCellFact
					{
						X = x,
						Y = y,
						Owner = x == 0 && y == 0,
						Components = (x + y) % 2,
						Blocking = (x + y) % 2 == 0,
						Door = false,
						Liquid = false
					});
			return cells;
		}

		private static KingdomRealizedObjectFact Object(int x, int y, string blueprint)
		{
			return new KingdomRealizedObjectFact
			{
				X = x,
				Y = y,
				Blueprint = blueprint,
				Slot = "wall",
				Layer = 1,
				Anchor = "north",
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
			};
		}

		private static List<KingdomRealizedObjectFact> Objects()
		{
			return new List<KingdomRealizedObjectFact>
			{
				Object(0, 0, "r_KingdomWall"),
				Object(1, 1, "r_KingdomDoor")
			};
		}

		private static string Digest(List<KingdomRealizedCellFact> cells,
			List<KingdomRealizedObjectFact> objects)
		{
			return KingdomRealizedCaptureRules.Digest(2, 2, cells, objects);
		}

		private static string Baseline()
		{
			return Digest(Cells(2, 2), Objects());
		}

		private static string Mutated(Action<KingdomRealizedObjectFact> change)
		{
			List<KingdomRealizedObjectFact> objects = Objects();
			change(objects[0]);
			return Digest(Cells(2, 2), objects);
		}

		// ----- injectivity: the collisions a separator grammar could not avoid ------------------

		/// <summary>Absent is not the literal sentinel. Mapping both to "-" is the collision.</summary>
		[Test]
		public void AbsentAndTheLiteralSentinelAreDistinct()
		{
			string absent = Mutated(delegate (KingdomRealizedObjectFact o) { o.Slot = null; });
			string sentinel = Mutated(delegate (KingdomRealizedObjectFact o) { o.Slot = "-"; });
			Assert.AreNotEqual(absent, sentinel);
			Assert.AreNotEqual(Baseline(), absent);
			Assert.AreNotEqual(Baseline(), sentinel);
		}

		/// <summary>
		/// A value spelled like a field boundary is still one value. Under a length prefix it cannot
		/// borrow the next field's bytes however it is written.
		/// </summary>
		[TestCase("3:abc")]
		[TestCase("0:")]
		[TestCase(":")]
		[TestCase("-4:xyz")]
		public void AValueSpelledLikeAFieldBoundaryDoesNotCollide(string spelling)
		{
			string a = Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Slot = spelling; o.Anchor = "north"; });
			string b = Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Slot = "wall"; o.Anchor = spelling; });
			Assert.AreNotEqual(a, b);
			Assert.AreNotEqual(Baseline(), a);
		}

		/// <summary>
		/// The separators the previous grammar joined with are now refused outright, so no live value
		/// can imitate a record or unit boundary at all.
		/// </summary>
		[TestCase("\u0001")]
		[TestCase("\u0002")]
		[TestCase("a\u0000b")]
		[TestCase("a\u007Fb")]
		[TestCase("a\u009Cb")]
		public void ControlValuesAreRefusedRatherThanEncoded(string hostile)
		{
			Assert.IsNull(Mutated(delegate (KingdomRealizedObjectFact o) { o.Tile = hostile; }));
		}

		/// <summary>
		/// A lone surrogate is refused. UTF-8 encoding with the default fallback maps every one of
		/// them to U+FFFD, which would hand two different lots one digest.
		/// </summary>
		[Test]
		public void UnpairedSurrogatesAreRefused()
		{
			Assert.IsNull(Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Tile = "a\uD800b"; }));
			Assert.IsNull(Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Tile = "a\uDC00b"; }));
			Assert.IsNotNull(Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Tile = "a\uD83D\uDE00b"; }), "a well-formed pair is an ordinary value");
			Assert.AreNotEqual(
				Mutated(delegate (KingdomRealizedObjectFact o) { o.Tile = "a\uD83D\uDE00b"; }),
				Mutated(delegate (KingdomRealizedObjectFact o) { o.Tile = "a\uD83D\uDE01b"; }));
		}

		[Test]
		public void OverboundValuesAreRefused()
		{
			Assert.IsNotNull(Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Tile = new string('t', KingdomRealizedCaptureRules.MaxToken); }));
			Assert.IsNull(Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Tile = new string('t', KingdomRealizedCaptureRules.MaxToken + 1); }));
		}

		// ----- the liquid subgrammar ------------------------------------------------------------

		/// <summary>
		/// Two component sets whose naive "key=value;" join is byte-identical must still differ. The
		/// outer grammar's framing is worth nothing if a nested one re-opens the same hole.
		/// </summary>
		[Test]
		public void TheLiquidSubgrammarIsFramedToo()
		{
			List<string> split = new List<string>
			{
				KingdomRealizedCaptureRules.Pair("x", 1),
				KingdomRealizedCaptureRules.Pair("y", 2)
			};
			List<string> merged = new List<string>
			{
				KingdomRealizedCaptureRules.Pair("x=1;c=y", 2)
			};
			Assert.AreNotEqual(KingdomRealizedCaptureRules.Liquid(4, 8, 0, split),
				KingdomRealizedCaptureRules.Liquid(4, 8, 0, merged));
		}

		[Test]
		public void LiquidRefusesHostileAndOverboundComponents()
		{
			Assert.IsNull(KingdomRealizedCaptureRules.Pair("a\u0001b", 1));
			Assert.IsNull(KingdomRealizedCaptureRules.Liquid(0, 0, 0, null));
			List<string> huge = new List<string>();
			for (int i = 0; i < 64; i++)
				huge.Add(KingdomRealizedCaptureRules.Pair(new string('k', 32), i));
			Assert.IsNull(KingdomRealizedCaptureRules.Liquid(0, 0, 0, huge));
		}

		// ----- coverage and totality -------------------------------------------------------------

		[Test]
		public void MalformedInputsReturnNullRatherThanThrowing()
		{
			Assert.IsNull(KingdomRealizedCaptureRules.Canonical(0, 2, Cells(2, 2), Objects()));
			Assert.IsNull(KingdomRealizedCaptureRules.Canonical(2, 2, null, Objects()));
			Assert.IsNull(KingdomRealizedCaptureRules.Canonical(2, 2, Cells(2, 2), null));
			Assert.IsNull(KingdomRealizedCaptureRules.Digest(2, 2, Cells(3, 3), Objects()),
				"a cell count that disagrees with the rect must refuse");
			Assert.IsNull(KingdomRealizedCaptureRules.Digest(65536, 65536, Cells(2, 2), Objects()),
				"an area that would overflow its own arithmetic must refuse");
		}

		/// <summary>
		/// A duplicate coordinate paired with a missing one keeps the count right while measuring a
		/// different lot. Counting is not coverage.
		/// </summary>
		[Test]
		public void DuplicateAndMissingCoordinatesAreRefused()
		{
			List<KingdomRealizedCellFact> duplicated = Cells(2, 2);
			duplicated[3].X = duplicated[0].X;
			duplicated[3].Y = duplicated[0].Y;
			Assert.IsNull(Digest(duplicated, Objects()));
			List<KingdomRealizedCellFact> shifted = Cells(2, 2);
			shifted[0].X = 1;
			Assert.IsNull(Digest(shifted, Objects()));
		}

		[Test]
		public void OutOfBoundsFactsAreRefused()
		{
			List<KingdomRealizedCellFact> cells = Cells(2, 2);
			cells[0].X = 9;
			Assert.IsNull(Digest(cells, Objects()));
			Assert.IsNull(Mutated(delegate (KingdomRealizedObjectFact o) { o.Y = 9; }));
		}

		[Test]
		public void ANullOrBlueprintlessObjectIsRefused()
		{
			List<KingdomRealizedObjectFact> objects = Objects();
			objects[0] = null;
			Assert.IsNull(Digest(Cells(2, 2), objects));
			Assert.IsNull(Mutated(delegate (KingdomRealizedObjectFact o) { o.Blueprint = null; }));
		}

		[Test]
		public void ANullCellIsRefused()
		{
			List<KingdomRealizedCellFact> cells = Cells(2, 2);
			cells[2] = null;
			Assert.IsNull(Digest(cells, Objects()));
		}
	}
}
#endif
