using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		// ==================================================================================
		// Check-in — reconcile before rendering (§3.1)
		// ==================================================================================

		/// <summary>
		/// The pass's first word with the book: advance the model to now, pay this zone's standing
		/// debt onto its real containers, carry the city's stock to where the founder is standing,
		/// and then let the ground overwrite the row.
		/// <para>
		/// Runs after <c>survey</c> and before <c>trade</c>, so everything downstream reads a
		/// ground the book has already made true. A missed check-out costs freshness, never
		/// correctness (&sect;3.4), because this reconciles against the ground either way.
		/// </para>
		/// </summary>
		public static void CheckIn(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (System == null || !System.Founded || Z == null || Survey == null || System.City == null)
			{
				return;
			}
			StampDedicationOrder(System, Survey);
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("check-in", fault);
				return;
			}
			// The span the model is about to be advanced over, read BEFORE it is: the lines run for
			// the same days the works produced over, off the same processed-through tick, so a
			// network can neither be paid a day production was not nor miss one it was.
			long lastThrough = state.ProcessedThroughTick;
			state = Reckon(System, state, TimeTicks);
			state = Networks(System, Z, state, lastThrough, TimeTicks);
			int index;
			if (!IndexOf(state, Z.ZoneID, out index))
			{
				return;
			}
			KingdomReifySpend spend;
			state = Reify(System, Z, Survey, state, index, TimeTicks, true, out spend);
			if (!KingdomDistanceRuntime.Observe(System, Z, Survey, state, out fault))
			{
				// No invented locality. A missing/stale live-ground slice pauses remote carry;
				// the rest of check-in remains authoritative and continues normally.
				Refuse("distance observe", fault);
			}
			// Central logistics owns every physical cross-zone transfer. Recover a source
			// callback before opening new work, settle only exact marked receipts that have
			// reached this ground, then start planned loads whose exact source is here.
			KingdomCentralLogistics.SweepReceiptMarkers(System, Survey);
			KingdomCentralLogistics.RecoverPreparedSources(System, Z, Survey);
			KingdomCentralLogistics.SettleScalarArrivals(System, Z, Survey, TimeTicks,
				CropOf(System));
			state = Carry(System, Z, Survey, state, TimeTicks);
			KingdomCentralLogistics.StartPlanned(System, Z, Survey, TimeTicks);
			// W7 repair: the audit that CAN be false. The one at the foot of this method reports an
			// identity the reconcile it follows has just constructed by re-deriving the debt -- it
			// proves the reconcile ran and is evidence about nothing else. This one asks the same
			// question BEFORE the ground is imposed, against the model as the reckoning left it, so
			// a cistern the founder emptied by hand or a container something else drank shows up as
			// a number instead of as silence. §3.1 step 4: attributed and told, never silently
			// repaired -- and a line that cannot disagree is not a telling.
			string drift = AuditLine(state, Z, Survey);
			if (drift != null)
			{
				KingdomLog.Log("city: check-in read " + drift);
			}
			state = Reconcile(System, Z, Survey, state, index, TimeTicks);
			// Home and post ids come from the live objects' own stable ids, not from last pass's work
			// rows. Read residents first: their JobWorkId is the sole authority from which ReadWorks
			// may derive CrewAssigned on this same check-in.
			// This is where the roster becomes rows and where the binding registry learns who is
			// standing in this ground (LIVING-CITY-ARCHITECTURE §8.3, §3.8): every settler here gets
			// a stable id and a row, and every row bound HERE whose body is not here reads back as
			// Abroad or Dead, with the cause.
			state = KingdomResidents.ReadRoster(System, Z, Survey, state, TimeTicks);
			state = ReadWorks(state, Z, Survey);
			Publish(System, state);
			if (!KingdomPolityResidentRuntime.TryReconcile(System, TimeTicks,
				out string polityFigureFailure))
				KingdomLog.Log("polity: resident figure reconciliation refused (" +
					polityFigureFailure + ")");
			if (!KingdomPolityVisitRuntime.TryReconcile(System, TimeTicks,
				out string polityVisitFailure))
				KingdomLog.Log("polity: first-contact reconciliation refused (" +
					polityVisitFailure + ")");
			// API-v3 is a real model lane, not a registration-only surface. It advances beside the
			// closed city state and keeps its one bounded wire on this exact city book.
			KingdomBehaviourRuntime.Reckon(System, System.City, System.SeatName);
			// The hour's placement, and the carriers who are mid-journey through this ground. Both
			// are renderings of what the book already says (§3.2(b), §3.7): the station gives
			// vanilla's own idle hook something to claim a settler with, and Render puts every open
			// job's carrier at At(job, now) - the same answer every other zone would give.
			KingdomStations.Attend(System, Z, Survey);
			KingdomBehaviourRuntime.Materialise(System, System.City, Z, TimeTicks);
			// Before anything is minted: a body carrying a job id the model already closed is the
			// one instant the goods could exist twice (§3.8 t3). ZoneThawedEvent is the hook the
			// architecture names, and it is not enough on its own — a suspended-but-resident zone is
			// entered with no thaw at all (§3.5), so the sweep runs on the entry path too. It is
			// idempotent: absence from the registry is what makes a body stale, and a swept zone
			// has none left.
			KingdomPorters.Sweep(System, Z, Survey);
			KingdomPorters.Render(System, Z, TimeTicks);
			Audit(System, Z, Survey, "check-in");
		}

	}
}
