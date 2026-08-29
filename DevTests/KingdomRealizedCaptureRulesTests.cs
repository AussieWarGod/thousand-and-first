#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The realized-lot canonical encoding. This is what answers the question a receipt-only
	/// differential could not: two builds whose frozen receipts agree but whose cells, objects, or
	/// rendering differ must produce different digests.
	/// <para>
	/// The grammar is length-prefixed, so the cases that matter most are the ones a separator-joined
	/// grammar would fold together: an absent value against the literal sentinel, a value carrying a
	/// separator, and a nested subgrammar joined with its own separators.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomRealizedCaptureRulesTests
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

		[Test]
		public void SameRealizedLotDigestsIdentically()
		{
			Assert.AreEqual(Baseline(), Baseline());
			Assert.AreEqual(64, Baseline().Length);
		}

		/// <summary>Enumeration order must not change the answer.</summary>
		[Test]
		public void DigestIsIndependentOfEnumerationOrder()
		{
			List<KingdomRealizedObjectFact> reversed = Objects();
			reversed.Reverse();
			List<KingdomRealizedCellFact> shuffled = Cells(2, 2);
			shuffled.Reverse();
			Assert.AreEqual(Baseline(), Digest(shuffled, reversed));
		}

		// ----- the whole point: different realized ground must not match -----------------------

		[TestCase("Owner")]
		[TestCase("Components")]
		[TestCase("Blocking")]
		[TestCase("Door")]
		[TestCase("Liquid")]
		public void EveryRecordedCellFieldChangesTheDigest(string field)
		{
			List<KingdomRealizedCellFact> altered = Cells(2, 2);
			KingdomRealizedCellFact cell = altered[3];
			switch (field)
			{
				case "Owner": cell.Owner = !cell.Owner; break;
				case "Components": cell.Components = cell.Components + 5; break;
				case "Blocking": cell.Blocking = !cell.Blocking; break;
				case "Door": cell.Door = !cell.Door; break;
				case "Liquid": cell.Liquid = !cell.Liquid; break;
			}
			Assert.AreNotEqual(Baseline(), Digest(altered, Objects()));
		}

		[Test]
		public void AMissingObjectChangesTheDigest()
		{
			List<KingdomRealizedObjectFact> fewer = Objects();
			fewer.RemoveAt(1);
			Assert.AreNotEqual(Baseline(), Digest(Cells(2, 2), fewer));
		}

		[Test]
		public void AMovedObjectChangesTheDigest()
		{
			Assert.AreNotEqual(Baseline(), Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.X = 1; }));
		}

		[TestCase("Blueprint")]
		[TestCase("Slot")]
		[TestCase("Anchor")]
		[TestCase("Tile")]
		[TestCase("RenderString")]
		[TestCase("ColorString")]
		[TestCase("DetailColor")]
		[TestCase("TileColor")]
		public void EveryRecordedTextFieldChangesTheDigest(string field)
		{
			Assert.AreNotEqual(Baseline(), Mutated(delegate (KingdomRealizedObjectFact o)
			{
				switch (field)
				{
					case "Blueprint": o.Blueprint = "r_KingdomOther"; break;
					case "Slot": o.Slot = "floor"; break;
					case "Anchor": o.Anchor = "south"; break;
					case "Tile": o.Tile = "Terrain/sw_other.bmp"; break;
					case "RenderString": o.RenderString = "="; break;
					case "ColorString": o.ColorString = "&r"; break;
					case "DetailColor": o.DetailColor = "R"; break;
					case "TileColor": o.TileColor = "&r"; break;
				}
			}));
		}

		[TestCase("Layer")]
		[TestCase("RenderLayer")]
		[TestCase("PathState")]
		[TestCase("AuthorityProved")]
		[TestCase("Existing")]
		[TestCase("Owner")]
		[TestCase("PhysicsPresent")]
		[TestCase("Solid")]
		[TestCase("BlueprintSolid")]
		[TestCase("Door")]
		[TestCase("Liquid")]
		public void EveryRecordedStateFieldChangesTheDigest(string field)
		{
			Assert.AreNotEqual(Baseline(), Mutated(delegate (KingdomRealizedObjectFact o)
			{
				switch (field)
				{
					case "Layer": o.Layer = 2; break;
					case "RenderLayer": o.RenderLayer = 9; break;
					case "PathState": o.PathState = 3; break;
					case "AuthorityProved": o.AuthorityProved = false; break;
					case "Existing": o.Existing = true; break;
					case "Owner": o.Owner = true; break;
					case "PhysicsPresent": o.PhysicsPresent = false; break;
					case "Solid": o.Solid = false; break;
					case "BlueprintSolid": o.BlueprintSolid = false; break;
					case "Door": o.Door = true; break;
					case "Liquid": o.Liquid = KingdomRealizedCaptureRules.Liquid(1, 2, 0,
						new List<string>()); break;
				}
			}));
		}

		// ----- RED 10: the live component, not its blueprint --------------------------------------

		/// <summary>
		/// A component whose Physics part was stripped after staging blocks nothing any more. A
		/// digest that read only the immutable blueprint would call it identical to the intact
		/// output, which is exactly the false green a realized-state capture exists to close.
		/// </summary>
		[Test]
		public void AStrippedPhysicsPartIsADifferentRealizedResult()
		{
			string stripped = Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.PhysicsPresent = false; o.Solid = false; });
			Assert.AreNotEqual(Baseline(), stripped);
			Assert.AreNotEqual(stripped, Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.PhysicsPresent = true; o.Solid = false; }),
				"a missing part and a present-but-permeable part are different results");
		}

		/// <summary>
		/// Live solidity and the blueprint's declaration are separate facts, so a component that
		/// drifted from its own design is visible as a difference rather than averaged away.
		/// </summary>
		[Test]
		public void LiveSolidityAndTheBlueprintDeclarationAreSeparateFacts()
		{
			string toggledLive = Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.Solid = false; });
			string toggledDeclared = Mutated(delegate (KingdomRealizedObjectFact o)
				{ o.BlueprintSolid = false; });
			Assert.AreNotEqual(Baseline(), toggledLive);
			Assert.AreNotEqual(Baseline(), toggledDeclared);
			Assert.AreNotEqual(toggledLive, toggledDeclared);
		}

		// ----- RED 9: lot identity is a precondition, never cross-path identity -------------------

		/// <summary>
		/// Two lawful builds of one design hold DIFFERENT lot ids - an ordinary commission's plot id
		/// and the review gallery's own generated one. The component token hashes the lot id, so
		/// carrying it into the comparison would make an ordinary-play anchor unmatchable by
		/// construction, which is the impossible-oracle defect one level down. The measured row
		/// therefore records only that authority was proved.
		/// </summary>
		[Test]
		public void DifferentLawfulLotIdsWithIdenticalPlacementsDigestAlike()
		{
			Assert.AreEqual(Measured("plot-hearth-0021"), Measured("taf-gallery-4-0f2c8a11"));
		}

		/// <summary>
		/// What a captured row for one lot looks like. The lot id is used for authority proof and
		/// then deliberately does not reach any recorded field.
		/// </summary>
		private static string Measured(string lotId)
		{
			List<KingdomRealizedObjectFact> objects = Objects();
			for (int i = 0; i < objects.Count; i++)
				objects[i].AuthorityProved = !string.IsNullOrEmpty(lotId);
			return Digest(Cells(2, 2), objects);
		}

		/// <summary>Unproved authority is still a different result, so the flag is not decorative.</summary>
		[Test]
		public void AnUnprovedAuthorityRowDoesNotMatchAProvedOne()
		{
			Assert.AreNotEqual(Measured("plot-hearth-0021"), Measured(null));
		}

	}
}
#endif
