using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		internal static bool TryBeforePass(KingdomSystem system, Zone zone, KingdomSurvey survey,
			out string refusal)
		{
			if (!KingdomResidentDeathRuntime.TryRecoverPending(system, out refusal)) return false;
			if (!TryExecutionFrame(system, XRL.The.Game?.TimeTicks ?? -1L, out _, out refusal)) return false;
			if (HasPending(system))
			{
				if (!KingdomSubsidence.TryReckon(system, zone, survey, The.Game.TimeTicks, out refusal))
				{
					if (string.IsNullOrEmpty(refusal))
						refusal = "The settlement's pending subsidence account could not be settled; its saved evidence is retained.";
					return false;
				}
			}
			else
			{
				if (!TryOption(system, KingdomSubsidence.Enabled, The.Game.TimeTicks,
					out KingdomElapsedOptionAction action, out refusal)) return false;
				if (action == KingdomElapsedOptionAction.AnchorDisabled
					|| action == KingdomElapsedOptionAction.AnchorEnabled)
				{
					// Anchoring consumes this pass without reporting a blocked settlement.
					refusal = null;
					return false;
				}
			}
			if (!CanStartReckoning(system))
			{
				refusal = "The settlement's subsidence account is unfinished or cannot be proved; its saved evidence is retained.";
				return false;
			}
			refusal = null;
			return true;
		}

		internal static bool CanStartReckoning(KingdomSystem system)
		{
			return TryExecutionFrame(system, XRL.The.Game?.TimeTicks ?? -1L, out OptionFrame frame, out _)
				&& frame.Owner.Step.Active == null
				&& frame.Owner.Step.BatchModel == KingdomSubsidenceBatchRules.None
				&& !KingdomSubsidenceAnnouncementRules.Pending(frame.Owner.Step)
				&& frame.Owner.Step.OptionModel == KingdomSubsidenceStepRules.NoOption;
		}

		internal static bool HasPending(KingdomSystem system)
		{
			return !CanStartReckoning(system);
		}

		internal static string Status(KingdomSystem system)
		{
			if (system == null || !system.Founded) return "";
			try
			{
				if (The.Game == null || system.LastSubsidenceTick < 0 || system.LastSubsidenceTick > The.Game.TimeTicks)
					return "\n{{W|Subsidence paused: its saved checkpoint does not match the world's clock.}}";
				if (!TryReadOwned(system, out List<Snapshot> books))
					return "\n{{W|Subsidence paused: its saved settlement account cannot be proved. Evidence is retained.}}";
				Snapshot owner = books.Find(item => ReferenceEquals(item.City, system.City));
				if (owner == null) return "\n{{W|Subsidence paused: its seated account is missing.}}";
				if (owner.Step.FailureModel != KingdomSubsidenceReportArchive.None)
					return "\n{{W|Subsidence has unconfirmed reports. Read the homecoming report to inspect and acknowledge retained warnings. Physical progress is recorded separately.}}";
				if (KingdomSubsidenceAnnouncementRules.Pending(owner.Step))
					return "\n{{W|Subsidence pending: its saved begin or arrest telling must settle. Read homecoming to acknowledge any interrupted notice attempt.}}";
				KingdomSubsidenceStepOperation active = owner.Step.Active;
				if (active != null)
					return "\n{{W|Subsidence pending: " + active.Completed + " of " + active.Quota
						+ " departures accounted for; its exact residents, works, and telling must settle before another step.}}";
				if (owner.Step.OptionModel != KingdomSubsidenceStepRules.NoOption)
					return "\n{{W|Subsidence paused: a saved option change awaits its original checkpoint. No new slide is charged.}}";
				if (owner.Step.BatchModel != KingdomSubsidenceBatchRules.None)
					return "\n{{W|Subsidence pending: its saved group of departures must finish and its ledger and Chronicle delivery must be proved before another slide.}}";
				if (!KingdomSubsidenceOptionRuntime.TryObserve(KingdomSubsidence.Enabled, The.Game.TimeTicks,
					out KingdomSubsidenceOptionObservation _, out string _))
					return "\n{{W|Subsidence paused: its option receipt is missing, changed, or malformed. Evidence is retained.}}";
				return "";
			}
			catch (Exception)
			{ return "\n{{W|Subsidence paused: its saved evidence could not be read safely.}}"; }
		}
	}
}
