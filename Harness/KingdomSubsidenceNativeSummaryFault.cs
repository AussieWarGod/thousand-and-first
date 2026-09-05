using System;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Disposable native callback fault, armed by the exact appended summary, not event count.</summary>
	[Serializable]
	public sealed class KingdomSubsidenceNativeSummaryFault : IPart
	{
		[NonSerialized] internal KingdomSystem System;
		[NonSerialized] internal string ExpectedOfficial;
		[NonSerialized] internal int ExpectedCount;
		[NonSerialized] internal long ExpectedCheckpoint;
		[NonSerialized] internal int ExpectedDepartures;
		[NonSerialized] internal int Throws;
		[NonSerialized] internal bool CommittedBeforeFault;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetDisplayNameEvent.ID;
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (Throws == 0 && ReferenceEquals(E.Object, ParentObject) && System != null
				&& System.ChronicleEntries.Count == ExpectedCount
				&& string.Equals(System.ChronicleEntries[ExpectedCount - 1], ExpectedOfficial,
					StringComparison.Ordinal))
			{
				Throws++;
				CommittedBeforeFault = System.LastSubsidenceTick == ExpectedCheckpoint
					&& System.Population == 45 && System.Stage == GrowthStage.City
					&& KingdomResidents.OnRollCount(System) == 45
					&& System.Ledger.Departures == ExpectedDepartures
					&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture);
				throw new InvalidOperationException("native subsidence summary interruption");
			}
			return base.HandleEvent(E);
		}
	}
}
