using System;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	[Serializable]
	public sealed class KingdomSubsidenceRungNativeFault : IPart
	{
		[NonSerialized] internal KingdomSystem System;
		[NonSerialized] internal long DueTick;
		[NonSerialized] internal int Throws;
		[NonSerialized] internal bool CommittedBeforeFault;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetDisplayNameEvent.ID;
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (Throws == 0 && ReferenceEquals(E.Object, ParentObject) && System != null
				&& KingdomSubsidenceStepCodec.TryDecode(System.City.SubsidenceModel, out KingdomSubsidenceStepBook book)
				&& book.Active != null && book.Active.Phase == KingdomSubsidenceStepPhase.Settling
				&& book.Active.Completed == 5 && book.Active.DueTick == DueTick
				&& book.Active.RungModel == KingdomSubsidenceStepRules.UnplannedRungs)
			{
				Throws++;
				CommittedBeforeFault = System.Population == 35 && KingdomResidents.OnRollCount(System) == 35
					&& System.Stage == GrowthStage.Town && book.Sequence == 3
					&& System.LastSubsidenceTick == DueTick - KingdomSubsidenceStepRules.StepTicks
					&& book.Active.PendingDepartureId == ""
					&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture);
				throw new InvalidOperationException("native subsidence rung declaration interruption");
			}
			return base.HandleEvent(E);
		}
	}
}
