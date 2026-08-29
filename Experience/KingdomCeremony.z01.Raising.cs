using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomCeremony
	{

		// ==================================================================================
		// The raising ceremony
		// ==================================================================================

		/// <summary>
		/// Closes construction: while attended, freezes exact already-bound builders, walks them by
		/// vanilla pathing to the functional first basin, and lets the durable construction outbox
		/// name only those whose Ready receipt proves arrival. While unattended, it touches no body
		/// or fixture and leaves a plainer dated chronicle line and homecoming note instead. Replaces the deed and
		/// chronicle a completion used to write directly, and is called from <b>both</b> paths
		/// that raise a building &mdash; <c>r_KingdomScaffold.Complete</c> for a single-cell
		/// design and <c>KingdomPlots.Finish</c> for a plot one &mdash; because a house is not a
		/// lesser thing to raise than a palisade.
		/// </summary>
		/// <param name="System">The realm. Null or unfounded is a no-op &mdash; nothing here can
		/// fire before a settlement exists to own it.</param>
		/// <param name="Cell">The cell the finished building now stands on, read for its zone.
		/// May be null; the ceremony still records the deed with no crew found.</param>
		/// <param name="DisplayName">The finished building's name.</param>
		/// <param name="CompleteTick">The scaffold's own due tick, read before its destruction.</param>
		/// <param name="PlanQuote">The surveyor's plan text carried onto the scaffold, or null
		/// when this design was never staked as a plan.</param>
		public static void OnBuildingRaised(KingdomSystem System, Cell Cell, string DisplayName, long CompleteTick, string PlanQuote)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(DisplayName))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: raising", delegate
			{
				System.RecordDeed("the " + DisplayName + " raised at " + KingdomPresentation.Rich(System.KingdomDisplayName));
				if (!Enabled)
				{
					KingdomChronicle.Record(System, "the " + DisplayName + " was raised at " + KingdomPresentation.Rich(System.KingdomDisplayName));
					MessageQueue.AddPlayerMessage("{{G|The " + DisplayName + " is complete.}}");
					return;
				}
				Zone zone = (Cell != null) ? Cell.ParentZone : null;
				bool window = KingdomCeremonyRules.IsAttended(CompleteTick, CurrentTicks());
				Zone physicalZone = window ? zone : null;
				string unattended = KingdomCeremonyRules.RaisingUnattendedChronicle(DisplayName,
					KingdomPresentation.Rich(System.SeatName), PlanQuote);
				long safe = CompleteTick > 0L ? CompleteTick : CurrentTicks();
				string dated = "a dated report for the " + Calendar.GetDay(safe) + " of "
					+ Calendar.GetMonth(safe) + ", " + Calendar.GetYear(safe)
					+ " AR said that " + unattended;
				// Legacy completions lack a construction outbox, but not physical truth. The
				// generic lifecycle freezes exact attendees and owns one RecordOnce chronicle.
				KingdomPhysicalQueueResult result = KingdomPhysicalHappenings.QueueGeneric(System,
					System.City, KingdomPhysicalHappeningKind.Raising, safe,
					KingdomCityRules.StableId(DisplayName + ":" + safe), 0, 0, physicalZone, null,
					KingdomCeremonyRules.RaisingAttendedChronicle(DisplayName, KingdomPresentation.Rich(System.SeatName),
						new List<string>(), PlanQuote), dated, "",
					KingdomCeremonyRules.RaisingLedgerNote(DisplayName),
					"{{G|The " + DisplayName + " is complete.}}", "", "", DisplayName,
					CurrentTicks());
				KingdomLog.Log("ceremony: legacy raised " + DisplayName + " physical="
					+ (result == KingdomPhysicalQueueResult.AttendedReady));
			});
		}

		/// <summary>
		/// Publishes frozen raising content before any sink callback, then dispatches each sink from
		/// its own durable disposition. Chronicle and ledger are inspectable/idempotent; deed and
		/// message are at-most-once and become Lost if reload observes an interrupted attempt.
		/// </summary>
		public static bool EnsureBuildingRaised(KingdomSystem System, Cell Cell,
			string DisplayName, long CompleteTick, string PlanQuote,
			ref KingdomConstructionJob Job)
		{
			if (System == null || !System.Founded || Job == null
				|| Job.Phase != KingdomConstructionPhase.Complete
				|| string.IsNullOrEmpty(DisplayName)) return false;
			string eventId = "construction:" + Job.Id + ":raised";
			if (Job.Outbox != null && Job.Outbox.EventId != eventId
				&& KingdomConstructionRules.OutboxSettled(Job.Outbox))
			{
				// A conversion first settles its strike telling, then later its raising telling.
				// Only a fully-settled prior event may yield the bounded active outbox slot.
				if (!KingdomConstruction.UpdateOutbox(ref Job, null)) return false;
			}
			if (Job.Outbox == null)
			{
				bool enabled = Enabled;
				bool attendedWindow = enabled && KingdomCeremonyRules.IsAttended(CompleteTick,
					CurrentTicks());
				bool attended = false;
				List<string> present = new List<string>();
				if (attendedWindow)
				{
					KingdomPhysicalQueueResult physical = KingdomPhysicalHappenings.QueueRaising(
						System, System.City, Job.Id, CompleteTick,
						Cell == null ? null : Cell.ParentZone, DisplayName, PlanQuote,
						CurrentTicks(), out string ignoredEventId, out string[] physicalNames);
					if (physical == KingdomPhysicalQueueResult.Pending) return false;
					if (physical == KingdomPhysicalQueueResult.Refused) return false;
					attended = physical == KingdomPhysicalQueueResult.AttendedReady;
					if (attended) present.AddRange(physicalNames);
				}
				string chronicle;
				string ledger = null;
				string message;
				int mode;
				if (!enabled)
				{
					mode = 1;
					chronicle = "the " + DisplayName + " was raised at "
						+ KingdomPresentation.Rich(System.KingdomDisplayName);
					message = "{{G|The " + DisplayName + " is complete.}}";
				}
				else if (attended)
				{
					// Mode 4 appends physical-attendance authority to legacy modes 1-3. It prevents
					// generic outbox recovery from publishing an attended raising without exact
					// Ready proof in the city sidecar.
					mode = 4;
					chronicle = KingdomCeremonyRules.RaisingAttendedChronicle(DisplayName,
						KingdomPresentation.Rich(System.SeatName), present, PlanQuote);
					message = KingdomCeremonyRules.RaisingAttendedMessage(DisplayName, present);
				}
				else
				{
					mode = 3;
					chronicle = KingdomCeremonyRules.RaisingUnattendedChronicle(DisplayName,
						KingdomPresentation.Rich(System.SeatName), PlanQuote);
					ledger = KingdomCeremonyRules.RaisingLedgerNote(DisplayName);
					message = "{{G|The " + DisplayName + " is complete.}}";
				}
				KingdomConstructionOutbox box = new KingdomConstructionOutbox
				{
					EventId = eventId,
					Mode = mode,
					Chronicle = chronicle,
					ChronicleState = KingdomConstructionSinkDisposition.Pending,
					Ledger = ledger,
					LedgerState = ledger == null
						? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending,
					Message = message,
					MessageState = KingdomConstructionSinkDisposition.Pending,
					Deed = "the " + DisplayName + " raised at " + KingdomPresentation.Rich(System.KingdomDisplayName),
					DeedState = KingdomConstructionSinkDisposition.Pending
				};
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			else if (Job.Outbox.EventId != eventId)
			{
				KingdomConstruction.Quarantine(ref Job,
					"The construction telling carries another event identity.");
				return false;
			}
			if (Job.Outbox.Mode == 4)
			{
				if (!Dispatch(System, ref Job)) return false;
				if (KingdomPhysicalHappenings.TryReadyRaising(System.City, Job.Id,
					CurrentTicks(), out string physicalEventId, out string[] ignoredNames))
					return KingdomPhysicalHappenings.AcknowledgeRaising(System, System.City,
						physicalEventId, CurrentTicks());
				return KingdomPhysicalHappenings.ReconcileSettledRaising(System, System.City,
					Job.Id, CurrentTicks());
			}
			return Dispatch(System, ref Job);
		}

		/// <summary>Resumes a published terminal outbox without recomputing option or content.</summary>
		public static bool DispatchPending(KingdomSystem System, ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || Job.Outbox == null) return false;
			if (Job.Outbox.Mode != 4) return Dispatch(System, ref Job);
			if (!Dispatch(System, ref Job)) return false;
			if (KingdomPhysicalHappenings.TryReadyRaising(System.City, Job.Id,
				CurrentTicks(), out string eventId, out string[] ignoredNames))
				return KingdomPhysicalHappenings.AcknowledgeRaising(System, System.City, eventId,
					CurrentTicks());
			return KingdomPhysicalHappenings.ReconcileSettledRaising(System, System.City,
				Job.Id, CurrentTicks());
		}
	}
}
