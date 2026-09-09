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
		private const string Ground = "Growth/KingdomPlot2.04.Ground.cs";
		private const string Protection = "Growth/KingdomMaterials.15.GroundAndWalls.cs";
		private const string Reservations = "Growth/KingdomWaterDebit.OpenReservations.cs";
		private const string Commission = "Growth/KingdomLab.Commission.cs";
		private const string Settle = "Growth/KingdomLab.Commission.Settle.cs";

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
			// Three genuinely different windows, and the depth counter is the weakest of them:
			// it is zero for the whole reserve-then-commit window, which is where the hazard is.
			Ordered(loader, "private static bool BasinCapacityHeld(KingdomSystem System, GameObject Basin,",
				"LiquidVolume Vessel, out string Reason)",
				"if (KingdomWaterDebit.TransactionOpen)",
				"if (KingdomWaterDebit.VesselReserved(Vessel))",
				"if (GrowthLegHoldsBasin(System, Basin))",
				"KingdomConstructionInputLeaseAuthority.TryCapture(out leases, out failure)",
				"KingdomConstructionInputLeaseAuthority.IsLeased(leases, Basin)");
			Ordered(loader, "if (BasinCapacityHeld(System, basin, vessel, out reason))",
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
			// Guarded like every sibling callback in FinishPlotEffects: a throw from the survey,
			// the anchored lookup or the ledger must not abort the settlement pass after the rung
			// stamp has already been advanced.
			Ordered(Read(Effects), "KingdomCeremonyHeart.OnRungRaised(",
				"KingdomSystem.Guard(\"heart basin capacity\", delegate",
				"ReconcileBasinCapacity(System, Building, Z);");
			// AFTER the seat exchange, and only for ground the seated realm claims. Before the
			// exchange the flat fields -- ClaimedZones, Ledger, LifecycleBook -- still answer for
			// the city the founder just left, so a second city's basin would be dedicated into the
			// wrong ledger and an unsettled water leg held by the DESTINATION city would be missed.
			Ordered(Read(Events), "KingdomPlots.RecoverLegacyPlotFinalEffects(this, E.Zone)",
				"if (TrySeat(E.Zone))",
				"Guard(\"heart basin capacity\", delegate",
				"if (E.Zone != null && ClaimedZones != null",
				"&& ClaimedZones.Contains(E.Zone.ZoneID))",
				"KingdomPlots.ReconcileBasinCapacity(this, E.Zone);");
			string catalogue = Read(Buildings);
			Contains(catalogue, "NOTHING HERE STORES WATER",
				"THE FIRST BASIN IS THE EXCEPTION, and it is not a rung.",
				"KingdomPlotRules.HeartBasinCapacityForRung: 16/48/160/512/1024");
		}

		[Test]
		public void AnOpenReservationOnTheBasinIsSeenPerVesselAndIsDroppedWhenItSettles()
		{
			string reservations = Read(Reservations);
			// The per-vessel question. A depth counter cannot answer it: every OpenTransactions
			// increment is paired with a finally inside one method body, so the counter is zero
			// for the whole window between a reserved receipt being handed back and its commit.
			Contains(reservations,
				"internal static bool VesselReserved(LiquidVolume Vessel)",
				"!debit.HoldsVessels",
				"ReferenceEquals(Entries[i].Vessel, Vessel)");
			// Weak references plus a live state re-read, so an abandoned receipt cannot leave a
			// permanent refusal behind the way a strong per-vessel set would.
			Contains(reservations,
				"List<WeakReference<KingdomWaterDebit>> OpenReservations",
				"OpenReservations.Add(new WeakReference<KingdomWaterDebit>(this));");
			Ordered(reservations, "if (!OpenReservations[i].TryGetTarget(out debit) || debit == null",
				"|| !debit.HoldsVessels)", "OpenReservations.RemoveAt(i);");
			// Registered when reservation returns still reserved.
			Ordered(Read("Growth/KingdomWaterDebit.cs"),
				"if (total != debit.Amount)", "return debit.RegisterReservation();");
			// Dropped on every terminal transition: commit (unless the caller declared a
			// compensation window, below), rollback, a cancelled reservation and a failed one.
			foreach (string path in new[] { "Growth/KingdomWaterDebit.Commit.cs",
				"Growth/KingdomWaterDebit.RollbackAndVerification.cs" })
				Ordered(Read(path), "finally", "OpenTransactions--;", "ReleaseReservation();");
			Ordered(Read("Growth/KingdomWaterDebit.RollbackAndVerification.cs"),
				"KingdomWaterDebitAction.CancelReservation", "ReleaseReservation();");
			Ordered(Read("Growth/KingdomWaterDebit.ClaimsAndHelpers.cs"),
				"private KingdomWaterDebit FailReservation(", "Entries.Clear();",
				"ReleaseReservation();");
		}

		[Test]
		public void AnUnsettledArrivalWaterLegOnTheBasinHoldsItsCapacityStill()
		{
			// leg.Capacity is the vessel's MaxVolume at preparation and the arrival endpoint
			// re-proves it before settling, so a widen in between strands the arrival forever.
			// This is the one MaxVolume freeze that survives a save, which is why it is read
			// alongside the two in-memory doors.
			Contains(Read("Growth/KingdomGrowth.z06.ArrivalPreparation.cs"),
				"vessel.MaxVolume, vessel.Volume,");
			Contains(Read("Growth/KingdomGrowth.z10.ArrivalProofAndDomainHash.cs"),
				"MaxVolume != leg.Capacity");
			string loader = Read(Loader);
			Ordered(loader, "private static bool GrowthLegHoldsBasin(KingdomSystem System, GameObject Basin)",
				"KingdomGrowthBook growth = System?.LifecycleBook?.Growth;",
				"string id = Basin.IDIfAssigned;",
				"UnsettledLegNames(growth.HeartbeatOp, id)",
				"UnsettledLegNames(growth.ArrivalOp, id)",
				"UnsettledLegNames(growth.DepartureOp, id)",
				"UnsettledLegNames(growth.DeliveryOp, id)",
				"UnsettledLegNames(growth.FetchOp, id)",
				"UnsettledLegNames(growth.MillOp, id)");
			// Only legs at or after the cursor are unsettled; the ones behind it are already done.
			Ordered(loader, "private static bool UnsettledLegNames(KingdomGrowthOperation Operation, string Id)",
				"i = (Operation.WaterCursor > 0 ? Operation.WaterCursor : 0);",
				"i < Operation.WaterLegs.Count; i++",
				"string.Equals(leg.ContainerId, Id, StringComparison.Ordinal)");
		}

		[Test]
		public void TheDedicatedBasinStillReadsAsBareGroundAndIsNeverToldToBeStruck()
		{
			// The dedication puts KingdomStores == 1 on the basin, which the settlement-works
			// clause would otherwise read as Held -- and a Held basin refuses the very rungs that
			// are supposed to be raised around it. The relic exemption therefore stands ABOVE it.
			string ground = Read(Ground);
			Ordered(ground, "public static KingdomPlotRules.GroundKind ReadObject(GameObject Object)",
				"Object.GetIntProperty(HeartStakeProperty) == 1 || Object.GetIntProperty(HeartRelicProperty) == 1",
				"return KingdomPlotRules.GroundKind.Bare;",
				"Object.GetIntProperty(\"KingdomBuilt\") == 1 || Object.GetIntProperty(\"KingdomStores\") == 1",
				"return KingdomPlotRules.GroundKind.Held;");
			// And the founder-facing refusal for the basin's cell never names a remedy the mod
			// always denies: a relic can be struck by nobody.
			string protection = Read(Protection);
			Ordered(protection, "public static bool IsProtected(GameObject Object, out string Reason)",
				"if (Object.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)",
				"Reason = \"The first basin was poured at the rite and is never cleared or struck. "
					+ "Build around it.\";",
				"Object.GetIntProperty(\"KingdomBuilt\") == 1 || Object.GetIntProperty(\"KingdomCitizen\") == 1");
			Contains(Read("Growth/KingdomArchitectureStamper.Components.cs"),
				"private static bool TryStrikeRemovable(GameObject Item,",
				"Item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)");
		}

		[Test]
		public void TheBasinIsReconciledOnlyOnGroundTheSeatedRealmActuallyClaims()
		{
			// The seat exchange writes the destination settlement over the flat fields, so a
			// reconciliation asked before it answers for the wrong city: the dedication lands in
			// the departed city's water accounts and GrowthLegHoldsBasin reads the departed
			// city's LifecycleBook, missing a prepared arrival leg the destination city holds.
			string events = Read(Events);
			int ownership = events.LastIndexOf("ExternalOwnershipAllows(E.Zone)",
				StringComparison.Ordinal);
			int seat = events.IndexOf("if (TrySeat(E.Zone))", StringComparison.Ordinal);
			int basin = events.IndexOf("Guard(\"heart basin capacity\", delegate",
				StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(ownership, 0);
			ClassicAssert.Greater(seat, ownership);
			ClassicAssert.Greater(basin, seat);
			ClassicAssert.AreEqual(1, Regex.Matches(events,
				@"Guard\(""heart basin capacity""").Count);
			// And the reconciler refuses unclaimed ground itself, so no caller can reach a
			// foreign, seceded, exiled or not-yet-seated heart by asking at the wrong moment.
			Ordered(Read(Loader),
				"internal static bool ReconcileBasinCapacity(KingdomSystem System, Zone Z)",
				"System != null && System.Founded && Z != null",
				"System.ClaimedZones != null && System.ClaimedZones.Contains(Z.ZoneID)",
				"TryStandingHeartRoot(Z, out root)",
				"ReconcileBasinCapacity(System, root, Z);");
			// The root-taking overload is the entry the rung's own completion uses, and that
			// completion resolves the SEATED realm rather than the one that owns the ground, so
			// the same claim gate is proved there too and not merely at its callers.
			Ordered(Read(Loader),
				"internal static bool ReconcileBasinCapacity(KingdomSystem System, GameObject Root, Zone Z)",
				"if (System == null || !System.Founded || Z == null || !GameObject.Validate(Root)",
				"|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID)",
				"|| Root.GetIntProperty(HeartPlotProperty) != 1) return false;");
		}

		[Test]
		public void ACommittedReceiptStillOpenToCompensationKeepsItsVesselHold()
		{
			// The adversary: the hall commits the water, runs the bits callbacks, and only THEN
			// decides whether to compensate. A widen landing in that span makes Rollback refuse
			// (AllStillCommitted asserts MaxVolume == OriginalMaxVolume), turning a recoverable
			// interruption into water the founder can never get back.
			string reservations = Read(Reservations);
			Contains(reservations, "private bool CompensationWindowOpen;",
				"public void BeginCompensationWindow()",
				"public void EndCompensationWindow()");
			Ordered(reservations, "private bool HoldsVessels",
				"State == KingdomWaterDebitState.Reserved",
				"(State == KingdomWaterDebitState.Committed && CompensationWindowOpen)");
			Contains(Read("Growth/KingdomWaterDebit.ReservationVerification.cs"),
				"entry.Vessel != null && entry.Vessel.MaxVolume == entry.OriginalMaxVolume");
			// Commit drops the hold as before, EXCEPT inside a declared window.
			Ordered(Read("Growth/KingdomWaterDebit.Commit.cs"), "finally", "OpenTransactions--;",
				"if (State != KingdomWaterDebitState.Committed || !CompensationWindowOpen)",
				"ReleaseReservation();");
			// Opt-in, and never inferred from a commit: a caller that commits and then finishes
			// its own work -- a construction whose completed rung widens the very basin it just
			// drained -- must not hold that vessel, or the rung could never widen anything.
			Contains(reservations, "Opt-in, and never inferred");
			// Closing the window releases the hold, and a still-open RESERVATION is never dropped
			// by it: only the reservation's own terminal transition may do that.
			Ordered(reservations, "public void EndCompensationWindow()",
				"CompensationWindowOpen = false;",
				"if (State != KingdomWaterDebitState.Reserved) ReleaseReservation();");
			string settle = Read(Settle);
			Ordered(settle, "debit.BeginCompensationWindow();", "try", "debit.Commit();",
				"KingdomMaterialDebitResult bitResult = bitDebit.Commit();",
				"bool waterRestored = debit.Rollback();",
				"finally", "debit.EndCompensationWindow();");
			// The window closes only below the last compensation the caller can reach.
			int close = settle.IndexOf("debit.EndCompensationWindow();",
				StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(close, 0);
			StringAssert.DoesNotContain("debit.Rollback()", settle.Substring(close));
		}

		[Test]
		public void EveryReceiptThatCanRefundAfterItsOwnCallbacksHoldsItsVesselsAcrossThem()
		{
			// The adversary, once per caller: the caller commits, runs its OWN callbacks -- publishes,
			// material commits, rows, a destroyed seed, an enrolment part, a standing batch -- and only
			// then decides whether to compensate. A basin widening landing in that span makes Rollback
			// refuse (AllStillCommitted asserts MaxVolume == OriginalMaxVolume), so the founder's water
			// is gone for good. Each caller must therefore open its window BEFORE the commit and close
			// it in an ENCLOSING finally, so no exit -- refusal, exception or success -- can either
			// drop the hold early or keep it after the caller's last compensation.
			foreach (var caller in PostCommitRefundCallers)
			{
				string source = Compact(Read(caller.File));
				string begin = caller.Receipt + ".BeginCompensationWindow();";
				string end = caller.Receipt + ".EndCompensationWindow();";
				ClassicAssert.AreEqual(1, Occurrences(source, begin), caller.File + " opens once");
				ClassicAssert.AreEqual(1, Occurrences(source, end), caller.File + " closes once");
				int opened = source.IndexOf(begin, StringComparison.Ordinal);
				int committed = source.IndexOf(Compact(caller.Commit), StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(committed, 0, caller.File + " commit token");
				ClassicAssert.Less(opened, committed, caller.File + " opens before its commit");
				// The close is lexically inside a finally block, not merely somewhere below.
				ClassicAssert.IsTrue(Regex.IsMatch(source,
					@"finally \{[^{}]*" + Regex.Escape(end)),
					caller.File + " closes its window in an enclosing finally");
				// And nothing below the close still tries to compensate that same receipt: a
				// refund reached after the hold is dropped is the very hazard this window covers.
				int closed = source.IndexOf(end, StringComparison.Ordinal);
				StringAssert.DoesNotContain(caller.Receipt + ".Rollback()", source.Substring(closed));
			}
		}

		[Test]
		public void TheConstructionWindowSpansTheMaterialCommitAndTheRefundThatFollowsIt()
		{
			// Funding commits the water, publishes, commits the MATERIAL -- real object callbacks --
			// and only then may refund a clean no-material attempt. The whole span is one window.
			Ordered(Read(Funding), "Water.BeginCompensationWindow();", "try",
				"bool waterCommitted = Water.Commit();",
				"KingdomMaterialDebitResult result = Material.Commit();",
				"bool rolledBack = Water.Rollback();",
				"finally", "Water.EndCompensationWindow();");
		}

		[Test]
		public void TheSowingWindowSpansTheLaidRowsAndTheSpentSeedAndTheRefundThatFollowsThem()
		{
			Ordered(Read(Sowing), "debit.BeginCompensationWindow();", "try", "!debit.Commit())",
				"laid = LayRows(zone, work, row, rows);",
				"bool destroyed = Seed.Destroy(null, Silent: true);",
				"bool waterRestored = debit.Rollback();",
				"finally", "debit.EndCompensationWindow();");
		}

		[Test]
		public void TheHallsCommissionSettlesItsWindowInAFinallyAndNowhereElse()
		{
			// The hall's window used to be closed by a plain statement, so a throw anywhere in the
			// span -- a live-body read, a persisted receipt write, a bit commit, a governance
			// republish -- unwound with the receipt Committed and the window still open, and every
			// vessel it bound stayed held for a caller that no longer existed. The span now lives
			// in its own shard so a real finally can close it, and the commission shard itself
			// holds no window at all.
			string commission = Compact(Read(Commission));
			StringAssert.DoesNotContain("BeginCompensationWindow", commission);
			StringAssert.DoesNotContain("EndCompensationWindow", commission);
			Ordered(commission, "if (!SettleCommissionFunding(Actor, job, frozen, debit, bitDebit,"
				+ " bitCost, out waterExact, out bitsExact)) return;");
			// And inside the shard, every exit reached between the commit and the finally still
			// compensates the receipt first: the finally covers the throw, not the refusals.
			string settle = Compact(Read(Settle));
			int opened = settle.IndexOf("debit.Commit();", StringComparison.Ordinal);
			int closed = settle.IndexOf("finally { ", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(opened, 0);
			ClassicAssert.Greater(closed, opened);
			string[] exits = settle.Substring(opened, closed - opened).Split(
				new[] { "return false;" }, StringSplitOptions.None);
			for (int i = 0; i < exits.Length - 1; i++)
				StringAssert.Contains("debit.Rollback()", exits[i],
					"commission exit " + (i + 1) + " inside the window compensates first");
			ClassicAssert.AreEqual(4, exits.Length, "three refusals and the settled fall-through");
		}

		[Test]
		public void TheFinishedRungIsFreeToWidenTheBasinTheFundingItPaidForDrained()
		{
			// The other half of the same law: a window that OUTLIVED its caller would deadlock the
			// very rung it paid for. The rung's widening runs in the plot effects pass, after Fund
			// has returned and its finally has dropped the hold, and that pass opens no water debit
			// of its own -- so the completion is never asking a hold it is itself holding.
			string effects = Read(Effects);
			Ordered(effects, "KingdomSystem.Guard(\"heart basin capacity\", delegate",
				"ReconcileBasinCapacity(System, Building, Z);");
			StringAssert.DoesNotContain("KingdomWaterDebit", effects);
			StringAssert.DoesNotContain("CompensationWindow", effects);
			string funding = Read(Funding);
			StringAssert.DoesNotContain("ReconcileBasinCapacity", funding);
			ClassicAssert.IsTrue(Regex.IsMatch(Compact(funding),
				@"finally \{[^{}]*Water\.EndCompensationWindow\(\);"),
				"the funding hold cannot survive the method that took it");
		}

		private const string Funding = "Growth/KingdomConstruction.Funding.cs";
		private const string Sowing = "Growth/KingdomCrops.02.Sowing.cs";

		private sealed class PostCommitRefundCaller
		{
			internal string File;
			internal string Receipt;
			internal string Commit;
		}

		private static readonly PostCommitRefundCaller[] PostCommitRefundCallers =
		{
			new PostCommitRefundCaller { File = Funding, Receipt = "Water",
				Commit = "bool waterCommitted = Water.Commit();" },
			new PostCommitRefundCaller { File = Sowing, Receipt = "debit",
				Commit = "|| !debit.Commit())" },
			new PostCommitRefundCaller { File = "Growth/KingdomAnnexe.Enrollment.cs",
				Receipt = "debit", Commit = "if (!debit.Commit())" },
			new PostCommitRefundCaller { File = Settle,
				Receipt = "debit", Commit = "debit.Commit();" },
			new PostCommitRefundCaller { File = "Growth/KingdomLab.Funding.cs",
				Receipt = "debit", Commit = "debit.Commit();" },
			new PostCommitRefundCaller { File = "Growth/KingdomLab.RemovalFunding.cs",
				Receipt = "debit", Commit = "debit.Commit();" },
			new PostCommitRefundCaller { File = "Growth/KingdomLab.RemovalOffer.cs",
				Receipt = "debit", Commit = "debit.Commit();" }
		};

		private static int Occurrences(string source, string token)
		{
			int count = 0;
			for (int at = source.IndexOf(token, StringComparison.Ordinal); at >= 0;
				at = source.IndexOf(token, at + token.Length, StringComparison.Ordinal)) count++;
			return count;
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
