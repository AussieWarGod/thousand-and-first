#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomGuestRulesTests
	{
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
		[TestCase(5, 4, KingdomPlotRules.PlotSize.Small)]
		[TestCase(4, 4, KingdomPlotRules.PlotSize.Small)]
		[TestCase(8, 6, KingdomPlotRules.PlotSize.Medium)]
		[TestCase(6, 6, KingdomPlotRules.PlotSize.Medium)]
		[TestCase(12, 9, KingdomPlotRules.PlotSize.Large)]
		[TestCase(9, 9, KingdomPlotRules.PlotSize.Large)]
		[TestCase(20, 14, KingdomPlotRules.PlotSize.Huge)]
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

		[TestCase(false, 0, false)]
		[TestCase(false, 99, false)]
		[TestCase(true, -1, false)]
		[TestCase(true, 0, true)]
		[TestCase(true, KingdomGuestRules.RoadRiskPercent - 1, true)]
		[TestCase(true, KingdomGuestRules.RoadRiskPercent, false)]
		[TestCase(true, 99, false)]
		public void HaulAtRisk_OnlyFiresWhileARaidThreatensAndOnlyBelowTheRiskPercent(bool raidActive, int riskRoll, bool expected)
		{
			Assert.AreEqual(expected, KingdomGuestRules.HaulAtRisk(raidActive, riskRoll));
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
		public void LostChronicleLine_NamesSettlementFactionAndManifest()
		{
			string text = KingdomGuestRules.LostChronicleLine("Tamsketh", "the Snapjaws", "6 marble");
			StringAssert.Contains("Tamsketh", text);
			StringAssert.Contains("the Snapjaws", text);
			StringAssert.Contains("6 marble", text);
		}

		[Test]
		public void LostLedgerNote_NamesTheManifest()
		{
			StringAssert.Contains("6 marble", KingdomGuestRules.LostLedgerNote("6 marble"));
		}

		[Test]
		public void DeliveredLedgerNote_NamesTheManifest()
		{
			StringAssert.Contains("6 marble", KingdomGuestRules.DeliveredLedgerNote("6 marble"));
		}
	}
}
#endif
