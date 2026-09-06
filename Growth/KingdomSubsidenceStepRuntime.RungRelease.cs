using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private static bool ResumeRungRelease(RungFrame frame, int index, ref string refusal)
		{
			RungReleasePort port = new RungReleasePort(frame, index);
			if (KingdomSubsidenceReleaseDriver.Resume(port, index)) return true;
			refusal = port.Refusal;
			return false;
		}

		private sealed class RungReleasePort : IKingdomSubsidenceReleasePort
		{
			internal string Refusal = "The settlement's fall waits for the exact completed work's wear receipt.";
			private readonly RungFrame Frame;
			private readonly int Index;
			private GameObject Work;
			private r_KingdomWear Wear;
			internal RungReleasePort(RungFrame frame, int index) { Frame = frame; Index = index; }
			public KingdomSubsidenceRungPlan Plan => Frame.Plan;
			public bool Current => RungOwnerExact(Frame);

			public bool TryObserve(out KingdomSubsidenceWearReceipt receipt)
			{
				receipt = null;
				if (!Current || Index < 0 || Index >= Plan.Works.Count) return false;
				KingdomSubsidenceRungWork row = Plan.Works[Index];
				if (row.ReleasePhase == KingdomSubsidenceReleasePhase.Released
					|| !Frame.Subjects.TryGetValue(row.ObjectId, out GameObject body)
					|| !RungReleaseSubjectExact(Frame, row, body)) return false;
				r_KingdomWear part = body.GetPart<r_KingdomWear>();
				if (!RungReleaseAttachmentExact(body, part)
					|| Work != null && (!ReferenceEquals(Work, body) || !ReferenceEquals(Wear, part))) return false;
				Work = body; Wear = part;
				if (Wear.LifecycleQuarantined)
				{
					Refusal = "The settlement's fall waits on a quarantined wear receipt; its evidence has been retained.";
					return false;
				}
				if (!ReleaseAvailable() || !Current) return false;
				receipt = new KingdomSubsidenceWearReceipt(Wear.IncidentPhase, Wear.IncidentId,
					Wear.IncidentCause, Wear.IncidentBeforeWear, Wear.IncidentAfterWear, Wear.Wear,
					Wear.LastCause, Wear.LastCompletedIncidentId, Wear.IncidentLine, Wear.IncidentMessageState);
				return Current && RungReleaseAttachmentExact(Work, Wear) && ReleaseAvailable();
			}

			public bool Publish(KingdomSubsidenceRungPlan expected, KingdomSubsidenceRungPlan next)
			{
				if (!Current || !ReferenceEquals(Plan, expected) || !KingdomSubsidenceRungRules.Valid(next)
					|| Index >= next.Works.Count || !TryObserve(out KingdomSubsidenceWearReceipt measured)) return false;
				KingdomSubsidenceRungWork row = next.Works[Index];
				KingdomSubsidenceStepBook changed;
				if (row.ReleasePhase == KingdomSubsidenceReleasePhase.Intent)
				{
					if (!KingdomSubsidenceReleaseRules.Same(measured, row.ReleaseBefore)
						|| !KingdomSubsidenceStepRules.TryArmRungRelease(Frame.Owner.Step, Index, true,
							measured, out changed)) return false;
				}
				else if (row.ReleasePhase != KingdomSubsidenceReleasePhase.Released
					|| !KingdomSubsidenceReleaseRules.Same(measured, row.ReleaseAfter)
					|| !KingdomSubsidenceStepRules.TryProveRungRelease(Frame.Owner.Step, Index, true,
						measured, out changed)) return false;
				return Current && ReferenceEquals(Plan, expected)
					&& KingdomSubsidenceRungCodec.TryEncode(next, out string wire)
					&& changed.Active.RungModel == wire && SaveRung(Frame, changed);
			}

			public bool Write(int field, KingdomSubsidenceWearReceipt expected,
				KingdomSubsidenceWearReceipt target)
			{
				if (!TryObserve(out KingdomSubsidenceWearReceipt measured)
					|| !KingdomSubsidenceReleaseRules.Same(measured, expected)) return false;
				KingdomSubsidenceRungWork row = Plan.Works[Index];
				if (row.ReleasePhase != KingdomSubsidenceReleasePhase.Intent
					|| !KingdomSubsidenceReleaseRules.TryNextWrite(row.ReleaseBefore, row.ReleaseAfter,
						measured, out int admitted) || admitted != field || field == 4
					|| !KingdomSubsidenceReleaseRules.Same(target,
						KingdomSubsidenceReleaseRules.AfterWrite(row.ReleaseBefore, row.ReleaseAfter, field + 1))) return false;
				switch (field)
				{
					case 0: Wear.LastCompletedIncidentId = target.LastCompletedId; break;
					case 1: Wear.IncidentPhase = target.Phase; break;
					case 2: Wear.IncidentId = target.Id; break;
					case 3: Wear.IncidentLine = target.Line; break;
					default: return false;
				}
				return TryObserve(out measured) && KingdomSubsidenceReleaseRules.Same(measured, target);
			}

			private bool ReleaseAvailable()
				=> !Wear.LifecycleQuarantined && Wear.RepairEffortLeft == 0
					&& Wear.LeakPhase == (int)KingdomWearLeakPhase.None
					&& KingdomSubsidenceRungRuntime.ConstructionAvailableForRelease(Work);
		}

		private static bool RungReleaseSubjectExact(RungFrame frame, KingdomSubsidenceRungWork row,
			GameObject work)
		{
			// Physical completion permits movement, not changed designation or duplicate identity.
			if (!RungOwnerExact(frame) || !ReleaseDesignationExact(row, work)) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal) { row.ObjectId };
			if (!KingdomPlots.TryCaptureGlobalLiveIds(ids, out Dictionary<string, GameObject> live)
				|| !live.TryGetValue(row.ObjectId, out GameObject exact)
				|| !ReferenceEquals(exact, work)) return false;
			return RungOwnerExact(frame) && ReleaseDesignationExact(row, work);
		}

		private static bool ReleaseDesignationExact(KingdomSubsidenceRungWork row, GameObject work)
			=> GameObject.Validate(work) && work.IDIfAssigned == row.ObjectId && work.Blueprint == row.Blueprint
				&& work.HasIntProperty("KingdomBuilt") && !work.HasStringProperty("KingdomBuilt")
				&& work.GetIntProperty("KingdomBuilt") == 1
				&& RawRungProperty(work, KingdomPlots.PlotIdProperty, row.PlotId == "" ? null : row.PlotId)
				&& RawRungProperty(work, KingdomUpgrade.BuildKeyProperty, row.DesignStamp);

		private static bool RungReleaseAttachmentExact(GameObject work, r_KingdomWear wear)
		{
			if (wear == null || !GameObject.Validate(work)
				|| !ReferenceEquals(work.GetPart<r_KingdomWear>(), wear)
				|| !ReferenceEquals(wear.ParentObject, work)) return false;
			int copies = 0, count = work.PartsList == null ? 0 : work.PartsList.Count;
			for (int i = 0; i < count; i++)
				if (work.PartsList[i] is r_KingdomWear) copies++;
			return copies == 1;
		}
	}
}
