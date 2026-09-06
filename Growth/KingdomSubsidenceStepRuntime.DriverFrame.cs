using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private sealed class DriverFrame
		{
			internal OptionFrame Owner;
			internal Zone Zone;
			internal KingdomSurvey Survey;
			internal long Now;
		}

		private static bool TryDriverFrame(KingdomSystem system, Zone zone, KingdomSurvey survey,
			long now, out DriverFrame frame, out string refusal)
		{
			frame = null;
			if (!TryExecutionFrame(system, now, out OptionFrame owner, out refusal)) return false;
			DriverFrame value = new DriverFrame { Owner = owner, Zone = zone, Survey = survey, Now = now };
			if (!DriverExact(value))
			{
				refusal = "Subsidence waits for its exact active settlement survey.";
				return false;
			}
			frame = value; return true;
		}

		private static bool DriverExact(DriverFrame frame)
		{
			return frame != null && ExecutionExact(frame.Owner, frame.Now) && frame.Now >= 0
				&& frame.Owner.Game.TimeTicks == frame.Now && frame.Zone != null && frame.Survey != null
				&& ReferenceEquals(frame.Survey.Ground, frame.Zone)
				&& ReferenceEquals(KingdomSurvey.ActiveFor(frame.Zone), frame.Survey)
				&& frame.Owner.System.ClaimedZones.Contains(frame.Zone.ZoneID)
				&& frame.Owner.Owner.City.TryReadExact(out _, out _);
		}

		private static bool SaveDriver(DriverFrame frame, KingdomSubsidenceStepBook next)
		{
			return DriverExact(frame) && SaveExecutingOption(frame.Owner, next, frame.Now) && DriverExact(frame);
		}

		internal static bool TryPassGuard(KingdomSystem system, Zone zone, KingdomSurvey survey,
			out Func<bool> exact, out Func<bool> sameSeat)
		{
			return TryPassGuard(system, zone, survey, out exact, out sameSeat, out _);
		}

		internal static bool TryPassGuard(KingdomSystem system, Zone zone, KingdomSurvey survey,
			out Func<bool> exact, out Func<bool> sameSeat, out string refusal)
		{
			exact = null; sameSeat = null;
			refusal = "Subsidence waits for the actual world clock.";
			if (The.Game == null || !TryDriverFrame(system, zone, survey, The.Game.TimeTicks,
				out DriverFrame frame, out refusal)) return false;
			exact = () => DriverExact(frame);
			sameSeat = () => OptionSeatExact(frame.Owner);
			return true;
		}

		// A child may credit its parent even when it returns false or throws. Re-read the same
		// step's durable accounting, never convert the child's return value into a departure.
		private static bool RefreshDriver(DriverFrame frame)
		{
			OptionFrame held = frame.Owner;
			if (!TryExecutionFrame(held.System, frame.Now, out OptionFrame current, out _)
				|| !ReferenceEquals(current.Game, held.Game) || !ReferenceEquals(current.Owner.City, held.Owner.City)
				|| current.Realm != held.Realm || current.Settlement != held.Settlement || current.Token != held.Token
				|| current.Owner.Step.Sequence != held.Owner.Step.Sequence
				|| current.Owner.Step.Active?.Id != held.Owner.Step.Active?.Id
				|| current.Owner.Step.OptionModel != held.Owner.Step.OptionModel
				|| current.Owner.Step.FailureModel != held.Owner.Step.FailureModel
				|| current.Owner.Step.AnnouncementModel != held.Owner.Step.AnnouncementModel
				|| current.Owner.Step.BatchModel != held.Owner.Step.BatchModel) return false;
			KingdomSubsidenceStepOperation before = held.Owner.Step.Active, after = current.Owner.Step.Active;
			if (before != null && (after.Completed < before.Completed || after.Completed > before.Completed + 1
				|| !after.CreditedDepartureIds.StartsWith(before.CreditedDepartureIds, StringComparison.Ordinal)
				|| after.LastActivityTick < before.LastActivityTick || after.CancelRequested != before.CancelRequested
				|| after.CancelTick != before.CancelTick || after.CancelToken != before.CancelToken
				|| after.RungReportModel != before.RungReportModel && !(after.Completed == before.Completed + 1
					&& after.RungReportModel == (after.ReachedStage == after.FromStage
						? KingdomSubsidenceBatchRules.NoReport : KingdomSubsidenceBatchRules.PendingReport))
				|| before.PendingDepartureId != "" && after.PendingDepartureId != "" && after.PendingDepartureId != before.PendingDepartureId
				|| after.Completed == before.Completed && after.ReachedStage != before.ReachedStage)) return false;
			frame.Owner = current;
			return DriverExact(frame);
		}

		private static bool SaveBatch(DriverFrame frame, KingdomSubsidenceBatch batch)
		{
			return KingdomSubsidenceBatchCodec.TryEncode(batch, out string wire)
				&& SaveDriver(frame, frame.Owner.Owner.Step.WithBatch(wire));
		}
	}
}
