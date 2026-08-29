using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomWitnessWorkCharterRuntime
	{
		public static void Open(KingdomSystem System, GameObject Founder)
		{
			if (!TryGround(System, Founder, out Ground ground, out string failure))
			{
				Popup.Show("Witness work requires one exact loaded city.\n\n" + failure); return;
			}
			Recover(ground);
			if (!KingdomWitnessWorkLease.TryReadAuthority(ground.Memory, ground.RealmId,
				out KingdomCivicMemorySectionLease lease,
				out KingdomCivicArtifactsEnvelope held, out failure))
			{
				Popup.Show("The witness register is unavailable. Nothing changed.\n\n" + failure);
				return;
			}
			List<KingdomWitnessWorkReceipt> pending = Rows(held.WitnessWorks,
				ground.City.SettlementId, KingdomWitnessWorkPhase.Captured);
			List<KingdomWitnessWorkReceipt> standing = Rows(held.WitnessWorks,
				ground.City.SettlementId, KingdomWitnessWorkPhase.Projected);
			int pick = Popup.PickOption(Title: "Fixed witness works",
				Intro: Register(held.WitnessWorks, ground.City.SettlementId),
				Options: new string[] { pending.Count == 0 ? "{{K|No closed event awaits a work}}"
					: "Commission one closed account", pending.Count == 0
					? "{{K|No closed account can be declined}}" : "Decline one closed account",
					standing.Count == 0 ? "{{K|No standing work can be retired}}"
					: "Retire one standing work", "{{K|Close the register}}" },
				Hotkeys: new char[] { 'c', 'd', 'r', 'x' },
				AllowEscape: true);
			if (pick == 0 && pending.Count > 0) Commission(ground, lease, held, pending);
			else if (pick == 1 && pending.Count > 0) Decline(ground, lease, pending);
			else if (pick == 2 && standing.Count > 0) Retire(ground, standing);
		}

		private static void Decline(Ground Ground, KingdomCivicMemorySectionLease Lease,
			List<KingdomWitnessWorkReceipt> Pending)
		{
			string[] labels = new string[Pending.Count];
			for (int i = 0; i < labels.Length; i++) labels[i] = Pending[i].Description;
			int pick = Popup.PickOption(Title: "Decline one fixed witness work",
				Options: labels, AllowEscape: true);
			if (pick < 0 || pick >= Pending.Count) return;
			KingdomWitnessWorkReceipt row = Pending[pick];
			if (Popup.ShowYesNo("Close only this offer? Its exact event, date, named maker, "
				+ "and account remain immutable declined history. No carrier, commerce, custody, "
				+ "standing, or replacement is created.\n\n" + row.Description)
				!= DialogResult.Yes) return;
			if (!KingdomWitnessWorkCommit.TryDeclinePlanned(Ground.Memory, Lease,
				Ground.RealmId, row.WorkId, Ground.Tick, out bool recorded, out string failure)
				|| !KingdomWitnessWorkLease.TryReadBackRow(Ground.Memory, Ground.RealmId,
					row.WorkId, out KingdomWitnessWorkReceipt declined, out failure)
				|| declined.Phase != KingdomWitnessWorkPhase.Declined)
			{
				Popup.Show("Nothing changed.\n\n" + failure); return;
			}
			if (recorded) KingdomGovernanceScope.Commit("decline fixed witness work");
			Popup.Show("The offer is quietly closed. Its immutable account remains in the register.");
		}

		private static List<KingdomWitnessWorkReceipt> Rows(KingdomWitnessWorkBook Book,
			string SettlementId, KingdomWitnessWorkPhase Phase)
		{
			List<KingdomWitnessWorkReceipt> rows = new List<KingdomWitnessWorkReceipt>();
			for (int i = 0; i < Book.Rows.Count; i++)
				if (Book.Rows[i].Source.SettlementId == SettlementId
					&& Book.Rows[i].Phase == Phase) rows.Add(Book.Rows[i]);
			return rows;
		}

		private static string Register(KingdomWitnessWorkBook Book, string SettlementId)
		{
			string text = "Exact retained accounts: " + Book.Rows.Count + " of "
				+ KingdomWitnessWorkRules.MaxRows + ".";
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				KingdomWitnessWorkReceipt row = Book.Rows[i];
				if (row.Source.SettlementId != SettlementId) continue;
				text += "\n\n[" + row.Phase + "] " + row.Description + "\n" + row.WorkId;
			}
			return text;
		}
	}
}
