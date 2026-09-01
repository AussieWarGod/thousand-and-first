#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomGuestRulesTests
	{
		[Test]
		public void GuestRuleDeclarationsKeepExactPublicAndNestedAbi()
		{
			System.Type rules = typeof(KingdomGuestRules);
			Assert.AreEqual("ThousandAndFirst.KingdomGuestRules", rules.FullName);
			Assert.IsTrue(rules.IsPublic && rules.IsAbstract && rules.IsSealed);

			System.Type hook = typeof(KingdomGuestRules.HookKind);
			System.Type lodging = typeof(KingdomGuestRules.LodgingVerdict);
			System.Type plant = typeof(KingdomGuestRules.PlantVerdict);
			Assert.AreEqual("ThousandAndFirst.KingdomGuestRules+HookKind", hook.FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomGuestRules+LodgingVerdict", lodging.FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomGuestRules+PlantVerdict", plant.FullName);
			Assert.IsTrue(hook.IsNestedPublic && lodging.IsNestedPublic && plant.IsNestedPublic);
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(hook));
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(lodging));
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(plant));
			Assert.AreEqual("0:Ruin,1:Machine,2:Debt", EnumShape(hook));
			Assert.AreEqual("0:Lodged,1:NoTier,2:NoRoom,3:NoFineHouse,4:FineHouseOccupied,"
				+ "5:ShopTooCrude", EnumShape(lodging));
			Assert.AreEqual("0:Planted,1:NotFounded,2:NothingToCarry,3:NoRoad,4:AlreadyInFlight",
				EnumShape(plant));

			string[] fields = { "RuinHooks", "MachineHooks", "NamedVillages", "DebtReasons" };
			for (int i = 0; i < fields.Length; i++)
			{
				System.Reflection.FieldInfo field = rules.GetField(fields[i],
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static |
					System.Reflection.BindingFlags.DeclaredOnly);
				Assert.IsNotNull(field, fields[i]);
				Assert.AreEqual(typeof(string[]), field.FieldType, fields[i]);
				Assert.IsTrue(field.IsInitOnly, fields[i]);
			}
			Assert.AreEqual(4, KingdomGuestRules.RuinHooks.Length);
			Assert.AreEqual(4, KingdomGuestRules.MachineHooks.Length);
			CollectionAssert.AreEqual(new[] { "Joppa", "Kyakukya", "Ezra" },
				KingdomGuestRules.NamedVillages);
			Assert.AreEqual(3, KingdomGuestRules.DebtReasons.Length);
		}

		private static string EnumShape(System.Type type)
		{
			System.Array values = System.Enum.GetValues(type);
			string[] rows = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				object value = values.GetValue(i);
				rows[i] = System.Convert.ToInt32(value) + ":" + value;
			}
			return string.Join(",", rows);
		}

		// ==================================================================================
		// Guests at the gate
		// ==================================================================================

		[TestCase(0uL, KingdomGuestRules.HookKind.Ruin)]
		[TestCase(1uL, KingdomGuestRules.HookKind.Machine)]
		[TestCase(2uL, KingdomGuestRules.HookKind.Debt)]
		[TestCase(3uL, KingdomGuestRules.HookKind.Ruin)]
		[TestCase(4uL, KingdomGuestRules.HookKind.Machine)]
		[TestCase(9uL, KingdomGuestRules.HookKind.Ruin)]
		public void PickHookKind_WrapsModuloHookKindCount(ulong roll, KingdomGuestRules.HookKind expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.PickHookKind(roll));
		}

		[TestCase(KingdomGuestRules.HookKind.Ruin, 0uL)]
		[TestCase(KingdomGuestRules.HookKind.Ruin, 7uL)]
		[TestCase(KingdomGuestRules.HookKind.Machine, 0uL)]
		[TestCase(KingdomGuestRules.HookKind.Machine, 11uL)]
		public void HookText_NeverEmptyForRuinOrMachine(KingdomGuestRules.HookKind kind, ulong roll)
		{
			string text = KingdomGuestRules.HookText(kind, roll);
			Assert.IsFalse(string.IsNullOrEmpty(text));
		}

		[Test]
		public void HookText_DebtNamesAKnownVillage()
		{
			bool sawAVillage = false;
			for (ulong roll = 0; roll < 30; roll++)
			{
				string text = KingdomGuestRules.HookText(KingdomGuestRules.HookKind.Debt, roll);
				foreach (string village in KingdomGuestRules.NamedVillages)
				{
					if (text.Contains(village))
					{
						sawAVillage = true;
					}
				}
			}
			Assert.IsTrue(sawAVillage, "every debt hook should name one of the verified vanilla villages");
		}

		[Test]
		public void HookText_DebtVariesVillageIndependentlyOfReason()
		{
			// Roll 0 and roll (NamedVillages.Length) share a reason index but differ in village
			// index by construction; if the split collapsed to one axis this pair would be equal.
			string a = KingdomGuestRules.HookText(KingdomGuestRules.HookKind.Debt, 0uL);
			string b = KingdomGuestRules.HookText(KingdomGuestRules.HookKind.Debt, 1uL);
			Assert.AreNotEqual(a, b);
		}

		[TestCase(KingdomGuestRules.HookKind.Ruin, KingdomPlotRules.PlotSize.Small)]
		[TestCase(KingdomGuestRules.HookKind.Debt, KingdomPlotRules.PlotSize.Medium)]
		[TestCase(KingdomGuestRules.HookKind.Machine, KingdomPlotRules.PlotSize.Large)]
		public void RequiredTier_ScalesWithHookKind(KingdomGuestRules.HookKind kind, KingdomPlotRules.PlotSize expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.RequiredTier(kind));
		}

		[TestCase(KingdomGuestRules.HookKind.Ruin, "scavenger")]
		[TestCase(KingdomGuestRules.HookKind.Machine, "machinist")]
		[TestCase(KingdomGuestRules.HookKind.Debt, "reckoner of debts")]
		public void TradeNoun_IsNamedFromTheHook(KingdomGuestRules.HookKind kind, string expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.TradeNoun(kind));
		}

		// ---- Notable guest cadence: mirrors LocusRulesTests' guest-cadence coverage ----

		[TestCase(999L, 1000L, false)]
		[TestCase(1000L, 1000L, true)]
		[TestCase(1001L, 1000L, true)]
		public void ShouldArrive_TripsAtTheDueTickNotBeforeIt(long timeTicks, long nextDueTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.ShouldArrive(timeTicks, nextDueTick));
		}

		[Test]
		public void NextDueTick_AddsTheFullNotableInterval()
		{
			Assert.AreEqual(5000L + KingdomGuestRules.NotableGuestIntervalTicks, KingdomGuestRules.NextDueTick(5000L));
		}

		[Test]
		public void DepartTickFor_AddsTheFullNotablePatience()
		{
			Assert.AreEqual(2000L + KingdomGuestRules.NotableGuestPatienceTicks, KingdomGuestRules.DepartTickFor(2000L));
		}

		[Test]
		public void NotableGuestIsRarerAndMorePatientThanAPlainTraveller()
		{
			// If a notable guest were tuned no differently from an ordinary traveller, giving it
			// its own rules file instead of extending KingdomLocusRules would have bought nothing.
			Assert.Greater(KingdomGuestRules.NotableGuestIntervalTicks, KingdomLocusRules.GuestIntervalTicks);
			Assert.Greater(KingdomGuestRules.NotableGuestPatienceTicks, KingdomLocusRules.GuestPatienceTicks);
		}

		[TestCase(5000L, 0L, false)]
		[TestCase(5000L, -1L, false)]
		[TestCase(4999L, 5000L, false)]
		[TestCase(5000L, 5000L, true)]
		[TestCase(5001L, 5000L, true)]
		public void ShouldDepartUnattended_NeverFiresWithoutATrackedGuest(long timeTicks, long departTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.ShouldDepartUnattended(timeTicks, departTick));
		}

		// ---- Lodging verdict: tier checked before room, both independent facts ----

		[TestCase(false, false, KingdomGuestRules.LodgingVerdict.NoTier)]
		[TestCase(false, true, KingdomGuestRules.LodgingVerdict.NoTier)]
		[TestCase(true, false, KingdomGuestRules.LodgingVerdict.NoRoom)]
		[TestCase(true, true, KingdomGuestRules.LodgingVerdict.Lodged)]
		public void AssessLodging_TierOutranksRoom(bool hasTier, bool hasRoom, KingdomGuestRules.LodgingVerdict expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.AssessLodging(hasTier, hasRoom));
		}

		[TestCase(false, KingdomPlotRules.PlotSize.None, false, 0,
			KingdomGuestRules.LodgingVerdict.NoFineHouse)]
		[TestCase(true, KingdomPlotRules.PlotSize.Small, true, 7,
			KingdomGuestRules.LodgingVerdict.NoTier)]
		[TestCase(true, KingdomPlotRules.PlotSize.Medium, false, 7,
			KingdomGuestRules.LodgingVerdict.FineHouseOccupied)]
		[TestCase(true, KingdomPlotRules.PlotSize.Medium, true, 2,
			KingdomGuestRules.LodgingVerdict.ShopTooCrude)]
		[TestCase(true, KingdomPlotRules.PlotSize.Medium, true, 3,
			KingdomGuestRules.LodgingVerdict.Lodged)]
		public void LegendaryTraderRequiresEveryLuxuryLaneClause(bool hasFineHouse,
			KingdomPlotRules.PlotSize tier, bool vacant, int shopTier,
			KingdomGuestRules.LodgingVerdict expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.AssessLegendaryTraderLodging(
				hasFineHouse, tier, vacant, shopTier));
		}

		[TestCase(KingdomGuestRules.LodgingVerdict.NoFineHouse, "fine house")]
		[TestCase(KingdomGuestRules.LodgingVerdict.FineHouseOccupied, "vacant")]
		[TestCase(KingdomGuestRules.LodgingVerdict.ShopTooCrude, "tier 3")]
		public void LegendaryTraderRefusalNamesActionableMissingClause(
			KingdomGuestRules.LodgingVerdict verdict, string phrase)
		{
			StringAssert.Contains(phrase, KingdomGuestRules.LegendaryTraderRefusal(verdict));
		}

		[Test]
		public void LegendaryTraderIsANamedShopRoleNotAHookAlias()
		{
			Assert.AreEqual("legendary trader", KingdomGuestRules.SettledTradeNoun(
				KingdomGuestRules.HookKind.Ruin, LegendaryTrader: true));
			Assert.AreEqual("scavenger", KingdomGuestRules.SettledTradeNoun(
				KingdomGuestRules.HookKind.Ruin, LegendaryTrader: false));
		}

		[Test]
		public void NoTierRefusal_NamesTheRequiredTierWord()
		{
			string text = KingdomGuestRules.NoTierRefusal(KingdomGuestRules.HookKind.Machine);
			StringAssert.Contains(KingdomPlotRules.SizeName(KingdomPlotRules.PlotSize.Large), text);
		}

		[Test]
		public void NoRoomRefusal_IsNeverEmpty()
		{
			Assert.IsFalse(string.IsNullOrEmpty(KingdomGuestRules.NoRoomRefusal()));
		}

		[Test]
		public void ProductionLegendaryTraderUsesExactFineHouseHomeAndLiveShop()
		{
			string runtime = KingdomGuestbookLogicalSource.Read();
			string helpers = TestMain.ReadRepositoryText(
				"Experience/KingdomGuestbook.z01c.MarketHandoffHelpers.cs");
			StringAssert.Contains("reading.Designation.BuildingKey, \"finehouse\"", runtime);
			StringAssert.Contains("survey.TryBenefits", runtime);
			StringAssert.Contains("TryPhysicalHousingTier", runtime);
			StringAssert.Contains("benefits.Total(\"roof\")", runtime);
			StringAssert.Contains("KingdomLodging.RoofCapacity", runtime);
			StringAssert.Contains("KingdomGuestRules.TryExactPlotBounds", runtime);
			StringAssert.Contains("KingdomLodging.ResidentsOf(Z, item).Count != 0", runtime);
			StringAssert.Contains("KingdomLodging.HomePlotIdProperty", runtime);
			StringAssert.Contains("system.HasShopkeeper ? system.ShopTier : 0", runtime);
			StringAssert.Contains("Trader.SetIntProperty(\"VillageMerchant\", 1)", runtime);
			StringAssert.Contains("Prior.RemoveIntProperty(\"VillageMerchant\")", helpers);
			StringAssert.Contains("Prior.RemovePart(marker)", helpers);
			StringAssert.Contains("Prior.GetPart<GenericInventoryRestocker>() == old", helpers);
			StringAssert.DoesNotContain("Prior.RemoveIntProperty(\"Merchant\")", helpers);
			StringAssert.DoesNotContain("Prior.RemovePart(old)", helpers);
			StringAssert.Contains("Restocker.Chance = 0", helpers);
			StringAssert.Contains("Restocker.RestockFrequency = long.MaxValue", helpers);
			StringAssert.Contains("TryCommitLegendaryMarketProjection", runtime);
			StringAssert.Contains("KingdomMarketStockCustody.TryAdmitHeld", runtime);
			StringAssert.DoesNotContain("restocker.Chance = 100", runtime);
			StringAssert.DoesNotContain("legendary trader restock", runtime);
			StringAssert.DoesNotContain("KingdomUpgrade.DesignKeyOf", runtime);
			StringAssert.DoesNotContain("HasPart(\"Bed\")", runtime);
			StringAssert.DoesNotContain("KingdomGrowth.CountBeds", runtime);
			string lodging = TestMain.ReadRepositoryText(
				"Experience/KingdomGuestbook.z01.LodgingAndHousing.cs");
			StringAssert.DoesNotContain("PerformRestock", lodging);
			string handoff = TestMain.ReadRepositoryText(
				"Experience/KingdomGuestbook.z01b.MarketHandoff.cs");
			StringAssert.Contains("merchants < 1 || merchants > 2", handoff);
			StringAssert.Contains("TransferExactLocalMarketStock", handoff);
			StringAssert.Contains("Trader.Inventory.AddObjectToInventory", handoff);
			StringAssert.DoesNotContain("Trader.Inventory.AddObject(item", handoff);
			StringAssert.Contains("Prior.Inventory.Objects.Contains(item)", handoff);
			StringAssert.Contains("KingdomShopStockRules.ItemSourceProperty", handoff);
			StringAssert.Contains("Item.IsImportant()", handoff);
			StringAssert.Contains("MarketHandoffIntentProperty", handoff);
			StringAssert.Contains("RollbackMarketTransfer", handoff);
			StringAssert.Contains("Trader.SetIntProperty(\"VillageMerchant\", 1)", handoff);
			StringAssert.Contains("Prior.RemoveIntProperty(\"VillageMerchant\")", helpers);
			StringAssert.Contains("Prior.GetPart<GenericInventoryRestocker>() == old", helpers);
			StringAssert.DoesNotContain("Prior.RemoveIntProperty(\"Merchant\")", helpers);
			Assert.Less(handoff.IndexOf("TransferExactLocalMarketStock", StringComparison.Ordinal),
				handoff.IndexOf("Trader.SetIntProperty(\"VillageMerchant\", 1)",
					StringComparison.Ordinal),
				"the prior merchant remains canonical until every exact stock move reads back");
			StringAssert.Contains("op.PlunderRequested", runtime);
			StringAssert.Contains("LodgeReceiptProperty", runtime);

			string blueprints = TestMain.ReadRepositoryText("ObjectBlueprints.xml");
			StringAssert.Contains("Name=\"r_KingdomNotableGuestTrader\" Inherits=\"DromadTrader1\"",
				blueprints);
			StringAssert.Contains("Name=\"r_TAF_LegendaryTrader\"", blueprints);
			string populations = TestMain.ReadRepositoryText("PopulationTables.xml");
			StringAssert.Contains("Blueprint=\"r_KingdomNotableGuestTrader\"", populations);
		}

		[Test]
		public void MarketHandoffPurposeFenceCoversTransferRollbackAndSuccessCredit()
		{
			string handoff = TestMain.ReadRepositoryText(
				"Experience/KingdomGuestbook.z01b.MarketHandoff.cs");
			const string protectedEvidence =
				"TryObjectGraphAvailableForOrdinaryTransfer(item, out _)";
			string transfer = Slice(handoff, "private static bool TransferExactLocalMarketStock(",
				"private static bool RollbackMarketTransfer(");
			Assert.AreEqual(4, Count(transfer, protectedEvidence));
			AssertOrdered(transfer, "foreach (GameObject item in Trader.Inventory.Objects)",
				protectedEvidence,
				"foreach (GameObject item in new List<GameObject>(Prior.Inventory.Objects))",
				protectedEvidence,
				"item.SetStringProperty(MarketTransferTargetProperty, Trader.IDIfAssigned)",
				protectedEvidence,
				"Trader.Inventory.AddObjectToInventory(item, null",
				protectedEvidence, "RollbackMarketTransfer(System, Prior, Trader, moved)",
				"TryObjectGraphAvailableForOrdinaryTransfer(moved[i], out _)", "return true;");

			string rollback = Slice(handoff, "private static bool RollbackMarketTransfer(",
				"private static bool ExactTransferableStock(");
			Assert.AreEqual(2, Count(rollback, protectedEvidence));
			AssertOrdered(rollback, protectedEvidence,
				"Prior.Inventory.AddObjectToInventory(item, null", protectedEvidence,
				"TryObjectGraphAvailableForOrdinaryTransfer(Moved[i], out _)",
				"MarketTransferTargetProperty, null, RemoveIfNull: true");
			string exact = Slice(handoff, "private static bool ExactTransferableStock(",
				"private static bool OurCurrentMarketReceipt(");
			StringAssert.Contains("TryObjectGraphAvailableForOrdinaryTransfer(Item, out _)", exact);
			string helpers = TestMain.ReadRepositoryText(
				"Experience/KingdomGuestbook.z01c.MarketHandoffHelpers.cs");
			StringAssert.Contains("private static void SealFiniteTrader(", helpers);
			StringAssert.Contains("TryCompleteTransferredMarketService", helpers);
			StringAssert.Contains("Prior.RemovePart(marker)", helpers);
			StringAssert.Contains("Prior.GetPart<GenericInventoryRestocker>() == old", helpers);
			StringAssert.DoesNotContain("CargoSchemaProperty", handoff);
			StringAssert.DoesNotContain("PortfolioCargo", handoff);
		}

		// ---- Prose: names the guest and the settlement, distinct by outcome ----

		[Test]
		public void ArrivalChronicleLine_NamesGuestAndSettlement()
		{
			string text = KingdomGuestRules.ArrivalChronicleLine("Oyu", "Tamsketh");
			StringAssert.Contains("Oyu", text);
			StringAssert.Contains("Tamsketh", text);
		}

		[Test]
		public void LodgedChronicleLine_NamesTheTradeFromTheHook()
		{
			string text = KingdomGuestRules.LodgedChronicleLine("Oyu", "Tamsketh", KingdomGuestRules.HookKind.Machine);
			StringAssert.Contains("Oyu", text);
			StringAssert.Contains("Tamsketh", text);
			StringAssert.Contains(KingdomGuestRules.TradeNoun(KingdomGuestRules.HookKind.Machine), text);
		}

		[Test]
		public void DepartedOutsiderRumor_CarriesTheHookForward()
		{
			string hook = "a machine nobody local can name";
			string text = KingdomGuestRules.DepartedOutsiderRumor("Oyu", KingdomGuestRules.HookKind.Machine, hook);
			StringAssert.Contains(hook, text);
			StringAssert.Contains("Oyu", text);
		}

		[Test]
		public void GuestbookLine_DiffersByLodgedOutcome()
		{
			string lodged = KingdomGuestRules.GuestbookLine("Oyu", KingdomGuestRules.HookKind.Ruin, "a sealed stair", true);
			string departed = KingdomGuestRules.GuestbookLine("Oyu", KingdomGuestRules.HookKind.Ruin, "a sealed stair", false);
			Assert.AreNotEqual(lodged, departed);
			StringAssert.Contains("Oyu", lodged);
			StringAssert.Contains("Oyu", departed);
		}

		// ---- Rect tier classification: every band this mod actually stamps, plus edges ----

		[TestCase(0, 0, KingdomPlotRules.PlotSize.None)]
		[TestCase(6, 4, KingdomPlotRules.PlotSize.Small)]
		[TestCase(4, 4, KingdomPlotRules.PlotSize.Small)]
		[TestCase(8, 6, KingdomPlotRules.PlotSize.Medium)]
		[TestCase(6, 6, KingdomPlotRules.PlotSize.Medium)]
		[TestCase(12, 10, KingdomPlotRules.PlotSize.Large)]
		[TestCase(9, 9, KingdomPlotRules.PlotSize.Large)]
		[TestCase(20, 18, KingdomPlotRules.PlotSize.Huge)]
		[TestCase(30, 30, KingdomPlotRules.PlotSize.Huge)]
		public void ClassifyRectTier_MatchesTheFourStampedBands(int width, int height, KingdomPlotRules.PlotSize expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.ClassifyRectTier(width, height));
		}

		[Test]
		public void ClassifyRectTier_IsMonotonicWithArea()
		{
			KingdomPlotRules.PlotSize small = KingdomGuestRules.ClassifyRectTier(KingdomPlotRules.SmallWidth, KingdomPlotRules.SmallHeight);
			KingdomPlotRules.PlotSize medium = KingdomGuestRules.ClassifyRectTier(KingdomPlotRules.MediumWidth, KingdomPlotRules.MediumHeight);
			KingdomPlotRules.PlotSize large = KingdomGuestRules.ClassifyRectTier(KingdomPlotRules.LargeWidth, KingdomPlotRules.LargeHeight);
			KingdomPlotRules.PlotSize huge = KingdomGuestRules.ClassifyRectTier(KingdomPlotRules.HugeWidth, KingdomPlotRules.HugeHeight);
			Assert.Less(small, medium);
			Assert.Less(medium, large);
			Assert.Less(large, huge);
		}

		[TestCase(4, 4)]
		[TestCase(16, 1)]
		public void ExactDesignationBoundsPreservePhysicalShape(int width, int height)
		{
			KingdomBenefitReading reading = RectangularReading(width, height);
			Assert.IsTrue(KingdomGuestRules.TryExactPlotBounds(reading.Designation.Cells,
				out int actualWidth, out int actualHeight));
			Assert.AreEqual(width, actualWidth);
			Assert.AreEqual(height, actualHeight);
		}

		[Test]
		public void IrregularDesignationCannotBorrowItsBoundingRectangleTier()
		{
			KingdomBenefitReading reading = RectangularReading(2, 2);
			reading.Designation.Cells.RemoveAt(0);
			Assert.IsFalse(KingdomGuestRules.TryExactPlotBounds(
				reading.Designation.Cells, out _, out _));
		}

		private static KingdomBenefitReading RectangularReading(int width, int height)
		{
			KingdomBenefitReading reading = new KingdomBenefitReading {
				Designation = new KingdomBenefitDesignation()
			};
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
					reading.Designation.Cells.Add(new KingdomBenefitCell(x, y,
						KingdomBenefitCellUse.Plot));
			return reading;
		}

		// ==================================================================================
		// The carry-sign
		// ==================================================================================

		[TestCase(0, 0, 10, 0, 0, 10, 0)]
		[TestCase(0, 0, 10, 3, 0, 10, 3)]
		[TestCase(0, 0, 10, 0, 4, 10, 4)]
		[TestCase(0, 0, 10, 3, 4, 10, 4)]
		[TestCase(0, 0, 10, 3, 4, 12, 4)]
		[TestCase(0, 0, 10, 0, 0, 15, 5)]
		[TestCase(-2, -2, 10, 2, 2, 10, 4)]
		public void ZoneGridDistance_IsChebyshevAcrossAllThreeAxes(int gx1, int gy1, int z1, int gx2, int gy2, int z2, int expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.ZoneGridDistance(gx1, gy1, z1, gx2, gy2, z2));
		}

		[Test]
		public void ZoneGridDistance_IsSymmetric()
		{
			int forward = KingdomGuestRules.ZoneGridDistance(1, 2, 10, 9, 5, 12);
			int backward = KingdomGuestRules.ZoneGridDistance(9, 5, 12, 1, 2, 10);
			Assert.AreEqual(forward, backward);
		}

		[TestCase(0, KingdomGuestRules.CarrySignBaseDays)]
		[TestCase(1, KingdomGuestRules.CarrySignBaseDays + KingdomGuestRules.CarrySignDaysPerZoneStep)]
		[TestCase(5, KingdomGuestRules.CarrySignBaseDays + 5 * KingdomGuestRules.CarrySignDaysPerZoneStep)]
		[TestCase(-3, KingdomGuestRules.CarrySignBaseDays)]
		public void HaulDays_ScalesWithDistanceAndNeverGoesBelowBase(int distance, int expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.HaulDays(distance));
		}

		[Test]
		public void HaulDueTick_AddsFullDaysInTicks()
		{
			Assert.AreEqual(1000L + 3 * KingdomRules.TicksPerDay, KingdomGuestRules.HaulDueTick(1000L, 3));
		}

		[TestCase(999L, 1000L, false)]
		[TestCase(1000L, 1000L, true)]
		[TestCase(1001L, 1000L, true)]
		public void ShouldResolveHaul_TripsAtTheDueTickNotBeforeIt(long timeTicks, long dueTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.ShouldResolveHaul(timeTicks, dueTick));
		}

		[TestCase(false, false, false)]
		[TestCase(true, false, true)]
		[TestCase(false, true, true)]
		[TestCase(true, true, true)]
		public void HaulWaitsForSafety_UntilWarningAndPhysicalRaidersAreBothGone(
			bool raidActive, bool raidersPresent, bool expected)
		{
			Assert.AreEqual(expected,
				KingdomGuestRules.HaulWaitsForSafety(raidActive, raidersPresent));
		}

		// ---- Plant verdict: precedence in the order a founder would hit the refusals ----

		[TestCase(false, false, true, 5, KingdomGuestRules.PlantVerdict.NotFounded)]
		[TestCase(true, true, true, 5, KingdomGuestRules.PlantVerdict.AlreadyInFlight)]
		[TestCase(true, false, false, 5, KingdomGuestRules.PlantVerdict.NoRoad)]
		[TestCase(true, false, true, 0, KingdomGuestRules.PlantVerdict.NothingToCarry)]
		[TestCase(true, false, true, -1, KingdomGuestRules.PlantVerdict.NothingToCarry)]
		[TestCase(true, false, true, 5, KingdomGuestRules.PlantVerdict.Planted)]
		public void AssessPlant_ChecksInFounderFacingOrder(bool founded, bool alreadyInFlight, bool hasRoad, int manifestTotal, KingdomGuestRules.PlantVerdict expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.AssessPlant(founded, alreadyInFlight, hasRoad, manifestTotal));
		}

		[TestCase(KingdomGuestRules.PlantVerdict.NotFounded)]
		[TestCase(KingdomGuestRules.PlantVerdict.AlreadyInFlight)]
		[TestCase(KingdomGuestRules.PlantVerdict.NoRoad)]
		[TestCase(KingdomGuestRules.PlantVerdict.NothingToCarry)]
		public void PlantRefusal_IsNeverEmptyForARealRefusal(KingdomGuestRules.PlantVerdict verdict)
		{
			Assert.IsFalse(string.IsNullOrEmpty(KingdomGuestRules.PlantRefusal(verdict)));
		}

		[Test]
		public void PlantRefusal_IsEmptyForThePlantedVerdict()
		{
			// Planted is not a refusal; the caller never shows this one.
			Assert.AreEqual("", KingdomGuestRules.PlantRefusal(KingdomGuestRules.PlantVerdict.Planted));
		}

		[Test]
		public void PlantConfirm_NamesTheManifestAndTheDays()
		{
			string text = KingdomGuestRules.PlantConfirm("8 timber and 4 cut stone", 3);
			StringAssert.Contains("8 timber and 4 cut stone", text);
			StringAssert.Contains("3 days", text);
		}

		[Test]
		public void PlantedChronicleLine_NamesSettlementManifestAndDays()
		{
			string text = KingdomGuestRules.PlantedChronicleLine("Tamsketh", "6 marble", 4);
			StringAssert.Contains("Tamsketh", text);
			StringAssert.Contains("6 marble", text);
			StringAssert.Contains("4 days", text);
		}

		[Test]
		public void DeliveredChronicleLine_NamesSettlementAndManifest()
		{
			string text = KingdomGuestRules.DeliveredChronicleLine("Tamsketh", "6 marble");
			StringAssert.Contains("Tamsketh", text);
			StringAssert.Contains("6 marble", text);
		}

		[Test]
		public void DeliveredLedgerNote_NamesTheManifest()
		{
			StringAssert.Contains("6 marble", KingdomGuestRules.DeliveredLedgerNote("6 marble"));
		}

		// ---- Notables who came and went through an absence ----

		[Test]
		public void NotablePatienceIsShorterThanTheIntervalSoOnlyOneIsEverStanding()
		{
			// The same relation the plain traveller's clock keeps, and for the same reason: it is
			// what makes KingdomRules.PassagesThrough answer "at most one at the gate".
			Assert.Less(KingdomGuestRules.NotableGuestPatienceTicks, KingdomGuestRules.NotableGuestIntervalTicks);
			Assert.Greater(KingdomGuestRules.NotableGuestPatienceTicks, 0L);
		}

		[Test]
		public void ASeasonAwayIsNotablesWhoCameAndLeftLetters()
		{
			// The co-opt's own promise kept through an absence: ignored, they leave a letter and
			// the hook becomes a rumor. Never lost, only relocated -- and never a queue of
			// strangers standing in the square when the founder finally walks in.
			long due = KingdomGuestRules.NotableGuestIntervalTicks;
			long now = due + KingdomRules.TicksPerDay * 200;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				due, now, KingdomGuestRules.NotableGuestIntervalTicks, KingdomGuestRules.NotableGuestPatienceTicks);
			Assert.Greater(passages.Departed, 25, "two hundred days at a seven-day cadence is not one notable");
			Assert.AreEqual(0L, passages.StandingSince);
		}

		[Test]
		public void ANotableWhoJustArrivedKeepsTheirOwnPatienceAndIsStillThere()
		{
			long due = KingdomGuestRules.NotableGuestIntervalTicks;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				due, due + KingdomRules.TicksPerDay, KingdomGuestRules.NotableGuestIntervalTicks, KingdomGuestRules.NotableGuestPatienceTicks);
			Assert.AreEqual(due, passages.StandingSince);
			Assert.AreEqual(0, passages.Departed);
			Assert.AreEqual(due + KingdomGuestRules.NotableGuestPatienceTicks, KingdomGuestRules.DepartTickFor(passages.StandingSince));
		}

		[TestCase(0, "the last of them today")]
		[TestCase(-3, "the last of them today")]
		[TestCase(1, "the last of them a day before you saw it")]
		[TestCase(12, "the last of them 12 days before you saw it")]
		public void WhenPhrase_DatesAgainstTheDayTheFounderIsBeingTold(int daysAgo, string expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.WhenPhrase(daysAgo));
		}

		[Test]
		public void PassedLinesCarryTheCountAndTheHonestDate()
		{
			string chronicle = KingdomGuestRules.PassedChronicleLine(9, "Tamsketh", 5);
			StringAssert.Contains("9 notables", chronicle);
			StringAssert.Contains("Tamsketh", chronicle);
			StringAssert.Contains("5 days before you saw it", chronicle);
			string ledger = KingdomGuestRules.PassedLedgerNote(9, 5);
			StringAssert.Contains("9 notables", ledger);
			StringAssert.Contains("nothing is lost", ledger);
			string book = KingdomGuestRules.PassedGuestbookLine(9, 5);
			StringAssert.Contains("9 notables", book);
			StringAssert.Contains("departed", book);
		}

		[Test]
		public void PassedOutsiderRumor_RelocatesTheHooksRatherThanLosingThem()
		{
			// The two registers do not agree about this: the official book records that the gate
			// went unanswered, and the road records that there is something out there now.
			string rumor = KingdomGuestRules.PassedOutsiderRumor(3, "Tamsketh", 4);
			StringAssert.Contains("Tamsketh", rumor);
			StringAssert.Contains("talk", rumor);
			Assert.AreNotEqual(KingdomGuestRules.PassedChronicleLine(3, "Tamsketh", 4), rumor);
		}

		[Test]
		public void PassedLinesSayNothingWhenNobodyCame()
		{
			Assert.IsNull(KingdomGuestRules.PassedChronicleLine(0, "Tamsketh", 4));
			Assert.IsNull(KingdomGuestRules.PassedOutsiderRumor(0, "Tamsketh", 4));
			Assert.IsNull(KingdomGuestRules.PassedLedgerNote(0, 4));
			Assert.IsNull(KingdomGuestRules.PassedGuestbookLine(-1, 4));
		}

		[Test]
		public void PassedLinesReadSingularForOne()
		{
			string one = KingdomGuestRules.PassedLedgerNote(1, 0);
			StringAssert.Contains("One notable", one);
			Assert.IsFalse(one.Contains("1 notables"), "the singular case read as a plural");
		}

		[Test]
		public void DepartedLedgerNote_DatesTheDepartureAgainstTheDayItActuallyHappened()
		{
			string dated = KingdomGuestRules.DepartedLedgerNote("Aeru", 9);
			StringAssert.Contains("Aeru", dated);
			StringAssert.Contains("9 days before you saw it", dated);
			StringAssert.Contains("nothing is lost", dated);
			// A departure noticed the day it happened drops the clause rather than reading "0
			// days before you saw it".
			Assert.IsFalse(KingdomGuestRules.DepartedLedgerNote("Aeru", 0).Contains("before you saw it"));
			StringAssert.Contains("a day before you saw it", KingdomGuestRules.DepartedLedgerNote("Aeru", 1));
		}
		private static string Slice(string source, string start, string end)
		{
			int at = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(at, 0, start);
			int until = source.IndexOf(end, at + start.Length, StringComparison.Ordinal);
			Assert.Greater(until, at, end);
			return source.Substring(at, until - at);
		}

		private static int Count(string source, string value)
		{
			int count = 0;
			for (int at = 0; ; )
			{
				at = source.IndexOf(value, at, StringComparison.Ordinal);
				if (at < 0) return count;
				count++;
				at += value.Length;
			}
		}

		private static void AssertOrdered(string source, params string[] values)
		{
			int cursor = -1;
			for (int i = 0; i < values.Length; i++)
			{
				int at = source.IndexOf(values[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(at, cursor, values[i]);
				cursor = at;
			}
		}
	}
}
#endif
