using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ThousandAndFirst
{
	internal enum KingdomSubsidenceEffectPhase : byte { Prepared, Intent, Proved }
	internal enum KingdomSubsidenceEffectAction : byte { Refuse, Apply, Confirm }

	/// <summary>One step's single frozen breakpoint, not one breakpoint per partial visit.</summary>
	internal sealed class KingdomSubsidenceRungPlan
	{
		internal readonly string StepId, RealmId, SettlementId, ZoneId;
		internal readonly GrowthStage From, To;
		internal readonly long DueTick, PreparedTick;
		internal readonly int Departed;
		internal readonly ReadOnlyCollection<KingdomSubsidenceRungWork> Works;

		internal KingdomSubsidenceRungPlan(string stepId, string realmId, string settlementId,
			string zoneId, GrowthStage from, GrowthStage to, long dueTick, long preparedTick, int departed,
			IEnumerable<KingdomSubsidenceRungWork> works)
		{
			StepId = stepId; RealmId = realmId; SettlementId = settlementId; ZoneId = zoneId;
			From = from; To = to; DueTick = dueTick; PreparedTick = preparedTick; Departed = departed;
			Works = works == null ? null : new List<KingdomSubsidenceRungWork>(works).AsReadOnly();
		}

		internal KingdomSubsidenceRungPlan Replace(int index, KingdomSubsidenceRungWork work)
		{
			List<KingdomSubsidenceRungWork> rows = new List<KingdomSubsidenceRungWork>(Works);
			rows[index] = work;
			return new KingdomSubsidenceRungPlan(StepId, RealmId, SettlementId, ZoneId,
				From, To, DueTick, PreparedTick, Departed, rows);
		}
	}

	/// <summary>WorkId is the existing model hash; ObjectId and exact witnesses remain required.</summary>
	internal sealed class KingdomSubsidenceRungWork
	{
		internal readonly int WorkId, X, Y, BeforeWear, AfterWear;
		internal readonly string ObjectId, Blueprint, PlotId, DesignStamp, Name;
		internal readonly bool HadWearPart;
		internal readonly KingdomSubsidenceEffectPhase WearPhase;
		internal readonly ReadOnlyCollection<KingdomSubsidenceRungRoof> Roofs;
		internal readonly KingdomSubsidenceReleasePhase ReleasePhase;
		internal readonly KingdomSubsidenceWearReceipt ReleaseBefore, ReleaseAfter;

		internal KingdomSubsidenceRungWork(int workId, string objectId, string blueprint,
			string plotId, string designStamp, string name, int x, int y, bool hadWearPart,
			int beforeWear, int afterWear, KingdomSubsidenceEffectPhase wearPhase,
			IEnumerable<KingdomSubsidenceRungRoof> roofs,
			KingdomSubsidenceReleasePhase releasePhase = KingdomSubsidenceReleasePhase.Pending,
			KingdomSubsidenceWearReceipt releaseBefore = null, KingdomSubsidenceWearReceipt releaseAfter = null)
		{
			WorkId = workId; ObjectId = objectId; Blueprint = blueprint; PlotId = plotId;
			DesignStamp = designStamp; Name = name; X = x; Y = y; HadWearPart = hadWearPart;
			BeforeWear = beforeWear; AfterWear = afterWear; WearPhase = wearPhase;
			Roofs = roofs == null ? null : new List<KingdomSubsidenceRungRoof>(roofs).AsReadOnly();
			ReleasePhase = releasePhase; ReleaseBefore = releaseBefore; ReleaseAfter = releaseAfter;
		}

		internal KingdomSubsidenceRungWork With(KingdomSubsidenceEffectPhase phase,
			IEnumerable<KingdomSubsidenceRungRoof> roofs)
		{
			return new KingdomSubsidenceRungWork(WorkId, ObjectId, Blueprint, PlotId,
				DesignStamp, Name, X, Y, HadWearPart, BeforeWear, AfterWear, phase, roofs,
				ReleasePhase, ReleaseBefore, ReleaseAfter);
		}

		internal KingdomSubsidenceRungWork WithRelease(KingdomSubsidenceReleasePhase phase,
			KingdomSubsidenceWearReceipt before, KingdomSubsidenceWearReceipt after)
		{
			return new KingdomSubsidenceRungWork(WorkId, ObjectId, Blueprint, PlotId,
				DesignStamp, Name, X, Y, HadWearPart, BeforeWear, AfterWear, WearPhase, Roofs,
				phase, before, after);
		}
	}

	/// <summary>Exact raw roof tuple. Construction never normalizes malformed carrier evidence.</summary>
	internal sealed class KingdomSubsidenceRungRoof
	{
		internal readonly int ResidentId;
		internal readonly string BodyObjectId;
		internal readonly bool BeforeStanding;
		internal readonly long BeforeReached, BeforeWarned;
		internal readonly KingdomSubsidenceEffectPhase Phase;

		internal KingdomSubsidenceRungRoof(int residentId, string bodyObjectId,
			bool beforeStanding, long beforeReached, long beforeWarned,
			KingdomSubsidenceEffectPhase phase)
		{
			ResidentId = residentId; BodyObjectId = bodyObjectId; BeforeStanding = beforeStanding;
			BeforeReached = beforeReached; BeforeWarned = beforeWarned; Phase = phase;
		}

		internal KingdomSubsidenceRungRoof With(KingdomSubsidenceEffectPhase phase)
		{
			return new KingdomSubsidenceRungRoof(ResidentId, BodyObjectId,
				BeforeStanding, BeforeReached, BeforeWarned, phase);
		}
	}
}
