using System;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Disposable native declaration fault, armed by the frozen report, not event count.</summary>
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
				&& System.ChronicleEntries.Count == ExpectedCount - 1
				&& KingdomSubsidenceStepCodec.TryDecode(System.City.SubsidenceModel, out KingdomSubsidenceStepBook book)
				&& book.Active == null
				&& KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)
				&& batch.Closing && batch.Departed == 5
				&& KingdomSubsidenceReportCodec.TryDecode(batch.ReportModel, out KingdomSubsidenceReportPlan report)
				&& report.Entries.Count == 1 && !report.Entries[0].ChronicleProved
				&& report.Entries[0].LedgerPhase == ReportLedgerPhase.Proved
				&& string.Equals("On the " + Calendar.GetDay(report.Entries[0].AtTick) + " of "
					+ Calendar.GetMonth(report.Entries[0].AtTick) + ", " + Calendar.GetYear(report.Entries[0].AtTick)
					+ " AR, " + report.Entries[0].Text + ".", ExpectedOfficial, StringComparison.Ordinal))
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
