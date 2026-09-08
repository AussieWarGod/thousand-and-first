#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// A dedicated stockpile holds a stated number of material units. Ruling 5 is the whole of
	/// this suite: counting stays WHOLE (no truncation anywhere, so no standing save ever reads
	/// lower than it did) and it is INTAKE that refuses.
	/// </summary>
	public class KingdomStockpileCapacityTests
	{
		private const string StoresFile = "Growth/KingdomSurvey.11.MaterialStores.cs";
		private const string RoomFile = "Growth/KingdomMaterials.StockpileRoom.cs";
		private const string StockFile = "Growth/KingdomMaterials.04.MaterialStock.cs";
		private const string GatesFile = "Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs";
		private const string CarryFile = "Quests/KingdomBounty.WorkAndCarry.cs";

		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(-9999)]
		public void StockpileCapacity_FallsBackRatherThanReadingAsZero(int declared)
		{
			ClassicAssert.AreEqual(KingdomRules.DefaultStockpileCapacity,
				KingdomRules.StockpileCapacity(declared));
		}

		[TestCase(1)]
		[TestCase(32)]
		[TestCase(384)]
		public void StockpileCapacity_TakesDeclaredSizeAtItsWord(int declared)
		{
			ClassicAssert.AreEqual(declared, KingdomRules.StockpileCapacity(declared));
		}

		[Test]
		public void NamedCapacities_AreTheTunableLadderTheRulingFixed()
		{
			ClassicAssert.AreEqual(32, KingdomRules.DefaultStockpileCapacity);
			ClassicAssert.AreEqual(32, KingdomRules.HeartStockpileCapacity);
			ClassicAssert.AreEqual(96, KingdomRules.StorehouseCapacity);
			ClassicAssert.AreEqual(192, KingdomRules.StoreyardCapacity);
			ClassicAssert.AreEqual(384, KingdomRules.StorehallCapacity);
			ClassicAssert.AreEqual(48, KingdomRules.ShelfCapacity);
			ClassicAssert.AreEqual(64, KingdomRules.LockerCapacity);
		}

		[Test]
		public void CapacityTag_IsItsOwnAccountAndNotThePantrys()
		{
			ClassicAssert.AreEqual("r_KingdomStockpileCapacity", KingdomRules.StockpileCapacityTag);
			ClassicAssert.AreNotEqual(KingdomRules.LarderCapacityTag,
				KingdomRules.StockpileCapacityTag);
			ClassicAssert.AreEqual("KingdomStockpileFullAnnounced",
				KingdomRules.StockpileFullAnnouncedProperty);
		}

		/// <summary>Ruling 5's load-bearing pin. Both counting paths must stay capacity-blind, or
		/// the settlement ledger and a purpose-local debit view could disagree, and an over-cap
		/// standing save would silently read lower than it did.</summary>
		[Test]
		public void CountingPathsNeverReadACapacity()
		{
			string counting = Between(TestMain.ReadRepositoryText(GatesFile),
				"public static MaterialStock Stock(Zone Z)",
				"private static void TallyAvailableHeld(");
			StringAssert.Contains("internal static MaterialStock StockForExactContainer(", counting);
			StringAssert.Contains("TallyAvailableHeld(stock, held)", counting);
			StringAssert.Contains("TallyAvailableHeld(exact, held)", counting);
			ClassicAssert.IsFalse(counting.Contains("StockCapacityOf"),
				"Stock()/StockForExactContainer() must never truncate by capacity");
			ClassicAssert.IsFalse(counting.Contains("StockpileRoom"),
				"Stock()/StockForExactContainer() must never truncate by room");
		}

		/// <summary>The capacity read mirrors the pantry's, off its OWN tag, and never reads
		/// zero; the hold beside it is physical.</summary>
		[Test]
		public void CapacityReadMirrorsThePantrysOffItsOwnTag()
		{
			string stores = TestMain.ReadRepositoryText(StoresFile);
			StringAssert.Contains("public static int StockCapacityOf(GameObject Container)", stores);
			StringAssert.Contains("Container.GetTag(KingdomRules.StockpileCapacityTag, \"\")", stores);
			StringAssert.Contains("return KingdomRules.StockpileCapacity(declared);", stores);
			// The sibling it is modelled on stays exactly where it was.
			string survey = KingdomSurveyLogicalSource.Read();
			StringAssert.Contains("public static int CapacityOf(GameObject Container)", survey);
			StringAssert.Contains("return KingdomRules.LarderCapacity(declared);", survey);
			StringAssert.Contains("public static int StockCapacityOf(GameObject Container)", survey);
		}

		/// <summary>The physical hold classifies with the material vocabulary and nothing else:
		/// no routed-input lease gate, so a leased stack still occupies the room it occupies and
		/// the number never jumps when a lease releases (R10). Anything unclassified counts as
		/// nothing and consumes no room.</summary>
		[Test]
		public void PhysicalHoldClassifiesWithoutTheLeaseGate()
		{
			string stores = TestMain.ReadRepositoryText(StoresFile);
			StringAssert.Contains("public static int StockHeldIn(GameObject Container)", stores);
			StringAssert.Contains("KingdomMaterials.TryOrdinaryMaterialOf(item, out _)", stores);
			StringAssert.Contains("KingdomMaterials.TryExoticOf(item, out _)", stores);
			StringAssert.Contains("KingdomMaterials.TryBitsOf(item, bits)", stores);
			StringAssert.Contains("held += (item.Count > 0) ? item.Count : 1;", stores);
			ClassicAssert.IsFalse(stores.Contains("CanUseMaterial"),
				"the physical hold must not apply the routed-input lease gate");
			ClassicAssert.IsFalse(stores.Contains("TallyAvailableHeld"),
				"the physical hold must not route through the spendable tally");
			ClassicAssert.IsFalse(stores.Contains("InputLease"),
				"the physical hold must not read a lease snapshot at all");
		}

		/// <summary>Room is capacity minus the physical hold, floored at zero, and an overfilled
		/// container reports no room rather than a negative one.</summary>
		[Test]
		public void RoomIsCapacityMinusPhysicalHoldAndNeverNegative()
		{
			string room = Between(TestMain.ReadRepositoryText(RoomFile),
				"public static int StockpileRoom(GameObject Container)",
				"internal static int StockpileRoomSpoken(");
			StringAssert.Contains(
				"KingdomSurvey.StockCapacityOf(Container) - KingdomSurvey.StockHeldIn(Container)",
				room);
			StringAssert.Contains("return (room > 0) ? room : 0;", room);
		}

		/// <summary>A delivery fills a store to its room, walks on to the next store with room,
		/// and spills the remainder exactly as it already spills when no stockpile exists.
		/// </summary>
		[Test]
		public void PutFillsToRoomThenWalksOnThenSpills()
		{
			string put = PutSource();
			AssertOrdered(put,
				"for (int i = 0; i < Stockpiles.Count && remaining > 0; i++)",
				"int room = StockpileRoomSpoken(container);",
				"if (room < 1)",
				"continue;",
				"placed += Deposit(container, blueprint, room, ref remaining);",
				"while (remaining > 0)",
				"if (Fallback != null)",
				"spilled += batch;",
				"item.Obliterate();",
				"Tally.Add(Material, placed + spilled);");
			string deposit = Between(put, "private int Deposit(GameObject Container", "return placed;");
			StringAssert.Contains("while (Remaining > 0 && room > 0)", deposit);
			StringAssert.Contains("batch = (Remaining < room) ? Remaining : room;", deposit);
			StringAssert.Contains("room -= batch;", deposit);
			StringAssert.Contains("NoStack: true", deposit);
		}

		/// <summary>A full store is skipped and nothing already in it is touched: no Destroy, no
		/// RemoveObject, no transfer out. Refusing intake is the whole enforcement.</summary>
		[Test]
		public void AFullStoreIsSkippedAndNeverEmptied()
		{
			string put = PutSource();
			ClassicAssert.AreEqual(0, Occurrences(put, "Destroy("),
				"a refused delivery must never disturb what is already stored");
			ClassicAssert.AreEqual(0, Occurrences(put, "RemoveObject("),
				"a refused delivery must never disturb what is already stored");
			// The one Obliterate is the pre-existing discard of an item this method just created
			// when the caller has no ground to drop on. It never touches stored goods.
			ClassicAssert.AreEqual(1, Occurrences(put, "item.Obliterate();"));
		}

		/// <summary>STANDARDS 7b: said once when the store fills, taken back the moment it has
		/// room again.</summary>
		[Test]
		public void FullnessIsSaidOnceAndTakenBackWhenRoomReturns()
		{
			string spoken = Between(TestMain.ReadRepositoryText(RoomFile),
				"internal static int StockpileRoomSpoken(GameObject Container)",
				"public static int FullStockpiles(");
			AssertOrdered(spoken,
				"int room = StockpileRoom(Container);",
				"if (room > 0)",
				"Container.SetIntProperty(KingdomRules.StockpileFullAnnouncedProperty, 0,",
				"RemoveIfZero: true);",
				"return room;",
				"if (Container.GetIntProperty(KingdomRules.StockpileFullAnnouncedProperty) == 1)",
				"return 0;",
				"Container.SetIntProperty(KingdomRules.StockpileFullAnnouncedProperty, 1);",
				"MessageQueue.AddPlayerMessage(\"The \" + Container.ShortDisplayName");
			StringAssert.Contains(
				"will not take another bundle; it holds all the keepers can account for.", spoken);
		}

		/// <summary>The porter reads room before every bundle, and a settlement whose stores are
		/// merely FULL is never told to dedicate a container it already has.</summary>
		[Test]
		public void ThePorterReadsRoomAndSaysTheHonestThing()
		{
			string carry = TestMain.ReadRepositoryText(CarryFile);
			AssertOrdered(carry,
				"bool anyStore = false;",
				"anyStore = true;",
				"if (KingdomMaterials.StockpileRoomSpoken(stock.Stockpiles[i]) < 1)",
				"if (!anyStore)",
				"Announce(System, Data, BountyBlock.NowhereToCarry);",
				"if (KingdomMaterials.StockpileRoom(container) < 1) break;");
		}

		/// <summary>The founder's status line carries the room, physical on both sides of the
		/// fraction.</summary>
		[Test]
		public void StatusLineCarriesHeldOfCapacity()
		{
			string gates = TestMain.ReadRepositoryText(GatesFile);
			string line = Between(gates, "public static string StockLine(Zone Z)",
				"public static string StockRoomClause(");
			StringAssert.Contains("string room = \" (\" + StockRoomClause(stock) + \")\";", line);
			StringAssert.Contains("\"The stockpiles stand empty\" + room + \".\"", line);
			StringAssert.Contains("+ room + \".\";", line);
			string clause = Between(gates, "public static string StockRoomClause(MaterialStock Stock)",
				"\t}\n}");
			StringAssert.Contains("held += KingdomSurvey.StockHeldIn(Stock.Stockpiles[i]);", clause);
			StringAssert.Contains("capacity += KingdomSurvey.StockCapacityOf(Stock.Stockpiles[i]);",
				clause);
			StringAssert.Contains("return held + \" of \" + capacity + \" units\"", clause);
			StringAssert.Contains("\" stockpile full\"", clause);
			StringAssert.Contains("\" stockpiles full\"", clause);
		}

		/// <summary>Every capacity a blueprint declares is one of the named tunable constants, so
		/// the ladder stays a ladder and nobody hand-writes a loose number into XML.</summary>
		[Test]
		public void DeclaredBlueprintCapacitiesAreNamedConstants()
		{
			HashSet<int> named = new HashSet<int>
			{
				KingdomRules.DefaultStockpileCapacity, KingdomRules.HeartStockpileCapacity,
				KingdomRules.StorehouseCapacity, KingdomRules.StoreyardCapacity,
				KingdomRules.StorehallCapacity, KingdomRules.ShelfCapacity,
				KingdomRules.LockerCapacity
			};
			Regex declaration = new Regex(
				"Name=\"" + Regex.Escape(KingdomRules.StockpileCapacityTag)
				+ "\"\\s+Value=\"([^\"]*)\"");
			foreach (string relative in new[]
			{
				"RuntimeData/ObjectBlueprints.xml", "RuntimeData/KingdomBuildings.xml"
			})
			{
				foreach (Match match in declaration.Matches(TestMain.ReadRepositoryText(relative)))
				{
					int declared;
					ClassicAssert.IsTrue(int.TryParse(match.Groups[1].Value, out declared),
						relative + " declares a non-numeric stockpile capacity");
					ClassicAssert.IsTrue(named.Contains(declared),
						relative + " declares stockpile capacity " + declared
						+ ", which is not a named constant in KingdomRules.MaterialStores");
				}
			}
		}

		/// <summary>The modder-facing contract is documented where a modder looks for it.</summary>
		[Test]
		public void TheCapacityTagIsDocumentedForModders()
		{
			string modding = TestMain.ReadRepositoryText("MODDING.md");
			StringAssert.Contains("r_KingdomStockpileCapacity", modding);
			StringAssert.Contains("KingdomRules.DefaultStockpileCapacity", modding);
			StringAssert.Contains(
				"Nothing already in a store is ever moved, released, or uncounted", modding);
		}

		private static string PutSource()
		{
			return Between(TestMain.ReadRepositoryText(StockFile),
				"public int Put(KingdomMaterial Material, int Units, Cell Fallback)",
				"/// <summary>Puts a whole tally away");
		}

		private static int Occurrences(string Source, string Term)
		{
			int total = 0;
			int at = Source.IndexOf(Term, StringComparison.Ordinal);
			while (at >= 0)
			{
				total++;
				at = Source.IndexOf(Term, at + Term.Length, StringComparison.Ordinal);
			}
			return total;
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, "missing source boundary: " + Start);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(end, start, "missing source boundary: " + End);
			return Source.Substring(start, end - start);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int previous = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], previous + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(found, previous, "out of order or missing: " + Terms[i]);
				previous = found;
			}
		}
	}
}
#endif
