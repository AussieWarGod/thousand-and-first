using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		private void OpenPolityTrafficRecords(KingdomSystem System)
		{
			Zone zone = ParentObject?.CurrentZone;
			if (System == null || !System.Founded || zone == null
				|| !System.OwnedZone(zone.ZoneID))
			{
				Popup.Show("Traffic records can only be read on exact loaded realm ground.");
				return;
			}
			string settlementId = System.SettlementIdForOwnedZone(zone.ZoneID);
			if (string.IsNullOrEmpty(settlementId)
				|| !System.TryFindSettlement(settlementId, out bool seated,
					out KingdomSettlement settlement))
			{
				Popup.Show("This ground has no exact settlement record. Nothing changed.");
				return;
			}

			List<KingdomPolityDirectRecord> records =
				KingdomPolitySchedulerRuntime.ReadDirectRecordsOnDemand(
					System, settlementId, IncludeAcknowledged: true);
			if (records.Count == 0)
			{
				Popup.Show("The traffic leaves of this Charter are empty.");
				return;
			}
			List<KingdomPolityDirectRecordView> views = BuildTrafficViews(records);
			if (views.Count != records.Count)
			{
				Popup.Show("A traffic record cannot be read safely. Nothing changed.");
				return;
			}
			string name = seated ? System.SeatName : settlement.SettlementName;
			int pick = Popup.PickOption(
				Title: "Traffic records of " + KingdomPresentation.Rich(name),
				Intro: "These dated leaves preserve traffic the city could not receive as a physical audience.",
				Options: TrafficLabels(views), AllowEscape: true);
			if (pick < 0 || pick >= records.Count) return;

			KingdomPolityDirectRecord record = records[pick];
			KingdomPolityDirectRecordView view = views[pick];
			Popup.Show("{{W|" + view.Title + "}}\n\n" + view.Body);
			if (view.WasAcknowledged
				|| Popup.ShowYesNo("Mark this traffic leaf as read?") != DialogResult.Yes) return;
			long tick = The.Game?.TimeTicks ?? -1L;
			if (!KingdomPolitySchedulerRuntime.TryAcknowledgeDirectRecordOnDemand(
				System, settlementId, record.RecordId, tick, out string failure))
			{
				Popup.Show("The leaf remains unread. " + KingdomPresentation.Rich(failure));
				return;
			}
			KingdomGovernanceScope.Commit("polity traffic record acknowledgement");
		}

		private static List<KingdomPolityDirectRecordView> BuildTrafficViews(
			IList<KingdomPolityDirectRecord> Records)
		{
			List<KingdomPolityDirectRecordView> views =
				new List<KingdomPolityDirectRecordView>(Records?.Count ?? 0);
			for (int i = 0; i < (Records?.Count ?? 0); i++)
			{
				long safe = Math.Max(0L, Records[i].CauseTick);
				string dated = Calendar.GetDay(safe) + " of " + Calendar.GetMonth(safe)
					+ ", " + Calendar.GetYear(safe) + " AR";
				if (KingdomPolityDirectRecordPresentationRules.TryBuild(
					Records[i], dated, out KingdomPolityDirectRecordView view)) views.Add(view);
			}
			return views;
		}

		private static string[] TrafficLabels(IList<KingdomPolityDirectRecordView> Views)
		{
			string[] labels = new string[Views.Count];
			for (int i = 0; i < Views.Count; i++) labels[i] = Views[i].Label;
			return labels;
		}
	}
}
