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
		private const string HostFile = "Growth/KingdomMaterials.StockpileDeposit.cs";
		private const string GroundFile = "Growth/KingdomMaterials.GroundSpill.cs";
		private const string YardFile = "Growth/KingdomMaterials.10b.YardWork.cs";
		private const string LawFile = "Core/KingdomDepositEngine.cs";
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
			ClassicAssert.AreEqual(48, KingdomRules.DefaultStockpileCapacity);
			ClassicAssert.AreEqual(48, KingdomRules.ShelfCapacity);
			ClassicAssert.AreEqual(64, KingdomRules.LockerCapacity);
			ClassicAssert.AreEqual(96, KingdomRules.StorehouseCapacity);
			ClassicAssert.AreEqual(192, KingdomRules.StoreyardCapacity);
			ClassicAssert.AreEqual(384, KingdomRules.StorehallCapacity);
			ClassicAssert.AreEqual(8, KingdomRules.MaxStockpiles);
			ClassicAssert.AreEqual(KingdomRules.MaxStockpiles * KingdomRules.DefaultStockpileCapacity,
				KingdomRules.MaxReachableStockpileUnits);
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
				"KingdomDepositOutcome outcome = Deposit(Zone, container, blueprint, room,",
				"ref remaining);",
				"placed += outcome.Placed;",
				"if (remaining > 0 && Fallback == null)",
				"if (remaining > 0)",
				"KingdomDepositOutcome overflow = KingdomDepositEngine.Fill(",
				"new GroundSpillHost(Zone, Fallback, blueprint, remaining), remaining,",
				"spilled += overflow.Placed;",
				"Tally.Add(Material, placed + spilled);");
			string deposit = DepositSource();
			StringAssert.Contains("while (remaining > 0 && room > 0)", deposit);
			StringAssert.Contains("int batch = KingdomRules.DepositBatch(remaining, room,", deposit);
			StringAssert.Contains("room -= landed;", deposit);
			StringAssert.Contains("NoStack: true", TestMain.ReadRepositoryText(HostFile));
			// The ground is a destination like any other and is paid on the same proof: the cell
			// is read rather than the call, because Cell.AddObject hands the object back even when
			// Physics.EnterCell refused it.
			string ground = TestMain.ReadRepositoryText(GroundFile);
			AssertOrdered(ground,
				"accepted = Ground.AddObject(item);",
				"public bool Landed(object Bundle, object Accepted, int Batch)",
				"ReferenceEquals(item.CurrentCell, Ground)",
				"Ground.Objects.Contains(item)");
			StringAssert.Contains("return GroundMaterialHeldNow(Ground, Blueprint);", ground);
		}

		/// <summary>
		/// A delivery that cannot prove where its bundle went stops OUTRIGHT. It does not walk on
		/// to the next store and it does not fall through to the ground: both would create the
		/// same units a second time while the first ones are still standing wherever a handler
		/// put them. Only what was proved deposited reaches the tally, and the founder is told
		/// once. <c>KingdomStockpileCustodyTests</c> drives the behaviour; this pins the
		/// propagation through <c>Put</c> and the whole-tally walk above it.
		/// </summary>
		[Test]
		public void AnUnprovedDepositStopsTheWholePutAndMintsNothing()
		{
			string put = PutSource();
			AssertOrdered(put,
				"public int Put(KingdomMaterial Material, int Units, Cell Fallback)",
				"return Put(Material, Units, Fallback, out custody);",
				"out KingdomDepositCustody Custody)",
				"Custody = KingdomDepositCustody.Settled;",
				"KingdomDepositOutcome outcome = Deposit(Zone, container, blueprint, room,",
				"placed += outcome.Placed;",
				"if (outcome.Refused)",
				"Custody = outcome.Custody;",
				"Tally.Add(Material, placed + spilled);",
				"return spilled;",
				"if (remaining > 0 && Fallback == null)");
			// The refusal returns BEFORE the ground seam, so nothing is created for the remainder.
			int refusal = put.IndexOf("if (outcome.Refused)", StringComparison.Ordinal);
			int overflow = put.IndexOf("new GroundSpillHost(", StringComparison.Ordinal);
			ClassicAssert.Greater(overflow, refusal,
				"the refusal must return before anything is created for the remainder");
			// And the ground seam's own refusal returns before the tally is credited for it.
			AssertOrdered(put,
				"KingdomDepositOutcome overflow = KingdomDepositEngine.Fill(",
				"spilled += overflow.Placed;",
				"remaining -= overflow.Placed;",
				"if (overflow.Refused)",
				"Custody = overflow.Custody;");
			// And the whole-tally walk stops on the first material whose custody is unproved.
			string all = Between(TestMain.ReadRepositoryText(StockFile),
				"public int PutAll(KingdomMaterialTally Yield, Cell Fallback)", "\t}\n}");
			AssertOrdered(all,
				"return PutAll(Yield, Fallback, out custody);",
				"out KingdomDepositCustody Custody)",
				"spilled += Put(material, Yield.Get(material), Fallback, out custody);",
				"if (custody != KingdomDepositCustody.Settled)",
				"Custody = custody;",
				"return spilled;");
			// The Qud side of the delivery is a seam and nothing else: the law it runs is the
			// engine-free one, and what it placed is what lowers the outstanding units.
			AssertOrdered(Between(TestMain.ReadRepositoryText(RoomFile),
					"internal static KingdomDepositOutcome Deposit(Zone Z, GameObject Container",
					"/// <summary>Room in an exact destination"),
				"KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(",
				"new StockpileDepositHost(Z, Container, Blueprint), Room, Remaining);",
				"Remaining -= outcome.Placed;",
				"return outcome;");
		}

		/// <summary>
		/// A refusal comes back NORMALLY. Every settlement-owned caller that goes on to stamp a
		/// receipt, write a chronicle line, or mark a one-shot yield as issued must therefore read
		/// the custody: a throw guard alone does not see it, and an unchanged tally is not
		/// distinguishable from a wiring fault.
		/// </summary>
		[Test]
		public void EveryCallerThatWritesAReceiptReadsTheCustodyFirst()
		{
			// The clearance stake: the ground yield is a ONE-SHOT phase. Stamping it issued after
			// a refusal forfeits the mud permanently and in silence.
			string clearance = TestMain.ReadRepositoryText(
				"Growth/KingdomMaterials.14.ClearanceWork.cs");
			AssertOrdered(clearance,
				"int spilled = stock.PutAll(yield, stakeCell, out KingdomDepositCustody yieldCustody);",
				"if (yieldCustody != KingdomDepositCustody.Settled)",
				"Order.BlockedAnnounced = true;",
				"return;",
				"StakeObject.SetIntProperty(ClearanceGroundPhaseProperty, 1);",
				"try { spilled += stock.Put(KingdomMaterial.Mud, mud, stakeCell, out mudCustody); }",
				"if (mudCustody != KingdomDepositCustody.Settled)",
				"Order.BlockedAnnounced = true;",
				"return;",
				"StakeObject.SetIntProperty(ClearanceGroundPhaseProperty, 2);",
				"string carried = yield.Describe();");
			// The charter: Deliver says the load was held, and the carry sign does not then write
			// "delivered" into the chronicle on top of it.
			string infrastructure = TestMain.ReadRepositoryText(
				"Growth/KingdomMaterials.06.InfrastructureAndDelivery.cs");
			AssertOrdered(infrastructure,
				"int spilled = stock.PutAll(Carried, fallback, out Custody);",
				"if (Custody != KingdomDepositCustody.Settled)",
				"KingdomLog.Log(\"materials: charter delivery held, custody unproved\");",
				"System.Ledger.Note(");
			AssertOrdered(TestMain.ReadRepositoryText(
					"Experience/KingdomGuestbook.z03.ReportingAndCarrySign.cs"),
				"int spilled = KingdomMaterials.Deliver(System, Z, manifest,",
				"out KingdomDepositCustody custody);",
				"if (custody != KingdomDepositCustody.Settled)",
				"return;",
				"KingdomChronicle.Record(System, KingdomGuestRules.DeliveredChronicleLine(");
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
				"object bundle = Host.Create();",
				"if (!Host.HeldByNobody(bundle))",
				"return Refuse(Host, Placed);",
				"int batch = KingdomRules.DepositBatch(remaining, room, Host.RoomNow(),",
				"Host.Stacks(bundle));",
				"if (batch < 1)",
				"if (!Host.Discard(bundle))",
				"return Refuse(Host, Placed);",
				"break;",
				"if (batch > 1)",
				"Host.Stamp(bundle, batch);",
				"if (!Host.HeldByNobody(bundle))",
				"return Refuse(Host, Placed);",
				"if (!KingdomRules.DepositStampHolds(batch, Host.CountOf(bundle),",
				"Host.RoomNow()))",
				"if (!Host.Discard(bundle))",
				"return Refuse(Host, Placed);",
				"break;",
				"int held = Host.MaterialHeldNow();",
				"object accepted = Host.Insert(bundle);",
				"if (Host.Landed(bundle, accepted, batch))",
				"Placed += batch;",
				"remaining -= batch;",
				"room -= batch;",
				"if (Host.Alive(bundle))",
				"if (!Host.HeldByNobody(bundle) || !Host.Discard(bundle))",
				"return Refuse(Host, Placed);",
				"break;",
				"int landed = KingdomRules.DepositLandedUnits(batch, false, held,",
				"Host.MaterialHeldNow());",
				"Placed += landed;",
				"remaining -= landed;",
				"room -= landed;",
				"if (landed < batch)");
			// The throw fence: a handler that throws out of a callback returns what was proved
			// rather than unwinding past the caller and discarding it.
			AssertOrdered(Between(TestMain.ReadRepositoryText(LawFile),
					"internal static KingdomDepositOutcome Fill(", "private static KingdomDepositOutcome Run("),
				"int placed = 0;",
				"try",
				"return Run(Host, Room, Units, ref placed);",
				"catch (Exception)",
				"return Refuse(Host, placed);");
			// And the seam onto real objects re-proves the exact destination on every reading.
			StringAssert.Contains(
				"GameObject.Validate(Container) && Container.Inventory != null", room);
			StringAssert.Contains("&& IsStockpile(Container)) ? StockpileRoom(Container) : 0;",
				room);
			StringAssert.Contains("internal static int DepositMaterialHeldNow(GameObject Container, string Blueprint)",
				room);
			string host = TestMain.ReadRepositoryText(HostFile);
			StringAssert.Contains("return DepositRoomNow(Container);", host);
			StringAssert.Contains("return DepositMaterialHeldNow(Container, Blueprint);", host);
			StringAssert.Contains("item.Count = Count;", host);
			StringAssert.Contains("return GameObject.Create(Blueprint);", host);
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
		/// elsewhere would erase the very ambiguity it proves, and would obliterate real goods a
		/// handler is holding. A bundle standing anywhere at all refuses the delivery instead.
		/// </summary>
		[Test]
		public void TheLandingProofIsExactAndOnlyAnOwnerlessBundleIsWithdrawn()
		{
			string room = TestMain.ReadRepositoryText(RoomFile);
			string landed = Between(room, "internal static bool DepositLanded(",
				"How many of a stock's");
			AssertOrdered(landed,
				"ReferenceEquals(Accepted, Item) && GameObject.Validate(Item)",
				"Item.Blueprint == Blueprint && Item.Count == Batch",
				"GameObject.Validate(Container) && Container.Inventory != null",
				// Eligibility is part of the proof, not a thing the room reader alone guards: a
				// handler can release the dedication and leave exact membership intact.
				"IsStockpile(Container)",
				"ReferenceEquals(Item.Physics.InInventory, Container)",
				"Item.CurrentCell == null && Container.Inventory.Objects.Contains(Item)");
			string law = TestMain.ReadRepositoryText(LawFile);
			// The unproved insertion: a surviving bundle is withdrawn only when nobody is holding
			// it AND the withdrawal is proved, and otherwise stops the delivery outright rather
			// than being destroyed or replaced.
			AssertOrdered(Between(law, "if (Host.Alive(bundle))", "// The bundle went into this"),
				"if (!Host.HeldByNobody(bundle) || !Host.Discard(bundle))",
				"return Refuse(Host, Placed);",
				"break;");
			// Only the store's own gain IN THIS MATERIAL is credited, and a gain short of the
			// batch stops the delivery rather than letting the caller create the shortfall again.
			AssertOrdered(Between(law, "// The bundle went into this", "return Settle(Host, Placed);"),
				"int landed = KingdomRules.DepositLandedUnits(batch, false, held,",
				"Host.MaterialHeldNow());",
				"if (landed < batch)",
				"return Refuse(Host, Placed);");
			foreach (string source in new[]
			{
				law, TestMain.ReadRepositoryText(HostFile), TestMain.ReadRepositoryText(GroundFile)
			})
			{
				ClassicAssert.AreEqual(0, Occurrences(source, "Destroy("),
					"an unproved insertion must never disturb what the store already held");
			}
			// Withdrawal is vetoable, so both seams read the body again and report the proof.
			foreach (string seam in new[]
			{
				TestMain.ReadRepositoryText(HostFile), TestMain.ReadRepositoryText(GroundFile)
			})
			{
				AssertOrdered(Between(seam, "public bool Discard(object Bundle)",
						"public object Insert(object Bundle)"),
					"if (!GameObject.Validate(item))",
					"return false;",
					"bool gone = item.Obliterate(null, Silent: true);",
					"return gone && !GameObject.Validate(item);");
				// Equipping and implanting BOTH clear the inventory and the cell, so a custody
				// proof that reads only those two would call an equipped bundle ownerless.
				StringAssert.Contains(
					"return GameObject.Validate(item) && item.Holder == null", seam);
				StringAssert.Contains("&& item.CurrentCell == null;", seam);
				ClassicAssert.AreEqual(0, Occurrences(seam, "item.InInventory == null"),
					"inventory alone is not a custody proof; equipment clears it");
			}
			// A store that can no longer carry the saying is still not said twice.
			AssertOrdered(Between(TestMain.ReadRepositoryText(HostFile),
					"public bool CustodyAnnounced", "public int RoomNow()"),
				"? Container.GetIntProperty(",
				": Spoken;",
				"Spoken = value;",
				"if (GameObject.Validate(Container))");
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
			// Put itself no longer creates or destroys anything. It walks the stores and hands
			// the overflow to the ground seam, which runs the same law; a body made only to be
			// destroyed runs two sets of other people's callbacks for nothing, which is exactly
			// how custody is lost.
			ClassicAssert.AreEqual(0, Occurrences(put, "Obliterate("),
				"the destination seams own every withdrawal");
			ClassicAssert.AreEqual(0, Occurrences(put, "GameObject.Create("),
				"only a seam may make a bundle");
			string deposit = TestMain.ReadRepositoryText(RoomFile);
			string host = TestMain.ReadRepositoryText(HostFile);
			string ground = TestMain.ReadRepositoryText(GroundFile);
			string law = TestMain.ReadRepositoryText(LawFile);
			foreach (string source in new[] { deposit, host, ground, law })
			{
				ClassicAssert.AreEqual(0, Occurrences(source, "Destroy("),
					"a refused delivery must never disturb what is already stored");
				ClassicAssert.AreEqual(0, Occurrences(source, "RemoveObject("),
					"a refused delivery must never disturb what is already stored");
			}
			ClassicAssert.AreEqual(0, Occurrences(deposit, "Obliterate("),
				"the destination seams own the only withdrawal");
			ClassicAssert.AreEqual(1, Occurrences(host, "item.Obliterate(null, Silent: true)"));
			ClassicAssert.AreEqual(1, Occurrences(ground, "item.Obliterate(null, Silent: true)"));
			ClassicAssert.AreEqual(3, Occurrences(law, "Host.Discard(bundle)"),
				"a bundle is withdrawn on exactly three paths, each proved held-by-nobody first");
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
		/// "The stockpiles stand empty (30 of 48 units)" &mdash; one sentence saying two things.
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
				"if (Custody != KingdomDepositCustody.Settled)",
				"if (Ground == null && Stock != null && Stock.Stockpiles.Count > 0",
				"&& FullStockpiles(Stock) >= Stock.Stockpiles.Count)",
				"KingdomLog.Log(",
				"MetricsManager.LogError(");
			string yard = TestMain.ReadRepositoryText(YardFile);
			StringAssert.Contains("ReportNothingLanded(stock, Yard.CurrentCell, ", yard);
			// A load held for unprovable custody is a third thing again, and the yard reads it off
			// the delivery rather than guessing from an unchanged tally.
			StringAssert.Contains(
				"stock.Put(refined, made, Yard.CurrentCell, out KingdomDepositCustody custody)",
				yard);
			StringAssert.Contains("BlueprintFor(refined), custody);", yard);
			// And the short raw load that goes back FIRST is read too: a second delivery into
			// stores already holding something unaccountable is made into the same uncertainty.
			AssertOrdered(yard,
				"stock.Put(raw, returned, Yard.CurrentCell, out KingdomDepositCustody returnCustody);",
				"if (returnCustody != KingdomDepositCustody.Settled)",
				"return;",
				"stock.Put(refined, made, Yard.CurrentCell, out KingdomDepositCustody custody)",
				// And a run that landed SOMETHING but could not prove all of it home is not
				// reported as the full amount made: the chronicle and the message below name the
				// whole of it, so the yard stops before them.
				"if (custody != KingdomDepositCustody.Settled)",
				"return;",
				"string madeLine = made + \" \" + KingdomMaterialRules.MaterialName(refined);");
			ClassicAssert.AreEqual(0, Occurrences(yard,
				"MetricsManager.LogError(\"ThousandAndFirst KingdomMaterials: the \""),
				"the yard must route its fault line through the helper that knows the difference");
		}

		/// <summary>The materials roster gained a shard, and the count that names it is asserted
		/// so it cannot drift unnoticed again.</summary>
		[Test]
		public void TheMaterialsRosterCountsTheNewShard()
		{
			ClassicAssert.AreEqual(22, KingdomMaterialsLogicalSource.FileCount);
			StringAssert.Contains("public static int StockpileRoom(GameObject Container)",
				KingdomMaterialsLogicalSource.Read());
		}

		/// <summary>Every capacity a blueprint declares is one of the named tunable constants, so
		/// the ladder stays a ladder and nobody hand-writes a loose number into XML. The shipped
		/// catalogue really declares the tag, so this is a live pin and not an empty loop.
		/// </summary>
		[Test]
		public void DeclaredBlueprintCapacitiesAreNamedConstants()
		{
			List<int> declared = DeclaredStockpileCapacities();
			ClassicAssert.GreaterOrEqual(declared.Count, 11,
				"the shipped stores must declare the tag, or the ladder is dead constants");
			HashSet<int> named = new HashSet<int>
			{
				KingdomRules.DefaultStockpileCapacity, KingdomRules.ShelfCapacity,
				KingdomRules.LockerCapacity, KingdomRules.StorehouseCapacity,
				KingdomRules.StoreyardCapacity, KingdomRules.StorehallCapacity
			};
			for (int i = 0; i < declared.Count; i++)
			{
				ClassicAssert.IsTrue(named.Contains(declared[i]),
					"RuntimeData declares stockpile capacity " + declared[i]
					+ ", which is not a named constant in KingdomRules.MaterialStores");
			}
			// And the other way round: a named rung nothing declares is dead weight that reads,
			// from the rules file, like shipped content.
			HashSet<int> shipped = new HashSet<int>(declared);
			foreach (int rung in new[]
			{
				KingdomRules.ShelfCapacity, KingdomRules.LockerCapacity,
				KingdomRules.StorehouseCapacity, KingdomRules.StoreyardCapacity,
				KingdomRules.StorehallCapacity
			})
			{
				ClassicAssert.IsTrue(shipped.Contains(rung),
					"no shipped blueprint declares the " + rung + "-unit rung");
			}
		}

		/// <summary>
		/// The cap must never make a shipped design impossible to raise. A bill is covered out of
		/// ONE reading of everything the stores hold (<c>KingdomMaterials.CanPay</c>), and the
		/// founder may only ever dedicate <see cref="KingdomRules.MaxStockpiles"/> of them, so
		/// the ceiling a founder
		/// reaches with ordinary chests alone has to clear the grandest bill in the catalogue
		/// &mdash; materials, the rare finds that must be standing beside them, and the
		/// bit-bearing stock that occupies the same room.
		/// </summary>
		[Test]
		public void TheReachableHoldCoversTheGrandestCatalogueBill()
		{
			int worst = LargestCatalogueBillUnits();
			ClassicAssert.Greater(worst, 0, "the catalogue must have priced something");
			ClassicAssert.GreaterOrEqual(KingdomRules.MaxReachableStockpileUnits, worst,
				"eight hand-dedicated stores at the default size cannot hold the "
				+ worst + "-unit bill, so that design could never be commissioned");
		}

		/// <summary>And a settlement that commissioned the top of the ladder holds the grandest
		/// bill in ONE store, so the release valve the rules file promises is real content rather
		/// than a comment.</summary>
		[Test]
		public void OneShippedStoreHoldsTheGrandestCatalogueBill()
		{
			int worst = LargestCatalogueBillUnits();
			int largest = 0;
			List<int> declared = DeclaredStockpileCapacities();
			for (int i = 0; i < declared.Count; i++)
			{
				if (declared[i] > largest) largest = declared[i];
			}
			ClassicAssert.GreaterOrEqual(largest, worst,
				"no single shipped store holds the " + worst + "-unit bill");
		}

		/// <summary>Every capacity declared in shipped RuntimeData, in file order.</summary>
		private static List<int> DeclaredStockpileCapacities()
		{
			List<int> found = new List<int>();
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
					int value;
					ClassicAssert.IsTrue(int.TryParse(match.Groups[1].Value, out value),
						relative + " declares a non-numeric stockpile capacity");
					found.Add(value);
				}
			}
			return found;
		}

		/// <summary>The largest single design price in the catalogue, in units that must be
		/// STANDING in the stores at one instant: material units, exotic units, and one unit for
		/// each bit tier a high-craft design asks for, since bits come out of real objects that
		/// occupy real room.</summary>
		private static int LargestCatalogueBillUnits()
		{
			string catalogue = TestMain.ReadRepositoryText("RuntimeData/KingdomBuildings.xml");
			int worst = 0;
			// The OPENING tag only: a design with skins closes with </building>, and its children
			// must not be swept into somebody else's price.
			foreach (Match design in new Regex("<building\\s[^>]*>", RegexOptions.Singleline)
				.Matches(catalogue))
			{
				int units = TalliedUnits(design.Value, "Materials")
					+ TalliedUnits(design.Value, "Exotics")
					+ DeclaredLength(design.Value, "Bits");
				int upgrade = TalliedUnits(design.Value, "UpgradeMaterials");
				if (units > worst) worst = units;
				if (upgrade > worst) worst = upgrade;
			}
			return worst;
		}

		/// <summary>Sums a <c>key:units</c> price attribute. Absent prices nothing.</summary>
		private static int TalliedUnits(string Design, string Attribute)
		{
			Match found = new Regex("(?<![A-Za-z])" + Attribute + "=\"([^\"]*)\"").Match(Design);
			if (!found.Success) return 0;
			int total = 0;
			foreach (string entry in found.Groups[1].Value.Split(','))
			{
				int at = entry.IndexOf(':');
				int units;
				if (at > 0 && int.TryParse(entry.Substring(at + 1), out units)) total += units;
			}
			return total;
		}

		/// <summary>Length of a bit-tier string: one bit-bearing object per tier asked for.
		/// </summary>
		private static int DeclaredLength(string Design, string Attribute)
		{
			Match found = new Regex("(?<![A-Za-z])" + Attribute + "=\"([^\"]*)\"").Match(Design);
			return found.Success ? found.Groups[1].Value.Trim().Length : 0;
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
			// And that a handler of theirs which carries a bundle off stops the delivery rather
			// than having its material made again, and never has that bundle destroyed for it.
			StringAssert.Contains("A delivery never makes the same material twice.", modding);
			StringAssert.Contains(
				"ended up somewhere the keepers cannot account for; the rest of the load is", modding);
			StringAssert.Contains(
				"only one standing in no inventory and no cell is withdrawn", modding);
			// The guide quotes the line the founder actually sees, so the two must not drift.
			string spoken = "ended up somewhere the keepers cannot account for; the rest of the load";
			StringAssert.Contains(spoken, modding);
			StringAssert.Contains(spoken, TestMain.ReadRepositoryText(HostFile));
			StringAssert.Contains(spoken, TestMain.ReadRepositoryText(GroundFile));
			StringAssert.Contains("is held rather than made a second time.}}",
				TestMain.ReadRepositoryText(HostFile));
		}

		/// <summary>The fill loop itself, which is engine-free and lives in the law shard.
		/// </summary>
		private static string DepositSource()
		{
			return Between(TestMain.ReadRepositoryText(LawFile),
				"private static KingdomDepositOutcome Run(IKingdomDepositHost Host",
				"/// <summary>Stops the delivery and says so once");
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
