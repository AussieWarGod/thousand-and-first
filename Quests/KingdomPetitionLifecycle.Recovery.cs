using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomPetitionLifecycle
	{
		private static bool ReconcileDisabled(KingdomSystem system, KingdomLifecycleBook book,
			long now)
		{
			KingdomLifecycleOperation op = book.Petition;
			PetitionLifecycle state = KingdomPetitionRules.LifecycleOf(op);
			if (state == PetitionLifecycle.Offered)
				return PublishTransition(system, book, op, KingdomLifecycleAction.PetitionExpire,
					KingdomPetitionRules.OptionClosedClock, op.DepartTick, now, "option-closed");
			if (state != PetitionLifecycle.Accepted
				|| string.Equals(op.Creed, KingdomPetitionRules.PausedClock,
					StringComparison.Ordinal)) return true;
			long remaining = KingdomPetitionRules.PauseRemaining(now, op.DepartTick);
			return remaining > 0L && PublishTransition(system, book, op,
				KingdomLifecycleAction.PetitionAccept, KingdomPetitionRules.PausedClock,
				remaining, now, "paused");
		}

		private static bool ResumeAccepted(KingdomSystem system, KingdomLifecycleBook book,
			long now)
		{
			KingdomLifecycleOperation op = book.Petition;
			if (KingdomPetitionRules.LifecycleOf(op) != PetitionLifecycle.Accepted
				|| !string.Equals(op.Creed, KingdomPetitionRules.PausedClock,
					StringComparison.Ordinal)) return true;
			if (!KingdomPetitionRules.TryResumeDeadline(now, op.DepartTick, out long deadline))
				return false;
			return PublishTransition(system, book, op, KingdomLifecycleAction.PetitionAccept,
				KingdomPetitionRules.ActiveClock, deadline, now, "resumed");
		}

		private static bool ObserveOption(KingdomLifecycleBook book, bool enabled, long now)
		{
			KingdomLifecycleOptionDecision decision = KingdomLifecycleRules.ObserveOption(
				book.PetitionOption, book.PetitionOptionTick, enabled, now, book.Petition != null);
			if (!decision.Valid)
			{
				book.Quarantined = true;
				book.Fault = "petition option evidence moved backwards or was malformed";
				return false;
			}
			book.PetitionOption = decision.State;
			book.PetitionOptionTick = decision.Tick;
			return true;
		}

		private static bool CanStart(KingdomSystem system, KingdomLifecycleBook book, long now)
		{
			if (book.PetitionOption != KingdomLifecycleOptionState.Enabled) return false;
			KingdomLifecycleOperation op = book.Petition;
			if (op != null && (!KingdomPetitionRules.FrozenSnapshotValid(op)
				|| !KingdomPetitionRules.IsTerminal(KingdomPetitionRules.LifecycleOf(op)))) return false;
			long last = 0L;
			if (op != null && !KingdomPetitionRules.TryIssuedTick(op, out last)) return false;
			int percent = KingdomRules.DistrictsPetitionIntervalPercent(
				system.ZoneDistricts == null ? null : system.ZoneDistricts.Values);
			long interval = KingdomPetitionRules.ScaledInterval(
				KingdomRules.PetitionCooldownTicks, percent);
			return KingdomPetitionRules.CanOfferAt(now, last, book.PetitionOptionTick, interval);
		}

		private static bool AdoptLegacy(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomLifecycleBook book, long now)
		{
			if (book.Petition != null) return true;
			PetitionLifecycle state = KingdomPetitionRules.NormalizeLegacy(system.PetitionState,
				system.PetitionKind);
			if (!KingdomPetitionRules.IsActive(state)) return true;
			if (!LegacyShape(system, book)
				|| !TryRequester(system, survey, system.PetitionPetitioner,
					out GameObject body, out string name)
				|| !KingdomPetitionRules.TryDeadline(system.PetitionIssuedTick,
					KingdomRules.PetitionLifetimeTicks, out long deadline))
			{
				book.Quarantined = true;
				book.Fault = "malformed legacy petition evidence was retained without reinterpretation";
				return false;
			}
			KingdomLifecycleAction action = state == PetitionLifecycle.Accepted
				? KingdomLifecycleAction.PetitionAccept : KingdomLifecycleAction.PetitionOffer;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Petition, action, now);
			if (op == null) return false;
			FreezeOffer(op, body, name, book.SettlementId, zone.ZoneID, system.PetitionKind,
				system.PetitionFaction, system.PetitionTarget, system.PetitionEventId,
				system.PetitionIssuedTick, deadline);
			op.Detail = system.PetitionCauseSnapshot;
			op.Outbox = Outbox(system, op, state == PetitionLifecycle.Accepted
				? "legacy-accepted" : "legacy-offered");
			return PublishAndDrive(system, book, op, now);
		}

		private static bool LegacyShape(KingdomSystem system, KingdomLifecycleBook book)
		{
			return system != null && book != null
				&& Enum.IsDefined(typeof(KingdomRules.PetitionKind), system.PetitionKind)
				&& system.PetitionKind != KingdomRules.PetitionKind.None
				&& !string.IsNullOrEmpty(system.PetitionPetitioner)
				&& KingdomPetitionRules.SnapshotTextValid(system.PetitionPetitioner,
					KingdomLifecycleRules.MaxNameChars, false)
				&& !string.IsNullOrEmpty(system.PetitionOriginSettlementId)
				&& string.Equals(system.PetitionOriginSettlementId, book.SettlementId,
					StringComparison.Ordinal)
				&& KingdomPetitionRules.SnapshotTextValid(system.PetitionCauseSnapshot,
					KingdomLifecycleRules.MaxTextChars, false)
				&& KingdomPetitionRules.EventIdValid(system.PetitionEventId)
				&& KingdomPetitionRules.SnapshotTextValid(system.PetitionFaction,
					KingdomLifecycleRules.MaxNameChars, true)
				&& system.PetitionIssuedTick >= 0L
				&& KingdomPetitionRules.TargetValid(system.PetitionKind,
					system.PetitionTarget);
		}

	}
}
