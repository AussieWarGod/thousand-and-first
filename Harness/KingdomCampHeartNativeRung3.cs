using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

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
				GameObject standing = StandingHeart();
				// Everything is READ before anything is asserted, so a refusal below names the
				// production gate that held (runs 7 and 33 named only the design key).
				RecordRung3Standing(standing);
				KingdomConstructionJob rung3Job = FindRung3Job();
				RecordRung3Blocked(rung3Job);
				RecordJobProgress(rung3Job == null ? "(no moot-yard job)" : rung3Job.Id,
					SecondStanding);
				RequireTownHeld("at the rung-3 boundary");
				Require(KingdomUpgrade.DesignKeyOf(standing) == ThirdRungKey,
					"taf-camp-rung3-unfinished: the heart did not finish its real paid climb to "
						+ "the moot yard; key=" + KingdomUpgrade.DesignKeyOf(standing));
				Require(KingdomUpgrade.IsFunctionallyBuilt(standing),
					"taf-camp-rung3-nonfunctional: the moot yard still has unfinished authority");
				RequireEnvelopeMatches(3, standing);
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
			/// ceremony is in flight, 2 settled. The moot yard must carry exactly 2, and a marker
			/// still at 1 is an interrupted ceremony, honestly lost, not a settle. The first climb's
			/// own marker was read while the waterstone stood (phase 2: zone rung 2 on that body);
			/// it cannot be read again here, because production RETIRES the predecessor when the
			/// successor stands (<c>Growth/KingdomUpgrade.25.HandoverRemoval</c>). That the SECOND
			/// consecutive climb happened on a distinct body is therefore proved by the receipt
			/// chain that retired the waterstone, <see cref="RequireRetiredPredecessorChain"/>,
			/// not by finding it. Native run 50 (13eb76d3) climbed lawfully and was refused by the
			/// old read that wanted the waterstone still standing; that read was the defect.
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
				Require(!string.IsNullOrEmpty(SecondHeartId)
					&& SecondHeartId != Standing.IDIfAssigned,
					"taf-camp-rung3-same-body: the moot yard is the very body rung two settled on, "
						+ "so only one climb happened");
				RequireRetiredPredecessorChain(Standing);
				Evidence.Append("\nphase3 rung-marker property=").Append(HeartEffectProperty)
					.Append("; moot yard=").Append(Standing.IDIfAssigned).Append(" reads ")
					.Append(settled).Append("; waterstone=").Append(SecondHeartId)
					.Append(" marker=unreadable (retired by the climb; read 2 at phase 2)")
					.Append("; distinct bodies=true");
			}

			/// <summary>
			/// THE PREDECESSOR IS RETIRED, NOT STANDING. Production removes the waterstone when the
			/// moot yard stands, so a live waterstone here would be the defect, never the proof.
			/// What proves the second climb is the receipt chain production itself binds ground by
			/// (<c>Growth/KingdomFoundingHeartChainRules.BindsGround</c>, read through
			/// <c>KingdomPlots.TryChainedWorkSuccessor</c> exactly as the spatial seal reads a
			/// climbed root, <c>Core/KingdomInheritanceSpatial.Evidence.cs</c> TryClimbedRoot):
			/// the completed moot-yard job names the waterstone as its subject and the standing
			/// body as its output; the standing body is built (<c>KingdomBuilt</c> 1), carries
			/// that job's construction receipt and the scaffold removal proof naming the
			/// waterstone; production's chain reader returns THIS body for the waterstone's work
			/// row; and only then, consulted LAST, the waterstone reads globally absent. A
			/// waterstone that is merely gone -- no completed job naming it, no removal proof on
			/// the successor -- still refuses: absence can refuse a chain and can never be its
			/// proof. Every read is journaled before the first Require.
			/// </summary>
			private void RequireRetiredPredecessorChain(GameObject Standing)
			{
				KingdomConstructionJob job = FindRung3Job();
				Require(job != null, "taf-camp-rung3-predecessor-unproved: no improvement job "
					+ "targets the moot yard, so nothing names the waterstone as retired");
				bool subjectNamed = job.SubjectId == SecondHeartId;
				bool outputNamed = job.OutputId == Standing.IDIfAssigned;
				bool complete = job.Phase == KingdomConstructionPhase.Complete;
				bool built = Standing.GetIntProperty("KingdomBuilt") == 1;
				bool receipt = KingdomConstruction.HasReceipt(Standing, job);
				bool removal = r_KingdomScaffold.HasRemovalProof(Standing, job.SubjectId);
				GameObject proved;
				bool chained = KingdomPlots.TryChainedWorkSuccessor(Zone,
						Simulation.City.KingdomCityRules.StableId(SecondHeartId), out proved)
					&& ReferenceEquals(proved, Standing);
				GameObject live;
				KingdomPhysicalLookupState lookup = KingdomConstruction.FindGlobalLiveId(SecondHeartId,
					out live);
				bool retired = !GameObject.Validate(SecondStanding)
					&& lookup == KingdomPhysicalLookupState.Absent;
				Evidence.Append("\npredecessor=").Append(retired ? "retired" : "live")
					.Append(" id=").Append(SecondHeartId)
					.Append(" successor=").Append(Standing.IDIfAssigned)
					.Append("; job=").Append(job.Id)
					.Append("; subject-named=").Append(subjectNamed)
					.Append("; output-named=").Append(outputNamed)
					.Append("; phase=").Append(job.Phase)
					.Append("; built=").Append(built)
					.Append("; receipt=").Append(receipt)
					.Append("; removal-proof=").Append(removal)
					.Append("; chained-successor=").Append(chained)
					.Append("; live-lookup=").Append(lookup);
				Require(subjectNamed && outputNamed && complete,
					"taf-camp-rung3-predecessor-unproved: the moot-yard job does not retire the "
						+ "waterstone this run climbed to: subject=" + job.SubjectId + "; output="
						+ job.OutputId + "; phase=" + job.Phase);
				Require(built && receipt && removal,
					"taf-camp-rung3-predecessor-unproved: the moot yard does not carry the chain: "
						+ "built=" + built + "; receipt=" + receipt + "; removal-proof=" + removal);
				Require(chained, "taf-camp-rung3-predecessor-unproved: production's chain reader "
					+ "does not return the moot yard for the waterstone's work row");
				Require(retired, "taf-camp-rung3-predecessor-unretired: the waterstone still reads "
					+ "live after the climb that retired it; lookup=" + lookup);
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
				KingdomConstructionJob found = FindRung3Job();
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
