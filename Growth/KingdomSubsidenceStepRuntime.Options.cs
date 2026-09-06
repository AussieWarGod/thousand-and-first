using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private sealed class OptionFrame
		{
			internal XRLGame Game;
			internal KingdomSystem System;
			internal Snapshot Owner;
			internal string Realm, Settlement;
			internal long Token;
		}

		internal static bool TryOption(KingdomSystem system, bool enabled, long now,
			out KingdomElapsedOptionAction action, out string refusal)
		{
			bool complete = TryOptionCore(system, enabled, now, out action, out refusal);
			if (complete) refusal = null;
			else if (string.IsNullOrEmpty(refusal))
				refusal = "The settlement's subsidence option waits for its exact saved account.";
			return complete;
		}

		private static bool TryOptionCore(KingdomSystem system, bool enabled, long now,
			out KingdomElapsedOptionAction action, out string refusal)
		{
			action = KingdomElapsedOptionAction.Invalid;
			refusal = "The settlement's subsidence option waits for its exact saved account.";
			try
			{
				if (!KingdomResidentDeathRuntime.TryRecoverPending(system, out refusal)) return false;
				if (!TryExecutionFrame(system, now, out OptionFrame frame, out refusal)) return false;
				if (!KingdomSubsidenceAnnouncementDriver.Resume(new AnnouncementPort(frame, now), out refusal)) return false;
				KingdomSubsidenceOptionObservation observation;
				if (frame.Owner.Step.OptionModel == KingdomSubsidenceStepRules.NoOption)
				{
					if (!KingdomSubsidenceOptionRuntime.TryObserve(enabled, now,
						out observation, out refusal) || !ExecutionExact(frame, now)
						|| !ReferenceEquals(observation.City, frame.Owner.City)
						|| observation.MasterToken != frame.Token) return false;
					action = observation.Snapshot.Decision.Action;
					if (action != KingdomElapsedOptionAction.AnchorDisabled
						&& action != KingdomElapsedOptionAction.AnchorEnabled) return true;
					if (frame.Owner.Step.Admission != KingdomSubsidenceAdmission.Admitted)
					{
						if (!KingdomSubsidenceStepRules.TryAdmit(frame.Owner.Step, frame.Realm,
							frame.Settlement, out KingdomSubsidenceStepBook admitted)
							|| !SaveExecutingOption(frame, admitted, now, true)) return false;
					}
					if (!KingdomSubsidenceOptionIntentRules.TryPrepare(frame.Owner.Step,
						system.LastSubsidenceTick, observation.Snapshot, out KingdomSubsidenceOptionIntent intent)
						|| !KingdomSubsidenceStepRules.TryFreezeOption(frame.Owner.Step, intent,
							out KingdomSubsidenceStepBook frozen) || !SaveExecutingOption(frame, frozen, now)) return false;
				}
				if (!KingdomSubsidenceOptionRuntime.TryRestore(system, frame.Owner.City,
					frame.Owner.Wire, out observation, out refusal) || !ExecutionExact(frame, now)) return false;
				KingdomElapsedOptionRecord target = observation.Snapshot.Decision.Record;
				action = observation.Snapshot.Decision.Action;
				KingdomSubsidenceStepOperation active = frame.Owner.Step.Active;
				if (active != null)
				{
					if (active.Completed < active.Quota
						&& (!KingdomSubsidenceStepRules.TryCancel(frame.Owner.Step, target.ObservedTick,
							target.MasterResumeToken, out KingdomSubsidenceStepBook cancelled)
							|| !SaveExecutingOption(frame, cancelled, now))) return false;
					refusal = "Subsidence keeps its earned departures and damaged works until their account is settled.";
					return false;
				}
				if (frame.Owner.Step.BatchModel != KingdomSubsidenceBatchRules.None)
				{
					refusal = "Subsidence keeps its earned departures until their saved summary is settled.";
					return false;
				}
				if (!KingdomSubsidenceOptionIntentRules.TryDecode(frame.Owner.Step.OptionModel,
					out KingdomSubsidenceOptionIntent held)
					|| !KingdomSubsidenceOptionIntentRules.TryCheckpoint(held, frame.Owner.Step,
						system.LastSubsidenceTick, out long checkpoint) || !ExecutionExact(frame, now)) return false;
				// Validate foreign receipt bytes before writing the clock; retain intent across either cut.
				if (!KingdomSubsidenceOptionRuntime.TryPublish(observation,
					out KingdomSubsidenceOptionPublication _, out refusal) || !ExecutionExact(frame, now)) return false;
				if (!KingdomSubsidenceOptionIntentRules.TryCheckpoint(held, frame.Owner.Step,
					system.LastSubsidenceTick, out long confirmed) || confirmed != checkpoint) return false;
				system.LastSubsidenceTick = checkpoint;
				if (target.State == KingdomElapsedOptionState.Disabled) system.SubsidenceAnnounced = false;
				if (!ExecutionExact(frame, now) || system.LastSubsidenceTick != checkpoint
					|| !KingdomSubsidenceOptionRuntime.TryConfirm(observation, out KingdomDurableKeyObservation row)
					|| !KingdomSubsidenceStepRules.TryFinishOption(frame.Owner.Step, checkpoint, row,
						out KingdomSubsidenceStepBook finished) || !SaveExecutingOption(frame, finished, now)) return false;
				refusal = null;
				return true;
			}
			catch (Exception)
			{
				refusal = "The settlement's subsidence account could not be proved; its saved evidence is retained.";
				return false;
			}
		}

		private static bool TryOptionFrame(KingdomSystem system, out OptionFrame frame)
		{
			frame = null;
			if (The.Game == null || system == null || !system.Founded
				|| !ReferenceEquals(The.Game.GetSystem<KingdomSystem>(), system)
				|| !TryReadOwned(system, out List<Snapshot> books)) return false;
			Snapshot owner = books.Find(item => ReferenceEquals(item.City, system.City));
			if (owner == null) return false;
			OptionFrame value = new OptionFrame { Game = The.Game, System = system, Owner = owner,
				Realm = system.CurrentRealmId, Settlement = owner.City.SettlementId,
				Token = system.MasterAppliedResumeToken };
			if (!OptionExact(value)) return false;
			frame = value;
			return true;
		}

		private static bool OptionExact(OptionFrame frame)
		{
			return OptionSeatExact(frame) && frame.Owner.City.SubsidenceModel == frame.Owner.Wire
				&& frame.Owner.City.HasValidSubsidenceStorage()
				&& KingdomResidentDeathRuntime.CanProceed(frame.System, out _);
		}

		private static bool OptionSeatExact(OptionFrame frame)
		{
			return frame != null && ReferenceEquals(The.Game, frame.Game)
				&& ReferenceEquals(frame.Game.GetSystem<KingdomSystem>(), frame.System)
				&& frame.System.Founded && ReferenceEquals(frame.System.City, frame.Owner.City)
				&& frame.System.CurrentRealmId == frame.Realm
				&& KingdomChronicle.SettlementId(frame.System) == frame.Settlement
				&& frame.Owner.City.SettlementId == frame.Settlement
				&& frame.System.MasterAppliedResumeToken == frame.Token;
		}

		private static bool SaveOption(OptionFrame frame, KingdomSubsidenceStepBook next, bool admit = false)
		{
			if (!OptionExact(frame) || next == null || next.RealmId != frame.Realm
				|| next.SettlementId != frame.Settlement
				|| !KingdomSubsidenceStepCodec.TryEncode(next, out string wire)) return false;
			if (admit)
			{
				if (frame.Owner.Step.Admission == KingdomSubsidenceAdmission.Admitted
					|| next.Admission != KingdomSubsidenceAdmission.Admitted || next.Sequence != 0
					|| next.Active != null || next.LastRetiredTick != 0
					|| next.BatchModel != KingdomSubsidenceBatchRules.None
					|| next.AnnouncementModel != KingdomSubsidenceAnnouncementCodec.None
					|| next.OptionModel != KingdomSubsidenceStepRules.NoOption) return false;
				frame.Owner.City.SubsidenceModel = wire;
			}
			else if (!Publish(frame.System, frame.Owner, next)) return false;
			frame.Owner = new Snapshot(frame.Owner.City, wire, next);
			return OptionExact(frame);
		}
	}
}
