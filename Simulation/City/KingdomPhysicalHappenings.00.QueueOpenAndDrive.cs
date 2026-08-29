using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomPhysicalHappenings
	{
		private static KingdomPhysicalQueueResult Queue(KingdomSystem system,
			KingdomCityBook book, KingdomPhysicalHappeningKind kind, long eventTick,
			int subjectA, int subjectB, int outcome, Zone zone, int[] requiredResidents,
			bool externalSemantic, bool preferConstruction, string chronicleAttended,
			string chronicleUnattended, string ledgerAttended, string ledgerUnattended,
			string messageAttended, string messageUnattended, string effect,
			string displayName, string planQuote, long nowTick, out string[] names,
			string fixedEventId = null)
		{
			names = new string[0];
			if (system == null || !system.Founded || book == null || eventTick <= 0L
				|| nowTick <= 0L || kind == KingdomPhysicalHappeningKind.None)
				return KingdomPhysicalQueueResult.Refused;
			string settlementId = book.SettlementId ?? "";
			string eventId = fixedEventId ?? EventId(settlementId, kind, eventTick,
				subjectA, subjectB, outcome);
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle))
				return KingdomPhysicalQueueResult.Refused;
			if (zone != null) ReconcileZoneProjections(zone, settlementId,
				lifecycle.Active == null ? "" : lifecycle.Active.EventId);
			if (KingdomHappeningLifecycleRules.AlreadyCompleted(lifecycle, kind, subjectA,
				subjectB)) return KingdomPhysicalQueueResult.AlreadyCompleted;
			if (lifecycle.Active != null)
			{
				if (!KingdomHappeningLifecycleRules.Matches(lifecycle.Active, kind, eventTick,
					subjectA, subjectB, outcome)) return KingdomPhysicalQueueResult.Busy;
				KingdomPhysicalQueueResult resumed = DriveCore(system, book, system.SeatName,
					StandsIn(lifecycle.Active.ZoneId), nowTick, 0, out int ignored);
				if (TryRead(book, nowTick, out lifecycle) && lifecycle.Active != null
					&& string.Equals(lifecycle.Active.EventId, eventId, StringComparison.Ordinal))
					names = Names(lifecycle.Active);
				return resumed;
			}
			if (zone == null || !StandsIn(zone.ZoneID) || !OwnedGround(system, zone.ZoneID))
				return externalSemantic ? KingdomPhysicalQueueResult.Unattended
					: OpenReport(system, book, lifecycle, eventId, kind, eventTick, subjectA,
						subjectB, outcome, chronicleAttended, chronicleUnattended,
						ledgerAttended, ledgerUnattended, messageAttended, messageUnattended,
						effect, displayName, planQuote, nowTick);
			GameObject fixture = FindFixture(zone, kind);
			if (!GameObject.Validate(fixture) || fixture.CurrentCell == null)
				return externalSemantic ? KingdomPhysicalQueueResult.Unattended
					: OpenReport(system, book, lifecycle, eventId, kind, eventTick, subjectA,
						subjectB, outcome, chronicleAttended, chronicleUnattended,
						ledgerAttended, ledgerUnattended, messageAttended, messageUnattended,
						effect, displayName, planQuote, nowTick);
			if (!TryParticipants(system, zone, fixture, kind, requiredResidents,
				preferConstruction, out KingdomHappeningParticipant[] participants))
				return externalSemantic ? KingdomPhysicalQueueResult.Unattended
					: OpenReport(system, book, lifecycle, eventId, kind, eventTick, subjectA,
						subjectB, outcome, chronicleAttended, chronicleUnattended,
						ledgerAttended, ledgerUnattended, messageAttended, messageUnattended,
						effect, displayName, planQuote, nowTick);
			if (kind == KingdomPhysicalHappeningKind.Raising && !externalSemantic)
			{
				List<string> present = new List<string>();
				for (int i = 0; i < participants.Length; i++) present.Add(participants[i].Name);
				chronicleAttended = KingdomCeremonyRules.RaisingAttendedChronicle(displayName,
					system.SeatName, present, planQuote);
				messageAttended = KingdomCeremonyRules.RaisingAttendedMessage(displayName,
					present);
			}
			KingdomHappeningProposal proposal = new KingdomHappeningProposal(eventId, kind,
				eventTick, subjectA, subjectB, outcome, settlementId, zone.ZoneID, fixture.ID,
				fixture.Blueprint, fixture.CurrentCell.X, fixture.CurrentCell.Y, true, externalSemantic,
				chronicleAttended, chronicleUnattended, ledgerAttended, ledgerUnattended,
				messageAttended, messageUnattended, effect, displayName, planQuote, participants);
			if (!KingdomHappeningLifecycleRules.TryOpen(lifecycle, proposal, nowTick,
				out KingdomHappeningLifecycleBook opened,
				out KingdomHappeningLifecycleFault fault) || !Write(book, opened))
			{
				KingdomLog.Log("happening physical: open refused (" + fault + ") for " + eventId);
				return KingdomPhysicalQueueResult.Refused;
			}
			names = Names(opened.Active);
			return DriveCore(system, book, system.SeatName, true, nowTick, 0,
				out int ignoredPush);
		}

		private static KingdomPhysicalQueueResult OpenReport(KingdomSystem system,
			KingdomCityBook book, KingdomHappeningLifecycleBook lifecycle, string eventId,
			KingdomPhysicalHappeningKind kind, long eventTick, int subjectA, int subjectB,
			int outcome, string chronicleAttended, string chronicleUnattended,
			string ledgerAttended, string ledgerUnattended, string messageAttended,
			string messageUnattended, string effect, string displayName, string planQuote,
			long nowTick)
		{
			KingdomHappeningProposal report = new KingdomHappeningProposal(eventId, kind,
				eventTick, subjectA, subjectB, outcome, book.SettlementId,
				"", "", "", 0, 0, false, false, chronicleAttended, chronicleUnattended,
				ledgerAttended, ledgerUnattended, messageAttended, messageUnattended, effect,
				displayName, planQuote, null);
			if (!KingdomHappeningLifecycleRules.TryOpen(lifecycle, report, nowTick,
				out KingdomHappeningLifecycleBook opened,
				out KingdomHappeningLifecycleFault fault) || !Write(book, opened))
			{
				KingdomLog.Log("happening report: open refused (" + fault + ") for " + eventId);
				return KingdomPhysicalQueueResult.Refused;
			}
			return DriveCore(system, book, system.SeatName, false, nowTick, 0,
				out int ignoredPush);
		}

		private static KingdomPhysicalQueueResult DriveCore(KingdomSystem system,
			KingdomCityBook book, string label, bool here, long nowTick, int pushBudget,
			out int pushed)
		{
			pushed = 0;
			if (TryReadRaw(book, out KingdomHappeningLifecycleBook standing))
				ReconcileZoneProjections(The.Player?.CurrentZone, book.SettlementId,
					standing.Active == null ? "" : standing.Active.EventId);
			for (int step = 0; step < 8; step++)
			{
				if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle))
					return KingdomPhysicalQueueResult.Refused;
				KingdomHappeningOperation operation = lifecycle.Active;
				if (operation == null) return KingdomPhysicalQueueResult.Unattended;
				bool founderHere = here && StandsIn(operation.ZoneId);
				Evidence evidence = Observe(system, operation);
				KingdomHappeningResumeAction action = KingdomHappeningLifecycleRules.ResumeAction(
					operation, nowTick, founderHere, evidence.FixtureExact,
					evidence.ParticipantsExact, evidence.AllArrived, evidence.UseReceiptExact);
				switch (action)
				{
				case KingdomHappeningResumeAction.PreparePosts:
					if (!Prepare(operation, evidence))
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Restoring, false, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (!SetPhase(book, lifecycle, operation.Phase,
						KingdomHappeningLifecyclePhase.Walking, false, 0L, nowTick))
						return KingdomPhysicalQueueResult.Refused;
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.WaitForArrival:
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.BeginHold:
					if (!StampUse(operation, evidence))
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Restoring, false, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (!SetPhase(book, lifecycle, operation.Phase,
						KingdomHappeningLifecyclePhase.Holding, false,
						nowTick + KingdomHappeningLifecycleRules.HoldTicks, nowTick))
						return KingdomPhysicalQueueResult.Refused;
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.WaitHold:
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.Publish:
					if (operation.Phase != KingdomHappeningLifecyclePhase.Ready)
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Ready, true, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (operation.ExternalSemantic) return KingdomPhysicalQueueResult.AttendedReady;
					pushed += PublishGeneric(system, book, operation, label,
						pushBudget - pushed, nowTick);
					if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null)
						return KingdomPhysicalQueueResult.Refused;
					if (!KingdomHappeningLifecycleRules.SinksSettled(lifecycle.Active))
						return KingdomPhysicalQueueResult.Pending;
					if (!SetPhase(book, lifecycle, KingdomHappeningLifecyclePhase.Ready,
						KingdomHappeningLifecyclePhase.Restoring, true, 0L, nowTick))
						return KingdomPhysicalQueueResult.Refused;
					continue;

				case KingdomHappeningResumeAction.WaitExternal:
					return KingdomPhysicalQueueResult.AttendedReady;

				case KingdomHappeningResumeAction.Restore:
					if (operation.Phase != KingdomHappeningLifecyclePhase.Restoring)
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Restoring, false, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (!Restore(system, book, lifecycle, nowTick))
						return KingdomPhysicalQueueResult.Pending;
					if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null)
						return KingdomPhysicalQueueResult.Refused;
					operation = lifecycle.Active;
					if (!operation.Attended && !operation.ExternalSemantic)
					{
						pushed += PublishGeneric(system, book, operation, label,
							pushBudget - pushed, nowTick);
						if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null)
							return KingdomPhysicalQueueResult.Refused;
						operation = lifecycle.Active;
						if (!KingdomHappeningLifecycleRules.SinksSettled(operation))
							return KingdomPhysicalQueueResult.Pending;
					}
					if (operation.ExternalSemantic && !operation.Attended)
					{
						if (!Clear(book, lifecycle, operation.EventId))
							return KingdomPhysicalQueueResult.Refused;
						return KingdomPhysicalQueueResult.Unattended;
					}
					bool wasAttended = operation.Attended;
					if (!Clear(book, lifecycle, operation.EventId))
						return KingdomPhysicalQueueResult.Refused;
					// Clear is the close proof. O5 sees the frozen operation only afterwards;
					// callback refusal never reopens or blocks the owning construction event.
					if (wasAttended) CaptureClosedWitness(system, operation, nowTick);
					return wasAttended ? KingdomPhysicalQueueResult.AttendedReady
						: KingdomPhysicalQueueResult.Unattended;

				default:
					KingdomLog.Log("happening physical: lifecycle refused for " + operation.EventId);
					return KingdomPhysicalQueueResult.Refused;
				}
			}
			return KingdomPhysicalQueueResult.Pending;
		}
	}
}
