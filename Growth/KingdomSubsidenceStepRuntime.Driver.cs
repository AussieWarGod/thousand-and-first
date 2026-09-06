using System;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		internal static bool TryDrive(KingdomSystem system, Zone zone, KingdomSurvey survey, long now,
			Func<KingdomCatalogueRules.SupportTally> readSupports, int storage, out string refusal)
		{
			refusal = "Subsidence waits for its exact saved settlement, clock, and resident account.";
			try
			{
				if (!TryDriverFrame(system, zone, survey, now, out DriverFrame frame, out refusal)) return false;
				if (frame.Owner.Owner.Step.Active?.CancelRequested == true
					&& frame.Owner.Owner.Step.OptionModel == KingdomSubsidenceStepRules.NoOption
					&& !ResumeStep(frame, readSupports, out refusal)) return false;
				bool optionReady = TryOption(system, KingdomSubsidence.Enabled, now,
					out KingdomElapsedOptionAction action, out refusal);
				if (!TryDriverFrame(system, zone, survey, now, out frame, out refusal)) return false;
				if (!optionReady && frame.Owner.Owner.Step.OptionModel == KingdomSubsidenceStepRules.NoOption) return false;
				if (frame.Owner.Owner.Step.Admission != KingdomSubsidenceAdmission.Admitted)
				{
					if (!KingdomSubsidenceStepRules.TryAdmit(frame.Owner.Owner.Step, frame.Owner.Realm,
						frame.Owner.Settlement, out KingdomSubsidenceStepBook admitted)
							|| !DriverExact(frame) || !SaveExecutingOption(frame.Owner, admitted, now, true)) return false;
				}
				for (int step = 0; step < KingdomSubsidenceRules.MaxSteps; step++)
				{
					if (!DriverExact(frame)) return false;
					if (frame.Owner.Owner.Step.Active != null && !ResumeStep(frame, readSupports, out refusal)) return false;
					KingdomSubsidenceStepBook book = frame.Owner.Owner.Step;
					if (book.BatchModel == KingdomSubsidenceBatchRules.None)
					{
						if (book.OptionModel != KingdomSubsidenceStepRules.NoOption)
							return TryOption(system, KingdomSubsidence.Enabled, now, out action, out refusal);
						if (!optionReady || action != KingdomElapsedOptionAction.Run) { refusal = null; return true; }
						KingdomCatalogueRules.SupportTally supports = readSupports();
						if (!DriverExact(frame)) return false;
						int elapsed = KingdomRules.ElapsedDays(now - system.LastSubsidenceTick);
						KingdomSubsidenceRules.Trajectory path = KingdomSubsidenceRules.Slide(system.Population,
							system.Stage, storage, supports, elapsed, system.SubsidenceAnnounced, system.Shade);
						if (path.Departed == 0) { refusal = null; return true; }
						string name = KingdomPresentation.Rich(system.KingdomDisplayName);
						if (!DriverExact(frame) || !KingdomSubsidenceBatchRules.TryBegin(book, system.LastSubsidenceTick,
							system.LastSubsidenceTick + path.Steps * KingdomSubsidenceStepRules.StepTicks,
							path.Departed, name, KingdomSubsidenceRules.BindingSupportFor(supports, system.Stage),
							out KingdomSubsidenceBatch created) || !SaveBatch(frame, created)) return false;
					}
					book = frame.Owner.Owner.Step;
					if (!KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)) return false;
					if (!batch.Closing && book.OptionModel != KingdomSubsidenceStepRules.NoOption)
					{
						if (!KingdomSubsidenceBatchRules.TryClose(batch, book, system.LastSubsidenceTick, out batch)
							|| !SaveBatch(frame, batch)) return false;
					}
					if (batch.Closing)
					{
						if (!ResumeBatchReport(frame, batch, out refusal)) return false;
						// Finish the frozen batch first; a later pass may begin newly elapsed work.
						if (frame.Owner.Owner.Step.OptionModel != KingdomSubsidenceStepRules.NoOption)
							return TryOption(system, KingdomSubsidence.Enabled, now, out action, out refusal);
						refusal = null; return true;
					}
					KingdomCatalogueRules.SupportTally live = readSupports();
					if (!DriverExact(frame)) return false;
					int level = KingdomSubsidenceRules.SupportedLevel(live, system.Stage, system.Shade);
					int quota = Math.Min(KingdomSubsidenceRules.SettlersPerStep(system.Stage),
						Math.Min(system.Population - level, batch.Wanted - batch.Departed));
					if (quota <= 0 || system.LastSubsidenceTick >= batch.ThroughTick)
					{
						if (!KingdomSubsidenceBatchRules.TryClose(batch, frame.Owner.Owner.Step,
							system.LastSubsidenceTick, out batch) || !SaveBatch(frame, batch)) return false;
						continue;
					}
					if (!KingdomSubsidenceStepRules.TryBegin(frame.Owner.Owner.Step, system.LastSubsidenceTick,
						now, system.Stage, quota, out KingdomSubsidenceStepBook begun, storage, batch.Binding)
						|| !SaveDriver(frame, begun)) return false;
				}
				refusal = "Subsidence reached its bounded recovery limit; its remaining account is retained.";
				return false;
			}
			catch (Exception)
			{ refusal = "Subsidence stopped while checking its saved evidence; no receipt was discarded."; return false; }
		}
	}
}
