using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomWitnessWorkCharterRuntime
	{
		private static void Recover(Ground Ground)
		{
			if (!KingdomWitnessWorkLease.TryReadAuthority(Ground.Memory, Ground.RealmId,
				out _, out KingdomCivicArtifactsEnvelope held, out string _)) return;
			for (int i = 0; i < held.WitnessWorks.Rows.Count; i++)
			{
				KingdomWitnessWorkReceipt row = held.WitnessWorks.Rows[i];
				if (row.Source.SettlementId != Ground.City.SettlementId
					|| row.CarrierZoneId != "taf:zone:" + Ground.Zone.ZoneID
					|| row.Phase == KingdomWitnessWorkPhase.Captured) continue;
				RecoverRow(Ground, row);
			}
		}

		private static void RecoverRow(Ground Ground, KingdomWitnessWorkReceipt Row)
		{
			bool found = TryReceiptCarrier(Ground, Row, out GameObject carrier);
			if (Row.Phase == KingdomWitnessWorkPhase.Removed
				|| Row.Phase == KingdomWitnessWorkPhase.Lost)
			{
				if (found || ExactObjectLoaded(Ground, Row))
					KingdomWitnessWorkProjectionRuntime.TryDetach(Ground.RealmId, Row,
					Ground.Zone, Ground.Survey, out string _);
				return;
			}
			if (!found)
			{
				if (KingdomWitnessWorkCommit.TryReconcile(Ground.Memory, Ground.RealmId,
					Row.WorkId, false, false, Ground.Tick, out string _)
					&& ExactObjectLoaded(Ground, Row)
					&& KingdomWitnessWorkLease.TryReadBackRow(Ground.Memory, Ground.RealmId,
						Row.WorkId, out KingdomWitnessWorkReceipt lost, out string _))
					KingdomWitnessWorkProjectionRuntime.TryDetach(Ground.RealmId, lost,
						Ground.Zone, Ground.Survey, out string _);
				return;
			}
			if (Row.Phase == KingdomWitnessWorkPhase.CarrierPrepared
				&& carrier.GetPart<r_KingdomWitnessWorkProjection>() == null)
				KingdomWitnessWorkProjectionRuntime.TryAttachPrepared(Ground.RealmId, Row,
					carrier, Ground.Survey, out _, out string _);
			if (!KingdomWitnessWorkProjectionRuntime.TryObserve(Ground.RealmId, Row,
				Ground.Zone, Ground.Survey, out KingdomWitnessCarrierObservation observation,
				out _, out string _))
			{
				if (observation == KingdomWitnessCarrierObservation.Diverged
					&& KingdomWitnessWorkCommit.TryReconcile(Ground.Memory, Ground.RealmId,
						Row.WorkId, false, false, Ground.Tick, out string _)
					&& KingdomWitnessWorkLease.TryReadBackRow(Ground.Memory, Ground.RealmId,
						Row.WorkId, out KingdomWitnessWorkReceipt lost, out string _))
					KingdomWitnessWorkProjectionRuntime.TryDetach(Ground.RealmId, lost,
						Ground.Zone, Ground.Survey, out string _);
				return;
			}
			if (observation == KingdomWitnessCarrierObservation.Present
				&& Row.Phase == KingdomWitnessWorkPhase.CarrierPrepared)
				KingdomWitnessWorkCommit.TryCommitCarrier(Ground.Memory, Ground.RealmId,
					Row.WorkId, Row.CarrierReceiptId, Ground.Tick, out string _);
			else if (observation == KingdomWitnessCarrierObservation.Missing)
				KingdomWitnessWorkCommit.TryReconcile(Ground.Memory, Ground.RealmId,
					Row.WorkId, false, false, Ground.Tick, out string _);
		}

		private static bool TryReceiptCarrier(Ground Ground, KingdomWitnessWorkReceipt Row,
			out GameObject Carrier)
		{
			Carrier = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryBuiltWorks(Ground.City,
				out List<KingdomCurrentCityEvidenceRuntime.Work> works, out string _)) return false;
			for (int i = 0; i < works.Count; i++)
			{
				GameObject item = works[i].Object;
				if (works[i].Evidence.ObjectId == Row.CarrierObjectId
					&& works[i].Evidence.WorkReceiptId == Row.CarrierConstructionReceiptId
					&& "taf:zone:" + works[i].Evidence.ZoneId == Row.CarrierZoneId
					&& item.CurrentCell?.X == Row.CarrierX && item.CurrentCell?.Y == Row.CarrierY)
				{ Carrier = item; return true; }
			}
			return false;
		}

		private static bool ExactObjectLoaded(Ground Ground, KingdomWitnessWorkReceipt Row)
		{
			for (int i = 0; i < Ground.Survey.Objects.Count; i++)
			{
				GameObject item = Ground.Survey.Objects[i];
				if (GameObject.Validate(item) && Row.CarrierObjectId
					== "taf:object:" + item.IDIfAssigned) return true;
			}
			return false;
		}

		private static void Retire(Ground Ground, List<KingdomWitnessWorkReceipt> Standing)
		{
			string[] labels = new string[Standing.Count];
			for (int i = 0; i < labels.Length; i++) labels[i] = Standing[i].Description;
			int pick = Popup.PickOption(Title: "Retire one fixed witness work",
				Options: labels, AllowEscape: true);
			if (pick < 0 || pick >= Standing.Count) return;
			KingdomWitnessWorkReceipt row = Standing[pick];
			if (Popup.ShowYesNo("Remove only this exact projected account? Its immutable source "
				+ "row remains as removed history; no surface or foreign part is destroyed.")
				!= DialogResult.Yes) return;
			if (!KingdomWitnessWorkCommit.TryReconcile(Ground.Memory, Ground.RealmId,
				row.WorkId, true, true, Ground.Tick, out string failure)
				|| !KingdomWitnessWorkLease.TryReadBackRow(Ground.Memory, Ground.RealmId,
					row.WorkId, out KingdomWitnessWorkReceipt removed, out failure)
				|| removed.Phase != KingdomWitnessWorkPhase.Removed)
			{
				Popup.Show("Nothing was removed.\n\n" + failure); return;
			}
			KingdomGovernanceScope.Commit("retire fixed witness work");
			if (!KingdomWitnessWorkProjectionRuntime.TryDetach(Ground.RealmId, removed,
				Ground.Zone, Ground.Survey, out failure))
				Popup.Show("Removal authority is durable; exact marker cleanup remains pending.\n\n"
					+ failure);
			else Popup.Show("The exact witness projection was retired. The surface remains.");
		}
	}
}
