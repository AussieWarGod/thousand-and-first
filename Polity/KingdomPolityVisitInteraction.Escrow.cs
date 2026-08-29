using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitInteraction
	{
		private static void OfferConsentedEscrow(KingdomSystem System,
			KingdomPolityIncidentRecord Clash, long Tick)
		{
			if (!TryCollectEscrowChoices(System, Clash,
				out List<KingdomPolityGroundEscrowSnapshot> choices, out string failure))
			{
				Popup.Show(failure); return;
			}
			string[] labels = new string[choices.Count + 1];
			for (int i = 0; i < choices.Count; i++) labels[i] =
				choices[i].DisplayName + " {{K|at " + choices[i].X + "," + choices[i].Y + "}}";
			labels[choices.Count] = "Choose another stance";
			int picked = Popup.PickOption(Title: "Choose exact collateral",
				Intro: "Only a one-count object already designated as realm property and lying " +
					"beside you can be leased. Nothing foreign or carried is eligible.",
				Options: labels, AllowEscape: true);
			if (picked < 0 || picked >= choices.Count) return;
			KingdomPolityGroundEscrowSnapshot selected = choices[picked];
			int confirmed = Popup.PickOption(Title: "Consent to reversible escrow",
				Intro: "Lease " + selected.DisplayName + " at " + selected.X + "," +
					selected.Y + " for this exact settlement only? It stays on loaded ground, " +
					"cannot be taken, harmed, copied, or consumed, and is released unchanged when " +
					"the receipt commits. The disclosed route pauses briefly, then reopens. No " +
					"casualty, death, conquest, standing, or unseen loss is authored.",
				Options: new[] { "I understand and consent", "Do not lease it" },
				AllowEscape: true);
			if (confirmed != 0) return;
			string consent = Witnessed("consented-escrow", Clash.IncidentPlanId,
				selected.StakeRef, selected.ObjectId, selected.Digest, N(Tick));
			if (!KingdomPolityConsentedEscrowRuntime.TryBegin(System, Clash,
				selected.Item, Tick, consent, out failure))
			{
				Popup.Show("The collateral was not lost. Exact escrow awaits recovery or " +
					"was cancelled: " + failure); return;
			}
			KingdomPolityIncidentRecord concluded = null;
			for (int i = 0; i < System.PolityLedger.Incidents.Count; i++)
				if (System.PolityLedger.Incidents[i].IncidentPlanId == Clash.IncidentPlanId)
					concluded = System.PolityLedger.Incidents[i];
			ShowAftermath(System, concluded);
		}

		private static bool TryCollectEscrowChoices(KingdomSystem System,
			KingdomPolityIncidentRecord Clash,
			out List<KingdomPolityGroundEscrowSnapshot> Choices, out string Failure)
		{
			Choices = new List<KingdomPolityGroundEscrowSnapshot>(); Failure = null;
			GameObject player = The.Player; Cell origin = player?.CurrentCell;
			if (!GameObject.Validate(player) || !player.IsPlayer() || origin == null)
			{
				Failure = "Stand beside exact designated realm property."; return false;
			}
			List<Cell> cells = new List<Cell> { origin };
			List<Cell> adjacent = origin.GetLocalAdjacentCells();
			for (int i = 0; i < adjacent.Count; i++)
				if (adjacent[i] != null && !cells.Contains(adjacent[i])) cells.Add(adjacent[i]);
			HashSet<GameObject> seen = new HashSet<GameObject>();
			for (int c = 0; c < cells.Count; c++)
			{
				List<GameObject> roots = cells[c].GetObjects();
				for (int i = 0; i < roots.Count; i++)
				{
					GameObject item = roots[i];
					if (!seen.Add(item) || !KingdomPolityConsentedEscrowRuntime.TryCaptureNew(
						System, Clash, item, out KingdomPolityGroundEscrowSnapshot snapshot,
						out string _)) continue;
					if (Choices.Count >= 64)
					{
						Choices.Clear(); Failure = "Too many eligible objects are piled nearby; " +
							"move intended collateral apart."; return false;
					}
					Choices.Add(snapshot);
				}
			}
			Choices.Sort(delegate(KingdomPolityGroundEscrowSnapshot a,
				KingdomPolityGroundEscrowSnapshot b)
			{
				int compare = a.X.CompareTo(b.X); if (compare != 0) return compare;
				compare = a.Y.CompareTo(b.Y); if (compare != 0) return compare;
				compare = string.CompareOrdinal(a.Blueprint, b.Blueprint);
				return compare != 0 ? compare : string.CompareOrdinal(a.ObjectId, b.ObjectId);
			});
			if (Choices.Count > 0) return true;
			Failure = "No eligible collateral is beside you. First designate one ordinary, " +
				"one-count ground object as realm property; foreign, important, carried, stacked, " +
				"or construction-leased objects remain inviolate.";
			return false;
		}
	}
}
