using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The third phase of the sealed camp-heart run (issue #159): the SECOND consecutive climb by
	/// the improvement route, rung 2 to rung 3, the waterstone into the moot yard.
	///
	/// <para>WHY THIS SEAM IS THE POINT. Every rung above the first climbs by the improvement
	/// route (<c>Growth/KingdomUpgrade.26.HeartRung.cs</c>,
	/// <c>TrySettleImprovementHeartRung</c>); the plot/rite route writes
	/// <c>r_TAF_HeartRung</c> only at the founding, because
	/// <c>Growth/KingdomPlot2.10.Commission.cs</c> states the heart is founded, not commissioned.
	/// Rung 2 -> 3 is therefore the first transition that runs that settlement helper TWICE in a
	/// row, with the retag census of the first climb already standing when the second begins.
	/// OPEN CASE 3, carried verbatim from the runbook and unanswered until this runs natively:
	/// whether a second consecutive improvement-route rung settles its stamp, widens the basin
	/// and retags its components exactly once, or whether the first climb's settled state
	/// interferes with the second.</para>
	///
	/// <para>Nothing here begins, funds, advances or applies anything: the moot yard is assessed,
	/// begun, debited, laboured and handed over by the real settlement pass on the turns the
	/// persona's own third <c>advance</c> spends. This phase only reads what it did.</para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>The rung-3 standing state: the moot yard is the heart, the zone records
			/// rung three, the basin holds what rung three is worth, the authored footprint grew
			/// from 8x6 to 12x10, the improvement job closed Complete with no failure and no
			/// inspection, and no layout or upgrade quarantine was raised on the way.</summary>
			private void ClimbRung3()
			{
				RecordJobProgress();
				GameObject standing = StandingHeart();
				Require(KingdomUpgrade.DesignKeyOf(standing) == ThirdRungKey,
					"taf-camp-rung3-unfinished: the heart did not finish its real paid climb to "
						+ "the moot yard; key=" + KingdomUpgrade.DesignKeyOf(standing));
				Require(KingdomUpgrade.IsFunctionallyBuilt(standing),
					"taf-camp-rung3-nonfunctional: the moot yard still has unfinished authority");
				Require(KingdomPlots.HeartRung(Zone) == 3,
					"taf-camp-rung3-unsettled: the moot yard stands but the recorded rung is "
						+ KingdomPlots.HeartRung(Zone));
				Require(BasinCapacity(standing) == "160",
					"taf-camp-rung3-basin-unsettled: rung three is worth 160 drams; got "
						+ BasinCapacity(standing));
				RequireGrownFootprint(standing);
				RequireCompletedImprovement();
				RequireSettledOnceEachClimb(standing);
				RequireNoQuarantine(standing);
				RequireRecoveredAfterSecondClimb();
				RequireStoreIdentity();
				List<GameObject> bodies;
				List<KingdomCampHeartNativeCensus.Unit> present = ContentUnits(out bodies);
				RequireHeld(RetainedBrush, present, "brush");
				RequireSameBodies(RetainedBrushBodies, bodies, "brush");
				RequireAbsent(MintedRung3, present, "rung-3 bill");
				GameObject fire = FireIn(standing);
				Require(fire != null,
					"taf-camp-rung3-fire-absent: no camp fire stands inside the moot yard");
				Require(Offset(fire.CurrentCell) == Offset(FireCell),
					"taf-camp-rung3-fire-moved: the camp fire left its rite-relative cell: "
						+ Offset(FireCell) + " -> " + Offset(fire.CurrentCell));
				Evidence.Append("\nphase3 tick=").Append(Game.TimeTicks)
					.Append("; standing=").Append(standing.IDIfAssigned)
					.Append("; key=").Append(KingdomUpgrade.DesignKeyOf(standing))
					.Append("; stage=").Append(System.Stage)
					.Append("; population=").Append(System.Population)
					.Append("; craft=").Append(KingdomZoning.Tech(System))
					.Append("; zone rung read=").Append(KingdomPlots.HeartRung(Zone))
					.Append("; basin capacity read=").Append(BasinCapacity(standing))
					.Append("; store=").Append(StoreId)
					.Append("; store raw custody census=")
					.Append(KingdomCampHeartNativeCensus.Describe(present))
					.Append("; retained unasked units=")
					.Append(KingdomCampHeartNativeCensus.Describe(RetainedBrush))
					.Append("; fire=").Append(fire.IDIfAssigned)
					.Append('@').Append(Offset(fire.CurrentCell));
			}

			/// <summary>
			/// CASE 3 ITSELF, ASSERTED RATHER THAN INFERRED. The heart's rung effects are settled
			/// once per climb behind an at-most-once 0/1/2 marker set on the SUCCESSOR of that
			/// climb (<c>Growth/KingdomPlotHeartRules.Settle.cs</c>, the property declared at
			/// <c>Growth/KingdomPlot2.03.RegistryAndDeclarations.cs</c>): 0 nothing owed, 1 the
			/// ceremony is in flight, 2 settled. A second consecutive improvement-route rung must
			/// therefore leave TWO distinct bodies each carrying exactly 2 -- the waterstone this
			/// run climbed to, and the moot yard it climbed to next -- and a marker still at 1 is
			/// an interrupted ceremony, honestly lost, not a settle.
			/// <para>The name is a string here because production declares it private. The
			/// DevTests pin holds the two spellings together, so a rename fails there rather than
			/// making this read a property nothing writes.</para>
			/// </summary>
			private void RequireSettledOnceEachClimb(GameObject Standing)
			{
				int settled = Standing.GetIntProperty(HeartEffectProperty);
				Require(settled == 2, "taf-camp-rung3-ceremony-unsettled: the moot yard's "
					+ "at-most-once rung marker reads " + settled
					+ ", not the settled 2 (0 owed, 1 in flight, 2 settled)");
				Require(GameObject.Validate(SecondStanding),
					"taf-camp-rung3-predecessor-lost: the waterstone this run climbed to is gone, "
						+ "so the second consecutive climb cannot be told from the first");
				Require(!ReferenceEquals(SecondStanding, Standing)
					&& SecondStanding.IDIfAssigned != Standing.IDIfAssigned,
					"taf-camp-rung3-same-body: the moot yard is the very body rung two settled on, "
						+ "so only one climb happened");
				int first = SecondStanding.GetIntProperty(HeartEffectProperty);
				Require(first == 2, "taf-camp-rung3-first-climb-unsettled: the waterstone's own "
					+ "rung marker reads " + first + ", not the settled 2");
				Evidence.Append("\nphase3 rung-marker property=").Append(HeartEffectProperty)
					.Append("; waterstone=").Append(SecondHeartId).Append(" reads ").Append(first)
					.Append("; moot yard=").Append(Standing.IDIfAssigned).Append(" reads ")
					.Append(settled).Append("; distinct bodies=true");
			}

			/// <summary>Issue #162, asked again after the SECOND climb: production's own founding
			/// heart recovery predicate, the one <c>KingdomConstruction.Settlement</c> gates every
			/// pass on, read on the ground the moot yard now stands on. Idempotent by construction
			/// (the pass calls it every tick), so asking it here drives nothing. A false here is the
			/// "founding heart recovery requires inspection" halt seen from inside the game, and it
			/// would mean the chained recovery PR #164 proved for one climb does not hold for two.
			/// </summary>
			private void RequireRecoveredAfterSecondClimb()
			{
				bool recovered = KingdomPlots.RecoverFoundingHeart(System, Zone);
				Evidence.Append("\nphase3 founding heart recovered after second climb=")
					.Append(recovered);
				Require(recovered, "taf-camp-rung3-heart-unrecovered: the settlement cannot recover "
					+ "its founding heart once the moot yard stands, so every later settlement pass "
					+ "on this ground refuses before it begins (issue #162 after a second climb)");
			}

			/// <summary>The authored growth the moot yard declares: 8x6 becomes 12x10
			/// (RuntimeData/KingdomBuildings.xml:644,654). Read off the standing rect rather than
			/// off the catalogue, so an unchanged envelope refuses.</summary>
			private void RequireGrownFootprint(GameObject Standing)
			{
				KingdomPlotRules.PlotRect rect;
				Require(KingdomPlots.TryReadRect(Standing, out rect),
					"taf-camp-rung3-rect-unreadable: the moot yard's rect could not be read");
				int width = rect.X2 - rect.X1 + 1;
				int height = rect.Y2 - rect.Y1 + 1;
				Require(width == 12 && height == 10,
					"taf-camp-rung3-footprint: the moot yard stands on " + width + "x" + height
						+ ", not the authored 12x10");
				Evidence.Append("\nphase3 footprint=").Append(width).Append('x').Append(height)
					.Append("; rect=").Append(rect.X1).Append(',').Append(rect.Y1).Append(' ')
					.Append(rect.X2).Append(',').Append(rect.Y2);
			}

			/// <summary>The rung-3 improvement job itself closed: Complete, with an empty failure
			/// and never InspectionRequired. A job still Working, or one that reached a terminal
			/// phase that is not Complete, refuses here rather than being read as a pass.</summary>
			private void RequireCompletedImprovement()
			{
				List<KingdomConstructionJob> jobs;
				string failure;
				// The whole durable registry, not the active slice: a finished job is terminal and
				// the active reads deliberately exclude it.
				Require(KingdomConstruction.TryRead(out jobs, out failure) && jobs != null,
					"taf-camp-rung3-jobs-unreadable: " + KingdomScenarioRules.Bounded(failure));
				KingdomConstructionJob found = null;
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomConstructionJob job = jobs[i];
					if (job == null || job.Route != KingdomConstructionRoute.Improvement
						|| job.TargetKey != ThirdRungKey) continue;
					Require(found == null,
						"taf-camp-rung3-job-ambiguous: two improvement jobs target the moot yard");
					found = job;
				}
				Require(found != null,
					"taf-camp-rung3-job-absent: no improvement job targets the moot yard");
				Require(found.Phase == KingdomConstructionPhase.Complete,
					"taf-camp-rung3-job-unfinished: the moot yard's job stands at " + found.Phase);
				Require(string.IsNullOrEmpty(found.Failure),
					"taf-camp-rung3-job-failed: " + KingdomScenarioRules.Bounded(found.Failure));
				Evidence.Append("\nphase3 job=").Append(found.Id)
					.Append("; phase=").Append(found.Phase)
					.Append("; physical=").Append(found.PhysicalPhase)
					.Append("; committed water debit=")
					.Append(found.Claims == null ? -1 : found.Claims.WaterSpent)
					.Append("; committed material debit=").Append(KingdomScenarioRules.Bounded(
						found.Claims == null ? "" : found.Claims.MaterialSpent))
					.Append("; failure=").Append(KingdomScenarioRules.Bounded(found.Failure));
			}

			/// <summary>No layout or upgrade quarantine was raised by the second consecutive
			/// climb: the retag census settled. Both fault properties are read off the standing
			/// heart and off the predecessor body this run began with.</summary>
			private void RequireNoQuarantine(GameObject Standing)
			{
				RequireClearOf(Standing, "the standing moot yard");
				if (GameObject.Validate(Heart)) RequireClearOf(Heart, "the predecessor heart");
			}

			private void RequireClearOf(GameObject Item, string Whose)
			{
				string fault = Item.GetStringProperty(KingdomArchitectureStamper.FaultProperty);
				string upgradeFault =
					Item.GetStringProperty(KingdomArchitectureStamper.UpgradeFaultProperty);
				Require(string.IsNullOrEmpty(fault), "taf-camp-rung3-quarantine: " + Whose
					+ " carries a layout fault: " + KingdomScenarioRules.Bounded(fault));
				Require(string.IsNullOrEmpty(upgradeFault), "taf-camp-rung3-upgrade-quarantine: "
					+ Whose + " carries an upgrade fault: "
					+ KingdomScenarioRules.Bounded(upgradeFault));
			}
		}
	}
}
