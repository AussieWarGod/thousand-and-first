using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool AttendSeatedSemantics(Zone Z)
		{
			if (!Founded || Z == null || !ClaimedZones.Contains(Z.ZoneID))
			{
				return false;
			}
			if (!PrepareSemanticPass(Z, The.Game.TimeTicks))
			{
				return false;
			}
			KingdomSurvey survey = null;
			Guard("survey", delegate
			{
				// The district-aware overload: a garrison district trains the whole watch, so the
				// bonus has to be on the shared survey Raids later reads defence from.
				survey = KingdomSurvey.Take(Z, this);
			});
			if (survey == null)
			{
				return false;
			}
			using (KingdomSurvey.PassScope surveyScope = survey.BindPass())
			{
			// The ledger is an unread report, not one pass's scratch buffer. It is cleared only
			// after the founder opens the report in the Charter; stationary daily reconciliation
			// therefore appends instead of erasing yesterday's news.
			// After survey and before trade, and the order is the whole of LIVING-CITY-ARCHITECTURE
			// §3.1: the model is advanced to now, this zone's standing debt is paid onto its real
			// containers in dedication order, the city's own stock is carried to where the founder
			// is standing, and then the ground overwrites the row. Everything below reads a ground
			// the book has already made true.
			if (!TrySemanticStep(SemanticStepCheckIn, "check-in", delegate
			{
				Simulation.City.KingdomCity.CheckIn(this, Z, survey, The.Game.TimeTicks);
				// Addendum 17 reads culture/species from the same real bodies this pass
				// witnessed. Body-side receipts make retries idempotent; a changed live source
				// is offered to research only after the city's ground has checked in.
				if (KingdomResidentIdentity.Reconcile(this, survey.Settlers))
				{
					KingdomResearch.ApplySources(this);
				}
				// What this city has room for, remembered for as long as the founder is away from it.
				LastKnownStorageSpace = survey.StorageSpace;
			}))
			{
				return false;
			}
			// Trade runs BEFORE growth, and the order is load-bearing. Both draw on one shared
			// survey, and growth is where upkeep is taken and the thirst ladder resolves. Water
			// that arrived this pass - a caravan under charter, a manifest sent from the realm's
			// other city - has to be in the stores before anything is drawn from them, or a
			// delivery sent precisely to end a drought would arrive one step too late to stop the
			// emigration it was sent to prevent.
			if (!TrySemanticStep(SemanticStepTrade, "trade", delegate
			{
				KingdomTrade.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepGrowth, "growth", delegate
			{
				KingdomGrowth.OnZoneActivated(this, Z, survey);
			})) return false;
			// Costed construction is an independent semantic lane. Growth's option controls new
			// settler arrivals, not whether an already-paid scaffold, plot, road, conversion, upgrade,
			// or repair may recover. Run its durable receipt resolver after upkeep/work assignment and
			// before any later luxury lane can spend the same remaining stores.
			if (!TrySemanticStep(SemanticStepConstruction, "construction", delegate
			{
				KingdomConstruction.OnSettlementPass(this, Z, survey);
			})) return false;
			// Petitions own their own option and calendar. They are settlement asks, not a side
			// effect of population growth, so disabling Growth cannot silence an accepted promise.
			if (!TrySemanticStep(SemanticStepPetitions, "petitions", delegate
			{
				KingdomPetitions.OnSettlementPass(this, Z, survey);
			})) return false;
			// After growth, and the order is load-bearing for the same reason trade runs before it:
			// growth is where this pass's arrivals, upkeep, and work assignment land, so the free
			// hands and the stores an improvement is allowed to draw on are only true once growth
			// has finished with them. An improvement is a luxury paid out of what is left.
			if (!TrySemanticStep(SemanticStepImprovement, "improvement", delegate
			{
				KingdomUpgrade.OnZoneActivated(this, Z, survey);
			})) return false;
			// After improvement, and the order is load-bearing for the same reason improvement runs
			// after growth: a posted price is paid out of what the stores still hold once the
			// settlement's own upkeep and arrivals are done with them, and a manning notice can only
			// fill an idleness AssignWork has already finished measuring.
			if (!TrySemanticStep(SemanticStepBounties, "bounties", delegate
			{
				KingdomBounty.OnSettlementPass(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepRaids, "raids", delegate
			{
				KingdomRaids.OnZoneActivated(this, Z, survey);
			})) return false;
			// After raids, and the order is load-bearing in both directions. After growth, because
			// hard running is read off the crew stretch KingdomGrowth.AssignWork stamps on
			// KingdomEffectiveness. After bounties and raids, because both move a work this pass
			// and wear must see the result: a work the raiders just broke is counted and queued
			// for mending now rather than a whole pass later. Condition is no longer folded back
			// into KingdomEffectiveness -- each consumer applies KingdomWearRules.WorkEffectiveness
			// itself (Addendum 10(b)), so the ordering no longer decides that arithmetic. Raid damage itself is a separate hook inside KingdomRaids.ExecuteRaid,
			// invoked from the "raids" step above -- it does not run from here. Before reach, so a
			// damaged great work shades its ground by what it is actually managing.
			if (!TrySemanticStep(SemanticStepWear, "wear", delegate
			{
				KingdomWear.OnZoneActivated(this, Z, survey);
			})) return false;
			// The Lab reads staffing after growth and condition after wear. Its persisted job clock
			// receives the pass's stable start tick, so a failed later step and retry cannot mint
			// another slice of staffed work from wall-clock time that elapsed between attempts.
			if (!TrySemanticStep(SemanticStepLab, "lab work", delegate
			{
				KingdomLab.OnSemanticStep(this, Z, survey, SemanticPassStartedTick);
			})) return false;
			if (!TrySemanticStep(SemanticStepOffices, "offices", delegate
			{
				KingdomOffices.OnZoneActivated(this, Z, survey);
			})) return false;
			// A great work is an office SEAT (Addendum 6), so the settlement's own office settles
			// first and the faith pass below can already ask what reaches whom.
			if (!TrySemanticStep(SemanticStepReach, "reach", delegate
			{
				KingdomReach.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepLocus, "locus", delegate
			{
				KingdomLocus.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepGuestbook, "guestbook", delegate
			{
				KingdomGuestbook.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepCreed, "creed", delegate
			{
				KingdomCreed.OnZoneActivated(this, Z);
			})) return false;
			if (!TrySemanticStep(SemanticStepFaith, "faith", delegate
			{
				KingdomFaith.OnZoneActivated(this, Z, survey);
			})) return false;
			// Named salvage is last resolver. Every upkeep/luxury lane has already spent from this
			// pass's stores; happenings therefore see returns and dated failures immediately without
			// letting a recovered dispatch steal goods promised to an earlier lane. A prepared receipt
			// can debit this active ground, so refresh its survey before check-out publishes ground truth.
			if (!TrySemanticStep(SemanticStepExpeditions, "salvage expeditions", delegate
			{
				Simulation.City.KingdomExpeditions.OnSettlementPass(this, Z, survey);
			})) return false;
			// W4. After faith and salvage, and last of the renderers, because a happening is a RENDERING of
			// what the pass has already settled: the creed the city holds with, the works that are
			// still turning, and who is left on the roll. Running it earlier would tell the founder
			// about a city one step out of date.
			if (!TrySemanticStep(SemanticStepHappenings, "happenings", delegate
			{
				Simulation.City.KingdomHappenings.OnZoneActivated(this, Z);
			})) return false;
			// The cheaper last read, and the one that usually beats SuspendingEvent there: what
			// this zone actually holds once the day has been drawn and the works have run. A
			// missed check-out costs freshness, never correctness (§3.4).
			if (!TrySemanticStep(SemanticStepCheckOut, "check-out", delegate
			{
				Simulation.City.KingdomCity.CheckOut(this, Z, survey, The.Game.TimeTicks);
			})) return false;
			if (!TrySemanticStep(SemanticStepDigest, "digest", delegate
			{
				if (Simulation.City.KingdomSemanticDispatcher.IsStationaryDispatch)
				{
					// The founder remained on this ground. Keep the presentation clock current, but
					// do not turn a daily settlement resolve into news of an absence that never happened.
					LastVisitTick = The.Game.TimeTicks;
					if (!Ledger.Any)
					{
						HomecomingDays = 0;
					}
					return;
				}
				long elapsed = The.Game.TimeTicks - LastVisitTick;
				// W4. What the told-log ring holds since the founder last stood here, counted into
				// the ordinary note lane before the report announces itself. Read from the ring
				// and nowhere else, so a happening is remembered once and reported once.
				Simulation.City.KingdomHappenings.Digest(this, City, LastVisitTick);
				LastVisitTick = The.Game.TimeTicks;
				int newlyAccounted = KingdomRules.ElapsedDays(elapsed);
				long totalAccounted = (long)HomecomingDays + newlyAccounted;
				HomecomingDays = (totalAccounted > int.MaxValue) ? int.MaxValue : (int)totalAccounted;
				if (Ledger.Any && elapsed >= KingdomRules.TicksPerDay)
				{
					// Nonmodal on purpose. You come home to a report, not an inspection: the
					// settlement says it has news and waits to be asked, in the Charter.
					XRL.Messages.MessageQueue.AddPlayerMessage("{{C|" + KingdomPresentation.Rich(SeatName) + "}} has news of the "
						+ ((HomecomingDays == 1) ? "day" : HomecomingDays + " days") + " you were away. {{K|(Charter: what happened while you were away)}}");
				}
			})) return false;
			// This is the coherent boundary for a settlement visit: intake, simulation, ground
			// publication, chronicle, and digest have all finished. The profile journal compares the
			// semantic snapshot and writes only when one of those facts actually changed.
			if (!TrySemanticStep(SemanticStepSeal, "seal stage", delegate
			{
				string failure;
				if (!KingdomSeal.TryStageSemanticSnapshot("settlement pass", out failure))
				{
					KingdomLog.Log("seal: settlement pass was not staged ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			})) return false;
			return (SemanticPassCompletedMask & SemanticRequiredMask) == SemanticRequiredMask;
			}
		}

		/// <summary>Starts a new durable pass only after the previous receipt was published. An
		/// unfinished pass is tied to its original ground and resumes there even after more world
		/// time elapsed; every subsystem owns its own absolute catch-up clock.</summary>
		private bool PrepareSemanticPass(Zone Z, long NowTick)
		{
			Simulation.City.KingdomSemanticPassReceiptVerdict verdict =
				Simulation.City.KingdomSemanticClockRules.ReceiptVerdict(
					SemanticPassActive, SemanticPassStartedTick, SemanticPassZoneId,
					SemanticPassCompletedMask, SemanticRequiredMask, LastSemanticTick, Z.ZoneID);
			if (verdict == Simulation.City.KingdomSemanticPassReceiptVerdict.Start)
			{
				SemanticPassActive = true;
				SemanticPassStartedTick = (NowTick > 0L) ? NowTick : 0L;
				SemanticPassZoneId = Z.ZoneID;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
				return true;
			}
			if (verdict == Simulation.City.KingdomSemanticPassReceiptVerdict.RefuseDifferentGround)
			{
				KingdomLog.Log("semantic: unfinished pass remains bound to "
					+ (SemanticPassZoneId ?? "?") + "; refused resume on " + Z.ZoneID);
				return false;
			}
			return true;
		}

		/// <summary>One named subsystem receipt. Started is written before the call and completed
		/// only after it returns. A throw stops the pass without advancing LastSemanticTick; retry
		/// skips every completed predecessor and re-enters only the incomplete step.</summary>
		private bool TrySemanticStep(long Bit, string Step, System.Action Action)
		{
			if ((SemanticPassCompletedMask & Bit) != 0L)
			{
				return true;
			}
			SemanticPassStartedMask |= Bit;
			try
			{
				Action();
				SemanticPassCompletedMask |= Bit;
				return true;
			}
			catch (System.Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: semantic " + Step
					+ " failed; the pass remains recoverable", ex);
				KingdomLog.Log("SEMANTIC caught in " + Step + ": " + ex.Message);
				return false;
			}
		}

		/// <summary>
		/// Runs an action inside the engine's event dispatch without letting it escape.
		/// A failure is logged and the step is skipped; the host game and other systems
		/// are never affected. All engine-invoked entry points must route through this.
		/// </summary>
		/// <param name="Step">Short label identifying the step, used in the error log.</param>
		/// <param name="Action">The work to perform.</param>
		public static void Guard(string Step, System.Action Action)
		{
			try
			{
				Action();
			}
			catch (System.Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: " + Step + " failed and was skipped", ex);
				KingdomLog.Log("GUARD caught in " + Step + ": " + ex.Message);
			}
		}

	}
}
