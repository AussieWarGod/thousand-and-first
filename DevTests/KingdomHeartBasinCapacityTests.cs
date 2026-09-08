#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// The ladder itself is pure and executed here. Everything physical -- the relic-slot
	// dedication, the reconciler, the loader attribute pair and the two live call sites -- is a
	// SOURCE contract: no zone, vessel, survey or engine callback runs in this fixture.
	[TestFixture]
	public sealed class KingdomHeartBasinCapacityTests
	{
		private const string Loader = "Growth/KingdomPlotHeartRules.Loader.cs";
		private const string Identity = "Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs";
		private const string Marks = "Growth/KingdomPlot2.07c.FoundingHeartMarks.cs";
		private const string Gallery = "Debug/KingdomArchitectureGalleryWishes.CanvasAndAuthority.cs";
		private const string Verification = "Growth/KingdomArchitectureStamper.Verification.cs";
		private const string Retag = "Growth/KingdomArchitectureStamper.UpgradeRetag.cs";
		private const string Capture = "Growth/KingdomSurvey.01.Capture.cs";
		private const string Effects = "Growth/KingdomPlot2.34.EffectsAndFurnishing.cs";
		private const string Events = "Core/KingdomSystem.z20.Events.cs";
		private const string Blueprints = "RuntimeData/ObjectBlueprints.xml";
		private const string Buildings = "RuntimeData/KingdomBuildings.xml";

		[Test]
		public void TheBasinLadderIsExactAndClimbsStrictlyAndIsSilentOffTheLadder()
		{
			ClassicAssert.AreEqual(16, KingdomPlotRules.HeartBasinCapacityForRung(1));
			ClassicAssert.AreEqual(48, KingdomPlotRules.HeartBasinCapacityForRung(2));
			ClassicAssert.AreEqual(160, KingdomPlotRules.HeartBasinCapacityForRung(3));
			ClassicAssert.AreEqual(512, KingdomPlotRules.HeartBasinCapacityForRung(4));
			ClassicAssert.AreEqual(1024, KingdomPlotRules.HeartBasinCapacityForRung(5));
			for (int rung = 2; rung <= 5; rung++)
				ClassicAssert.Greater(KingdomPlotRules.HeartBasinCapacityForRung(rung),
					KingdomPlotRules.HeartBasinCapacityForRung(rung - 1), "rung " + rung);
			foreach (int off in new[] { int.MinValue, -1, 0, 6, 7, int.MaxValue })
				ClassicAssert.AreEqual(0, KingdomPlotRules.HeartBasinCapacityForRung(off),
					"rung " + off + " is not on the heart's ladder");
		}

		[Test]
		public void EveryRungBelowTheTopStopsShortOfTheGateAboveTheOneItOpens()
		{
			// Population is held at the City gate so only the CAPACITY term can move the answer.
			ClassicAssert.AreEqual(GrowthStage.Steading,
				KingdomRules.StageFor(50, KingdomPlotRules.HeartBasinCapacityForRung(1)));
			ClassicAssert.AreEqual(GrowthStage.Steading,
				KingdomRules.StageFor(50, KingdomPlotRules.HeartBasinCapacityForRung(2)));
			ClassicAssert.AreEqual(GrowthStage.Village,
				KingdomRules.StageFor(50, KingdomPlotRules.HeartBasinCapacityForRung(3)));
			ClassicAssert.AreEqual(GrowthStage.Town,
				KingdomRules.StageFor(50, KingdomPlotRules.HeartBasinCapacityForRung(4)));
			ClassicAssert.AreEqual(GrowthStage.City,
				KingdomRules.StageFor(50, KingdomPlotRules.HeartBasinCapacityForRung(5)));
		}

		[Test]
		public void TheRiteGroundBasinAloneCarriesFiveSettlersIntoASteading()
		{
			int rite = KingdomPlotRules.HeartBasinCapacityForRung(1);
			ClassicAssert.AreEqual(GrowthStage.Steading,
				KingdomRules.StageFor(5, rite));
			ClassicAssert.AreEqual(GrowthStage.Camp,
				KingdomRules.StageFor(4, rite));
			ClassicAssert.AreEqual(GrowthStage.Camp,
				KingdomRules.StageFor(5, rite - 1));
		}

		[Test]
		public void ANeglectedBasinAtEveryRungLeaksLessThanThatRungsDailyWaterBill()
		{
			var gates = new[]
			{
				new { Rung = 2, Population = 5, Stage = GrowthStage.Steading },
				new { Rung = 3, Population = 12, Stage = GrowthStage.Village },
				new { Rung = 4, Population = 25, Stage = GrowthStage.Town },
				new { Rung = 5, Population = 50, Stage = GrowthStage.City }
			};
			foreach (var gate in gates)
			{
				int capacity = KingdomPlotRules.HeartBasinCapacityForRung(gate.Rung);
				int lost = KingdomWearRules.Leaked(capacity, capacity,
					KingdomMaterialRules.MaxWearPercent, 1);
				int bill = KingdomRules.UpkeepDrams(gate.Population, gate.Stage);
				ClassicAssert.Greater(bill, lost,
					"rung " + gate.Rung + " leaks " + lost + " against a bill of " + bill);
			}
		}

		[Test]
		public void TheRelicSlotDedicatesTheStoreAndNothingElseDoes()
		{
			string identity = Read(Identity);
			Contains(identity, "private static bool FoundingHeartSlotStores(int Slot)",
				"return Slot == KingdomFoundingHeartRules.RelicSlot;");
			Ordered(Read(Marks), "created.SetIntProperty(FoundingHeartSlotMark(Slot), 1);",
				"if (FoundingHeartSlotStores(Slot))",
				"created.SetIntProperty(\"KingdomStores\", 1);");
			// The Debug gallery photographs a synthetic copy of the same blueprint. It must never
			// be able to enter a settlement's water accounts, which is why the dedication is not
			// authored on r_KingdomFirstBasin and not stamped by the gallery.
			StringAssert.DoesNotContain("KingdomStores", Read(Gallery));
			StringAssert.Contains("KingdomStores", Read(Marks));
			string basin = Slice(Read(Blueprints), "<object Name=\"r_KingdomFirstBasin\"", "</object>");
			StringAssert.Contains("MaxVolume=\"16\"", basin);
			StringAssert.DoesNotContain("KingdomStores", basin);
		}

		[Test]
		public void AnExistingAuthorityComponentIsStampedZeroSoTheSurveyNeverStripsTheBasin()
		{
			Contains(Read(Verification), "Item.SetIntProperty(KingdomPlots.PlotPartProperty, "
				+ "Placement.ExistingAuthority ? 0 : 1);");
			Contains(Read(Retag), "Item.SetIntProperty(KingdomPlots.PlotPartProperty, "
				+ "AfterPlacement.ExistingAuthority ? 0 : 1);");
			// The legacy sweep only releases a mark from a NON-root plot piece, which is exactly
			// what the two setters above keep the basin from ever being.
			Contains(Read(Capture),
				"if (item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1 "
					+ "&& item.GetIntProperty(\"KingdomBuilt\") != 1 "
					+ "&& item.GetIntProperty(\"KingdomStores\") == 1)");
		}

		[Test]
		public void TheReconcilerRaisesCapacityAndNeverLowersIt()
		{
			string loader = Read(Loader);
			Ordered(loader, "int capacity = KingdomPlotRules.HeartBasinCapacityForRung(rung);",
				"if (capacity <= 0) return false;",
				"KingdomArchitectureStamper.TryExactAnchoredComponent(Root, Z, HeartBasinRole,",
				"basin.Blueprint != HeartRelicBlueprint",
				"basin.GetIntProperty(HeartRelicProperty) != 1",
				"if (vessel.MaxVolume >= capacity)", "return true;",
				"vessel.MaxVolume = capacity;");
			// One write, and it is the raise. A lowering assignment would show up here.
			ClassicAssert.AreEqual(1,
				Regex.Matches(loader, @"MaxVolume\s*=[^=]").Count,
				"the reconciler writes MaxVolume exactly once");
			StringAssert.Contains("vessel.MaxVolume = capacity;", loader);
		}

		[Test]
		public void AHeldBasinIsSkippedAndSaidOnceAndTheFlagClearsWhenTheHoldLifts()
		{
			string loader = Read(Loader);
			Ordered(loader, "private static bool BasinCapacityHeld(GameObject Basin, out string Reason)",
				"if (KingdomWaterDebit.TransactionOpen)",
				"KingdomConstructionInputLeaseAuthority.TryCapture(out leases, out failure)",
				"KingdomConstructionInputLeaseAuthority.IsLeased(leases, Basin)");
			Ordered(loader, "if (BasinCapacityHeld(basin, out reason))",
				"AnnounceBasinCapacityHold(System, basin, capacity, reason);", "return false;");
			Ordered(loader, "private static void AnnounceBasinCapacityHold(",
				"if (Basin.GetIntProperty(BasinCapacityHeldProperty) == 1) return;",
				"Basin.SetIntProperty(BasinCapacityHeldProperty, 1);");
			Ordered(loader, "private static void ReleaseBasinCapacityHold(GameObject Basin)",
				"if (Basin.GetIntProperty(BasinCapacityHeldProperty) != 1) return;",
				"Basin.RemoveIntProperty(BasinCapacityHeldProperty);");
			// The hold is released on both non-blocked exits, so the founder is never told twice
			// and never left with a stale notice after the block lifts.
			ClassicAssert.AreEqual(2,
				Regex.Matches(loader, @"ReleaseBasinCapacityHold\(basin\);").Count);
		}

		[Test]
		public void EveryWaterTransactionDoorIsOpenedAndClosedInPairs()
		{
			string debit = Read("Growth/KingdomWaterDebit.cs")
				+ Read("Growth/KingdomWaterDebit.Commit.cs")
				+ Read("Growth/KingdomWaterDebit.RollbackAndVerification.cs")
				+ Read("Growth/KingdomWaterDebit.ClaimsAndHelpers.cs");
			ClassicAssert.AreEqual(3, Regex.Matches(debit, @"OpenTransactions\+\+;").Count);
			ClassicAssert.AreEqual(3, Regex.Matches(debit, @"OpenTransactions--;").Count);
			Contains(Read("Growth/KingdomWaterDebit.ClaimsAndHelpers.cs"),
				"internal static bool TransactionOpen", "return OpenTransactions > 0;");
			Ordered(Read("Growth/KingdomWaterDebit.cs"), "OpenTransactions++;", "try",
				"finally { OpenTransactions--; }");
			foreach (string path in new[] { "Growth/KingdomWaterDebit.Commit.cs",
				"Growth/KingdomWaterDebit.RollbackAndVerification.cs" })
				Ordered(Read(path), "Operating = true;", "OpenTransactions++;", "finally",
					"Operating = false;", "OpenTransactions--;");
		}

		[Test]
		public void TheLoaderCarriesItsAttributePairAndNeverThawsUnvisitedGround()
		{
			string loader = Read(Loader);
			Ordered(loader, "[HasCallAfterGameLoaded]",
				"public static class KingdomHeartBasinLoader", "[CallAfterGameLoaded]",
				"public static void ReconcileStandingHeartBasin()");
			Contains(loader, "Zone zone = The.ZoneManager?.ActiveZone;");
			foreach (string thaw in new[] { "GetZone(", "LoadZone", "ZoneManager.Get",
				"CachedZones[" }) StringAssert.DoesNotContain(thaw, loader);
			// Nothing is destroyed, moved or emptied by a capacity reconciliation.
			foreach (string forbidden in new[] { "Obliterate(", ".Destroy(", "RemoveObject(",
				"KingdomLiquids.Drain(", "vessel.Volume =" })
				StringAssert.DoesNotContain(forbidden, loader);
		}

		[Test]
		public void BothLiveStampsAndTheCatalogueAgreeWhereTheWaterIs()
		{
			Ordered(Read(Effects), "KingdomCeremonyHeart.OnRungRaised(",
				"ReconcileBasinCapacity(System, Building, Z);");
			Ordered(Read(Events), "KingdomPlots.RecoverLegacyPlotFinalEffects(this, E.Zone)",
				"KingdomPlots.ReconcileBasinCapacity(this, E.Zone);");
			string catalogue = Read(Buildings);
			Contains(catalogue, "NOTHING HERE STORES WATER",
				"THE FIRST BASIN IS THE EXCEPTION, and it is not a rung.",
				"KingdomPlotRules.HeartBasinCapacityForRung: 16/48/160/512/1024");
		}

		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static string Compact(string value)
		{
			return Regex.Replace(value, @"\s+", " ").Trim();
		}

		private static string Slice(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}

		private static void Contains(string source, params string[] tokens)
		{
			foreach (string token in tokens)
				StringAssert.Contains(Compact(token), Compact(source), token);
		}

		private static void Ordered(string source, params string[] tokens)
		{
			string compact = Compact(source);
			int cursor = 0;
			foreach (string token in tokens)
			{
				string expected = Compact(token);
				int at = compact.IndexOf(expected, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, token);
				cursor = at + expected.Length;
			}
		}
	}
}
#endif
