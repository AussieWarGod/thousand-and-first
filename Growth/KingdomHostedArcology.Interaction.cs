using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public delegate string KingdomHostedKnowledgeView(KingdomSystem System);
	public delegate bool KingdomHostedReadOnlyEligibility(KingdomSystem System,
		Zone HostZone, GameObject HostRoot, out string Refusal);

	public static partial class KingdomHostedArcology
	{
		private sealed class KnowledgeProvider
		{
			public KingdomHostedReadOnlyEligibility Eligibility;
			public KingdomHostedKnowledgeView View;
		}

		private static readonly Dictionary<string, KnowledgeProvider> KnowledgeViews =
			new Dictionary<string, KnowledgeProvider>(StringComparer.Ordinal);

		/// <summary>Registers one visualization-only provider. Eligibility receives only the
		/// exact loaded host ground; neither callback can request a queue through this seam.</summary>
		public static bool RegisterKnowledgeView(string Key,
			KingdomHostedReadOnlyEligibility Eligibility, KingdomHostedKnowledgeView View,
			out string Failure)
		{
			Failure = null;
			if (string.IsNullOrEmpty(Key) || Key.Length > 64 || Eligibility == null || View == null)
				return Fail("The hosted knowledge view is malformed.", out Failure);
			if (KnowledgeViews.ContainsKey(Key))
				return Fail("The hosted knowledge view is already registered.", out Failure);
			KnowledgeViews.Add(Key, new KnowledgeProvider {
				Eligibility = Eligibility, View = View
			});
			return true;
		}

		internal static void Open(r_KingdomArcology Root, GameObject Actor)
		{
			if (Root == null || Actor == null || !Actor.IsPlayer()) return;
			GameObject shell = Root.ParentObject;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system == null || shell?.CurrentCell == null) return;
			if (!string.IsNullOrEmpty(Root.QuarantineReason))
			{
				Popup.Show(Status(Root)); return;
			}
			List<KingdomHostedLotDefinition> lots = KingdomHostedArcologyRules.RegisteredHostedLots();
			List<string> options = new List<string>();
			for (int i = 0; i < lots.Count; i++)
			{
				KingdomHostedLotReceipt receipt; string failure;
				if (!TryReceipt(Root, lots[i].Key, out receipt, out failure))
				{
					Quarantine(Root, failure); Popup.Show(Status(Root)); return;
				}
				string tag = lots[i].ReadOnly ? " {{K|[read]}}" : receipt == null
					? " {{C|[commission]}}" : receipt.Phase == KingdomHostedLotPhase.Active
					? " {{G|[active]}}" : receipt.Phase == KingdomHostedLotPhase.Working
					? " {{Y|[raising]}}" : " {{r|[quarantined]}}";
				options.Add(lots[i].DisplayName + tag);
			}
			int chosen = Popup.PickOption(Title: "The hosted arcology", Intro: Status(Root),
				Options: options, AllowEscape: true);
			if (chosen < 0 || chosen >= lots.Count) return;
			KingdomHostedLotDefinition lot = lots[chosen];
			if (lot.ReadOnly)
			{
				KnowledgeProvider provider;
				if (!KnowledgeViews.TryGetValue(lot.KnowledgeView, out provider))
				{
					Popup.Show("This read-only knowledge view has no registered renderer."); return;
				}
				string refusal;
				Zone provedZone = shell.CurrentZone;
				string provedRealm = system.RealmId;
				string provedRoot = shell.IDIfAssigned;
				if (string.IsNullOrEmpty(provedRoot))
				{
					Popup.Show("The hosted knowledge ground lacks assigned identity."); return;
				}
				try
				{
					if (!provider.Eligibility(system, shell.CurrentZone, shell, out refusal))
					{
						Popup.Show(string.IsNullOrEmpty(refusal)
							? "This read-only knowledge view is not available here." : refusal);
						return;
					}
					if (shell.CurrentZone != provedZone || shell.IDIfAssigned != provedRoot
						|| system.RealmId != provedRealm)
					{
						Popup.Show("The read-only knowledge ground changed during eligibility proof.");
						return;
					}
					string view = provider.View(system);
					if (shell.CurrentZone != provedZone || shell.IDIfAssigned != provedRoot
						|| system.RealmId != provedRealm)
					{
						Popup.Show("The read-only knowledge ground changed while the view was drawn.");
						return;
					}
					Popup.Show(view ?? "No realm knowledge is visible here.");
				}
				catch (Exception)
				{
					Popup.Show("The read-only knowledge provider refused an unsafe or incomplete view.");
				}
				return;
			}
			KingdomHostedLotReceipt standing; string receiptFailure;
			if (!TryReceipt(Root, lot.Key, out standing, out receiptFailure) || standing != null)
			{
				Popup.Show(receiptFailure ?? Status(Root)); return;
			}
			KingdomRules.BuildEntry entry;
			if (!KingdomData.TryGetBuilding(lot.MaterialKey, out entry))
			{
				Popup.Show("The hosted lot has no current catalogue price."); return;
			}
			string materials = KingdomMaterials.CostFor(lot.MaterialKey).Describe();
			string bits = KingdomMaterials.BitCostFor(lot.MaterialKey).Describe();
			string exotics = KingdomMaterials.ExoticCostFor(lot.MaterialKey).Describe();
			List<string> prices = new List<string>();
			if (!string.IsNullOrEmpty(materials)) prices.Add(materials);
			if (!string.IsNullOrEmpty(bits)) prices.Add(bits);
			if (!string.IsNullOrEmpty(exotics)) prices.Add(exotics);
			string price = entry.CostDrams + " drams" + (prices.Count == 0 ? ""
				: " and " + KingdomMaterialRules.JoinPhrases(prices));
			if (Popup.ShowYesNo("Commission " + lot.DisplayName + " inside this exact shell?\n\n"
				+ "It costs {{C|" + price + "}}, needs " + lot.Crew
				+ " additional hands while rising, and occupies no surface plot.") != DialogResult.Yes) return;
			BeginLot(system, shell.CurrentZone, Root, lot);
		}
	}
}
