using System;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private static bool ResumeStep(DriverFrame frame, Func<KingdomCatalogueRules.SupportTally> readSupports,
			out string refusal)
		{
			refusal = "The unfinished subsidence step waits for its exact resident departure or remaining eligible residents.";
			KingdomSystem system = frame.Owner.System;
			for (int attempt = 0; attempt <= 5; attempt++)
			{
				if (!DriverExact(frame)) return false;
				KingdomSubsidenceStepOperation op = frame.Owner.Owner.Step.Active;
				if (op == null || op.Phase == KingdomSubsidenceStepPhase.Quarantined) return false;
				if (op.PendingDepartureId != "")
				{
					bool recovered = false;
					try { recovered = KingdomResidentDepartureRuntime.TryRecoverPending(system, frame.Zone, out refusal); }
					catch (Exception) { }
					if (!RefreshDriver(frame) || !recovered || frame.Owner.Owner.Step.Active.PendingDepartureId != "") return false;
					op = frame.Owner.Owner.Step.Active;
				}
				if (op.Phase == KingdomSubsidenceStepPhase.Settling) return SettleStep(frame, out refusal);
				KingdomCatalogueRules.SupportTally supports = readSupports();
				if (!DriverExact(frame)) return false;
				if (KingdomSubsidenceRules.HasArrived(system.Population,
					KingdomSubsidenceRules.SupportedLevel(supports, system.Stage, system.Shade)))
				{
					if (!KingdomSubsidenceStepRules.TryCancel(frame.Owner.Owner.Step, frame.Now, frame.Owner.Token,
						out KingdomSubsidenceStepBook cancelled) || !SaveDriver(frame, cancelled)) return false;
					return SettleStep(frame, out refusal);
				}
				int namedIndex = op.Completed, wanted = op.Quota;
				if (frame.Owner.Owner.Step.BatchModel != KingdomSubsidenceBatchRules.None)
				{
					if (!KingdomSubsidenceBatchCodec.TryDecode(frame.Owner.Owner.Step.BatchModel,
						out KingdomSubsidenceBatch batch)) return false;
					namedIndex += batch.Departed; wanted = batch.Wanted;
				}
				int before = op.Completed;
				try
				{
					KingdomGrowth.EmigrateForSubsidence(system, frame.Zone, frame.Survey, op.Id,
						KingdomSubsidenceRules.DepartureCause(op.BindingSupport),
						KingdomSubsidenceRules.TellsDeparture(namedIndex, wanted));
				}
				catch (Exception) { }
				if (!RefreshDriver(frame)) return false;
				op = frame.Owner.Owner.Step.Active;
				if (op.PendingDepartureId != "") continue;
				if (op.Completed <= before) return false;
			}
			return false;
		}

		private static bool SettleStep(DriverFrame frame, out string refusal)
		{
			refusal = "The settlement's fall waits for its damaged works, roofs, and dated telling.";
			KingdomSystem system = frame.Owner.System;
			KingdomSubsidenceStepOperation op = frame.Owner.Owner.Step.Active;
			if (!DriverExact(frame) || op == null || op.Phase != KingdomSubsidenceStepPhase.Settling
				|| op.PendingDepartureId != "" || system.Stage != op.FromStage && system.Stage != op.ReachedStage) return false;
			system.Stage = op.ReachedStage;
			if (op.RungModel == KingdomSubsidenceStepRules.UnplannedRungs)
			{
				if (!KingdomWear.TryRecoverBeforeSubsidenceRung(system, frame.Zone, frame.Survey,
					frame.Owner.Owner.Step, out refusal) || !DriverExact(frame)
					|| !TryPrepareRung(system, frame.Zone, frame.Survey, frame.Now, out refusal)
					|| !RefreshDriver(frame)) return false;
			}
			if (!TryResumeRung(system, frame.Zone, frame.Survey, out refusal) || !RefreshDriver(frame)
				|| !ResumeRungReport(frame, out refusal)) return false;
			refusal = "Subsidence awaits its exact checkpoint and space for failed reports. Read the homecoming report to acknowledge existing warnings; saved evidence is retained.";
			if (!DriverExact(frame) || !KingdomSubsidenceStepRules.TryCheckpoint(frame.Owner.Owner.Step,
				system.LastSubsidenceTick, out long checkpoint)) return false;
			system.LastSubsidenceTick = checkpoint;
			if (!DriverExact(frame) || system.LastSubsidenceTick != checkpoint
				|| !KingdomSubsidenceStepRules.TryRetire(frame.Owner.Owner.Step, checkpoint,
					out KingdomSubsidenceStepBook retired) || !SaveDriver(frame, retired)) return false;
			refusal = null; return true;
		}
	}
}
