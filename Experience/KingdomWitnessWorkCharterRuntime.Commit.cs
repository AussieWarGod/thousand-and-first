using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomWitnessWorkCharterRuntime
	{
		private static void Commission(Ground Ground, KingdomCivicMemorySectionLease Lease,
			KingdomCivicArtifactsEnvelope Held, List<KingdomWitnessWorkReceipt> Pending)
		{
			string[] labels = new string[Pending.Count];
			for (int i = 0; i < labels.Length; i++) labels[i] = Pending[i].Description;
			int pick = Popup.PickOption(Title: "Choose one closed account", Options: labels,
				AllowEscape: true);
			if (pick < 0 || pick >= Pending.Count) return;
			KingdomWitnessWorkReceipt source = Pending[pick];
			if (!MakerPresent(Ground, source.Source))
			{
				Popup.Show("The exact named maker has departed. Nothing changed."); return;
			}
			if (!TryCarriers(Ground, Held.WitnessWorks, out List<Carrier> carriers,
				out string failure) || carriers.Count == 0)
			{
				Popup.Show("No unlinked, construction-receipted fixed civic surface stands here. "
					+ "Nothing changed.\n\n" + failure); return;
			}
			string[] surfaces = new string[carriers.Count];
			for (int i = 0; i < surfaces.Length; i++) surfaces[i] = carriers[i].Object.ShortDisplayName
				+ " at " + carriers[i].Object.CurrentCell.X + "," + carriers[i].Object.CurrentCell.Y;
			pick = Popup.PickOption(Title: "Choose one fixed surface", Options: surfaces,
				AllowEscape: true);
			if (pick < 0 || pick >= carriers.Count) return;
			Carrier carrier = carriers[pick];
			if (!KingdomWitnessWorkCommit.TryPlan(Held, source.WorkId,
				carrier.Evidence.ObjectId, "taf:zone:" + carrier.Evidence.ZoneId,
				carrier.Evidence.WorkReceiptId, carrier.Object.CurrentCell.X,
				carrier.Object.CurrentCell.Y, Ground.Tick, out KingdomWitnessWorkPlan plan,
				out failure))
			{
				Popup.Show("Nothing changed.\n\n" + failure); return;
			}
			if (Popup.ShowYesNo(plan.Disclosure(source.Source)
				+ "\n\nCommission exactly this work?") != DialogResult.Yes) return;
			Commit(Ground, Lease, source, carrier, plan);
		}

		private static void Commit(Ground Prior, KingdomCivicMemorySectionLease Lease,
			KingdomWitnessWorkReceipt Source, Carrier PriorCarrier, KingdomWitnessWorkPlan Plan)
		{
			if (!TryGround(Prior.System, Prior.Founder, out Ground ground, out string failure)
				|| ground.City.SettlementId != Source.Source.SettlementId
				|| !MakerPresent(ground, Source.Source)
				|| !TryExactCarrier(ground, PriorCarrier.Object, Plan, out GameObject carrier,
					out failure))
			{
				Popup.Show("Nothing changed.\n\n" + (failure ?? "maker or surface changed")); return;
			}
			if (!KingdomWitnessWorkCommit.TryPreparePlanned(ground.Memory, Lease, ground.RealmId,
				Plan, out KingdomWitnessWorkReceipt prepared, out bool recorded, out failure))
			{
				Popup.Show("Nothing changed.\n\n" + failure); return;
			}
			if (recorded) KingdomGovernanceScope.Commit("commission fixed witness work");
			if (!KingdomWitnessWorkLease.TryReadBackRow(ground.Memory, ground.RealmId,
				prepared.WorkId, out prepared, out failure)
				|| prepared.Phase != KingdomWitnessWorkPhase.CarrierPrepared
				|| !KingdomWitnessWorkProjectionRuntime.TryAttachPrepared(ground.RealmId, prepared,
					carrier, ground.Survey, out _, out failure)
				|| !KingdomWitnessWorkProjectionRuntime.TryObserve(ground.RealmId, prepared,
					ground.Zone, ground.Survey, out KingdomWitnessCarrierObservation observation,
					out _, out failure) || observation != KingdomWitnessCarrierObservation.Present
				|| !KingdomWitnessWorkCommit.TryCommitCarrier(ground.Memory, ground.RealmId,
					prepared.WorkId, prepared.CarrierReceiptId, ground.Tick, out failure)
				|| !KingdomWitnessWorkLease.TryReadBackRow(ground.Memory, ground.RealmId,
					prepared.WorkId, out KingdomWitnessWorkReceipt kept, out failure)
				|| kept.Phase != KingdomWitnessWorkPhase.Projected)
			{
				Popup.Show("Prepared authority remains recoverable; nothing was reminted.\n\n"
					+ failure); return;
			}
			Popup.Show("The fixed account now stands on its exact zero-commerce surface.\n\n"
				+ prepared.Description);
		}

		private static bool TryExactCarrier(Ground Ground, GameObject Expected,
			KingdomWitnessWorkPlan Plan, out GameObject Carrier, out string Failure)
		{
			Carrier = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryBuiltWorks(Ground.City,
				out List<KingdomCurrentCityEvidenceRuntime.Work> works, out Failure)) return false;
			for (int i = 0; i < works.Count; i++)
				if (ReferenceEquals(works[i].Object, Expected)
					&& works[i].Evidence.ObjectId == Plan.ObjectId
					&& works[i].Evidence.WorkReceiptId == Plan.ConstructionReceiptId
					&& "taf:zone:" + works[i].Evidence.ZoneId == Plan.ZoneId
					&& Expected.CurrentCell?.X == Plan.X && Expected.CurrentCell?.Y == Plan.Y)
				{ Carrier = Expected; Failure = null; return true; }
			Failure = "the exact construction-owned surface moved, vanished, or changed proof";
			return false;
		}
	}
}
