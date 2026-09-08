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
		/// no routed-input lease gate and no custody gate, so a leased or reserved stack still
		/// occupies the room it occupies and the number never jumps when a lease releases (R10).
		/// Anything unclassified counts as nothing and consumes no room.
		/// <para>
		/// The counterexample this pins: a purpose-effect debit stamps its witness onto a stack
		/// that has not moved and has not changed count and is still in the same store
		/// (<c>Growth/KingdomPurposePortfolio.EffectDebitEvidence.cs</c>), which makes it protected
		/// cargo and so no longer ORDINARY. Read with the ordinary classifier, twenty stones would
		/// vanish out of the physical hold while sitting in the chest, and the store would offer
		/// twenty units of room it does not have.
		/// </para>
		/// </summary>
		[Test]
		public void PhysicalHoldClassifiesWithoutTheLeaseGate()
		{
			string stores = TestMain.ReadRepositoryText(StoresFile);
			StringAssert.Contains("public static int StockHeldIn(GameObject Container)", stores);
			StringAssert.Contains("KingdomMaterials.TryMaterialOf(item, out _)", stores);
			StringAssert.Contains("KingdomMaterials.TryExoticOf(item, out _)", stores);
			StringAssert.Contains("KingdomMaterials.TryBitsOf(item, bits)", stores);
			StringAssert.Contains("held += (item.Count > 0) ? item.Count : 1;", stores);
			ClassicAssert.IsFalse(stores.Contains("TryOrdinaryMaterialOf"),
				"the physical hold must count custody, not spend eligibility");
			ClassicAssert.IsFalse(stores.Contains("HasProtectedCargoEvidence"),
				"a reserved stack still stands in the store and still takes up its room");
			ClassicAssert.IsFalse(stores.Contains("TryProveEmpty"),
				"a stack carrying custody evidence still takes up its room");
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
				"placed += Deposit(Zone, container, blueprint, room, ref remaining);",
				"while (remaining > 0)",
				"if (Fallback != null)",
				"spilled += batch;",
				"item.Obliterate();",
				"Tally.Add(Material, placed + spilled);");
			string deposit = DepositSource();
			StringAssert.Contains("while (Remaining > 0 && room > 0)", deposit);
			StringAssert.Contains("int batch = KingdomRules.DepositBatch(Remaining, room,", deposit);
			StringAssert.Contains("room -= landed;", deposit);
			StringAssert.Contains("NoStack: true", deposit);
		}

		/// <summary>The delivery never trusts a remembered room across an engine callback. It
		/// creates the item, RE-PROVES this exact destination and its room, and only then chooses
		/// a batch; a destination that stopped being a dedicated stockpile has no room at all, and
		/// an item created for a store that filled underneath it is discarded rather than forced
		/// in. Nothing already stored is touched on that path.</summary>
		[Test]
		public void DepositReProvesTheDestinationAfterEveryCallback()
		{
			string room = TestMain.ReadRepositoryText(RoomFile);
			AssertOrdered(DepositSource(),
				"GameObject item = GameObject.Create(Blueprint);",
				"int batch = KingdomRules.DepositBatch(Remaining, room,",
				"DepositRoomNow(Container), item.HasPart(\"Stacker\"));",
				"if (batch < 1)",
				"item.Obliterate();",
				"break;",
				"item.Count = batch;",
				"if (!DepositStamped(Container, item, batch))",
				"if (GameObject.Validate(item)) item.Obliterate();",
				"break;",
				"int held = DepositHeldNow(Container);",
				"Container.Inventory.AddObject(item, null,",
				"int landed = DepositLanded(Container, item, accepted, Blueprint, batch)",
				"? batch : DepositSalvage(Container, item, held, batch);",
				"placed += landed;",
				"Remaining -= landed;",
				"room -= landed;",
				"if (landed < batch)");
			StringAssert.Contains(
				"GameObject.Validate(Container) && Container.Inventory != null", room);
			StringAssert.Contains("&& IsStockpile(Container)) ? StockpileRoom(Container) : 0;",
				room);
			StringAssert.Contains(
				"&& IsStockpile(Container)) ? KingdomSurvey.StockHeldIn(Container) : 0;", room);
		}

		/// <summary>
		/// The stamp is a callback seam of its own. <c>item.Count = batch</c> is
		/// <c>Stacker.StackCount</c>, whose setter sends <c>StackCountChangedEvent</c> to anything
		/// registered for it (Stacker.cs:26-38, StackCountChangedEvent.cs:25-43), so a handler
		/// runs AFTER the room proof that chose the batch and BEFORE the insertion that spends it.
		/// The adversary is that handler filling the store's last unit: the stamped bundle must be
		/// refused outright rather than inserted on the strength of the older number.
		/// </summary>
		[TestCase(4, 4, 4, true)]
		[TestCase(4, 4, 9, true)]
		[TestCase(1, 1, 1, true)]
		[TestCase(4, 4, 3, false)]
		[TestCase(4, 4, 1, false)]
		[TestCase(4, 4, 0, false)]
		[TestCase(4, 4, -2, false)]
		[TestCase(4, 5, 9, false)]
		[TestCase(4, 3, 9, false)]
		[TestCase(0, 0, 9, false)]
		public void AStampedBundleIsRefusedWhenItsStoreFilledWhileTheStampRan(int batch,
			int stamped, int live, bool holds)
		{
			ClassicAssert.AreEqual(holds, KingdomRules.DepositStampHolds(batch, stamped, live));
		}

		/// <summary>What may be counted is what the store ended up holding, never what the
		/// insertion call returned. A proved bundle is worth its batch; an unproved one is worth
		/// only the gain the store itself shows, so a handler that merged the bundle into a stack
		/// already there is paid once and a handler that refused it is paid nothing.</summary>
		[TestCase(4, true, 10, 14, 4)]
		[TestCase(4, true, 10, 10, 4)]
		[TestCase(4, false, 10, 14, 4)]
		[TestCase(4, false, 10, 12, 2)]
		[TestCase(4, false, 10, 10, 0)]
		[TestCase(4, false, 10, 6, 0)]
		[TestCase(4, false, 10, 99, 4)]
		[TestCase(0, true, 10, 14, 0)]
		public void OnlyWhatTheStoreGainedIsEverCounted(int batch, bool proved, int before,
			int after, int expected)
		{
			ClassicAssert.AreEqual(expected,
				KingdomRules.DepositLandedUnits(batch, proved, before, after));
		}

		/// <summary>The landing proof is the exact-object shape the food landing already uses: the
		/// same object came back, of the same blueprint, carrying the stamped count, standing in
		/// this exact store and in no cell. Withdrawal is narrower than the proof on purpose &mdash;
		/// only a bundle that reached NOBODY is destroyed, because destroying one the engine placed
		/// elsewhere would erase the very ambiguity it proves.</summary>
		[Test]
		public void TheLandingProofIsExactAndOnlyAnOwnerlessBundleIsWithdrawn()
		{
			string room = TestMain.ReadRepositoryText(RoomFile);
			string landed = Between(room, "internal static bool DepositLanded(",
				"What an unproved insertion");
			AssertOrdered(landed,
				"ReferenceEquals(Accepted, Item) && GameObject.Validate(Item)",
				"Item.Blueprint == Blueprint && Item.Count == Batch",
				"GameObject.Validate(Container) && Container.Inventory != null",
				"ReferenceEquals(Item.Physics.InInventory, Container)",
				"Item.CurrentCell == null && Container.Inventory.Objects.Contains(Item)");
			string salvage = Between(room, "internal static int DepositSalvage(",
				"/// <summary>How many of a stock's");
			AssertOrdered(salvage,
				"if (GameObject.Validate(Item))",
				"if (Item.InInventory == null && Item.CurrentCell == null) Item.Obliterate();",
				"return 0;",
				"return KingdomRules.DepositLandedUnits(Batch, false, Held,",
				"DepositHeldNow(Container));");
			ClassicAssert.AreEqual(0, Occurrences(salvage, "Destroy("),
				"an unproved insertion must never disturb what the store already held");
		}

		/// <summary>The one-room adversary, in numbers. A store is chosen with room for four and
		/// something running inside the delivery's own creation or insertion callback fills it to
		/// one: the next insertion carries one unit, not four. Filled outright, it carries none.
		/// Room released underneath the delivery is not taken either &mdash; it belongs to the
		/// next delivery to find it.</summary>
		[TestCase(9, 4, 4, true, 4)]
		[TestCase(9, 4, 1, true, 1)]
		[TestCase(9, 4, 0, true, 0)]
		[TestCase(9, 4, -3, true, 0)]
		[TestCase(9, 0, 4, true, 0)]
		[TestCase(2, 4, 3, true, 2)]
		[TestCase(9, 4, 9, true, 4)]
		[TestCase(9, 4, 4, false, 1)]
		[TestCase(1, 4, 4, true, 1)]
		[TestCase(0, 4, 4, true, 0)]
		public void DepositBatchNeverExceedsTheRoomProvedRightNow(int remaining, int room,
			int live, bool stackable, int expected)
		{
			ClassicAssert.AreEqual(expected,
				KingdomRules.DepositBatch(remaining, room, live, stackable));
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
			// The one Obliterate in Put discards an item it just created and never placed, when
			// the caller has no ground to drop on.
			ClassicAssert.AreEqual(1, Occurrences(put, "item.Obliterate();"));
			// And the three in the delivery discard a bundle it created and never counted: the
			// destination filled up between the creation and the batch, or between the stamp and
			// the insertion, or the insertion left it belonging to nobody at all. None of them
			// touches stored goods, and none of them decrements what is still to deliver.
			string deposit = TestMain.ReadRepositoryText(RoomFile);
			ClassicAssert.AreEqual(0, Occurrences(deposit, "Destroy("),
				"a refused delivery must never disturb what is already stored");
			ClassicAssert.AreEqual(0, Occurrences(deposit, "RemoveObject("),
				"a refused delivery must never disturb what is already stored");
			ClassicAssert.AreEqual(2, Occurrences(deposit, "item.Obliterate();"));
			ClassicAssert.AreEqual(1, Occurrences(deposit, "Item.Obliterate();"));
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
				"MessageQueue.AddPlayerMessage(\"{{K|The \" + Container.ShortDisplayName");
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
				"if (KingdomMaterials.StockpileRoom(container) < 1)",
				"KingdomMaterials.StockpileRoomSpoken(container);",
				"if (Data.TransferredUnits <= 0) return;",
				"break;");
			// A full store must never fall through to PileEmpty, which is a PERMANENT block.
			StringAssert.Contains("if (Data.TransferredUnits <= 0) return;", carry);
		}

		/// <summary>The founder's status line carries the room, physical on both sides of the
		/// fraction.</summary>
		[Test]
		public void StatusLineCarriesHeldOfCapacity()
		{
			string gates = TestMain.ReadRepositoryText(GatesFile);
			string line = Between(gates, "public static string StockLine(Zone Z)",
				"public static string StockRoomClause(");
			StringAssert.Contains(
				"string room = \" (\" + StockRoomClause(stock, out physical) + \")\";", line);
			StringAssert.Contains("\"The stockpiles stand empty\" + room + \".\"", line);
			StringAssert.Contains("+ room + \".\";", line);
			string clause = Between(gates, "public static string StockRoomClause(MaterialStock Stock)",
				"\t}\n}");
			StringAssert.Contains("int stored = KingdomSurvey.StockHeldIn(container);", clause);
			StringAssert.Contains("int size = KingdomSurvey.StockCapacityOf(container);", clause);
			StringAssert.Contains("return held + \" of \" + capacity + \" units\"", clause);
			StringAssert.Contains("\" stockpile full\"", clause);
			StringAssert.Contains("\" stockpiles full\"", clause);
			// One walk answers held, capacity and full together, and a thing that holds nothing is
			// skipped by both, so the fraction and the full-count never disagree about a store.
			StringAssert.Contains("if (container == null || container.Inventory == null)", clause);
		}

		/// <summary>The empty line and the room clause must agree. A store physically holding
		/// thirty units that a live work has leased has nothing SPENDABLE in it, and must not read
		/// "The stockpiles stand empty (30 of 32 units)" &mdash; one sentence saying two things.
		/// </summary>
		[Test]
		public void TheEmptyLineIsPhysicalAwareAndNeverContradictsTheRoom()
		{
			string line = Between(TestMain.ReadRepositoryText(GatesFile),
				"public static string StockLine(Zone Z)", "public static string StockRoomClause(");
			AssertOrdered(line,
				"int physical;",
				"string room = \" (\" + StockRoomClause(stock, out physical) + \")\";",
				"return (physical > 0)",
				"\"The stockpiles hold nothing that can be spent right now\" + room + \".\"",
				"\"The stockpiles stand empty\" + room + \".\"");
		}

		/// <summary>Every settlement-owned intake path chooses a store WITH ROOM, so "a full store
		/// refuses the next delivery" is true of clearance payout and strike salvage as well as of
		/// MaterialStock.Put and the porter. Both already had a ground path to fall into, and the
		/// choice is made before any receipt identity is frozen.</summary>
		[Test]
		public void EverySettlementOwnedIntakePathChoosesAStoreWithRoom()
		{
			AssertOrdered(TestMain.ReadRepositoryText("Growth/KingdomPlot2.28.ClearPayout.cs"),
				"candidate.GetIntProperty(KingdomMaterials.StockpileProperty) == 1",
				"&& KingdomMaterials.StockpileRoom(candidate) >= 1)",
				"ClearInt(Works, ClearDestinationKindProperty, 2);");
			AssertOrdered(
				TestMain.ReadRepositoryText("Growth/KingdomMaterials.13.StrikeRemovalAndSalvage.cs"),
				"&& candidate.Inventory != null && StockpileRoom(candidate) >= 1)",
				"Z.GetCell(Job.X, Job.Y)?.AddObject(item)",
				"Job.PhysicalSpilled + (destination == null ? amount : 0)");
		}

		/// <summary>A settlement out of room is not a wiring fault. Full stores with no ground to
		/// spill on must not be reported as a missing item blueprint (a MODERROR).</summary>
		[Test]
		public void RunningOutOfRoomIsNeverReportedAsAMissingBlueprint()
		{
			string room = TestMain.ReadRepositoryText(RoomFile);
			AssertOrdered(room,
				"internal static void ReportNothingLanded(",
				"if (Ground == null && Stock != null && Stock.Stockpiles.Count > 0",
				"&& FullStockpiles(Stock) >= Stock.Stockpiles.Count)",
				"KingdomLog.Log(",
				"MetricsManager.LogError(");
			string yard = TestMain.ReadRepositoryText(
				"Growth/KingdomMaterials.10.SettlementPassAndYards.cs");
			StringAssert.Contains("ReportNothingLanded(stock, Yard.CurrentCell, ", yard);
			ClassicAssert.AreEqual(0, Occurrences(yard,
				"MetricsManager.LogError(\"ThousandAndFirst KingdomMaterials: the \""),
				"the yard must route its fault line through the helper that knows the difference");
		}

		/// <summary>The materials roster gained a shard, and the count that names it is asserted
		/// so it cannot drift unnoticed again.</summary>
		[Test]
		public void TheMaterialsRosterCountsTheNewShard()
		{
			ClassicAssert.AreEqual(19, KingdomMaterialsLogicalSource.FileCount);
			StringAssert.Contains("public static int StockpileRoom(GameObject Container)",
				KingdomMaterialsLogicalSource.Read());
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
			// Vacuous until T-storage-2/T-camp-2 declare the tag: nothing in RuntimeData carries
			// it yet, so this matches zero declarations today and guards every later one.
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
			// The founder must be warned BEFORE dedicating a loot chest: anything worth bits
			// occupies stockpile room, which is most loot.
			StringAssert.Contains("anything vanilla can take apart into bits", modding);
			StringAssert.Contains("junk in it is counted against the capacity", modding);
			// And that the hold is custody rather than spend eligibility, so a modder reading it
			// does not expect a reserved stack to free the room it is standing in.
			StringAssert.Contains("still counts against the", modding);
			StringAssert.Contains("the room never jumps when a reservation is taken or released",
				modding);
		}

		private static string DepositSource()
		{
			return Between(TestMain.ReadRepositoryText(RoomFile),
				"internal static int Deposit(Zone Z, GameObject Container",
				"/// <summary>Room in an exact destination");
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
