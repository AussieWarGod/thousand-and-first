using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Engine bridge for four exact native stasis projections.</summary>
	internal static partial class KingdomStasisVault
	{
		internal const string BayIndexProperty = "r_TAF_StasisBay";

		internal static void Normalize(r_KingdomStasisVault Vault)
		{
			if (Vault == null) return;
			if (Vault.Slots == null) Vault.Slots = new List<KingdomStasisCustodyReceipt>();
			List<KingdomStasisCustodyReceipt> kept =
				new List<KingdomStasisCustodyReceipt>();
			bool[] used = new bool[KingdomStasisVaultRules.MaxSlots];
			int highest = 0;
			for (int i = 0; i < Vault.Slots.Count; i++)
			{
				KingdomStasisCustodyReceipt receipt = Vault.Slots[i];
				receipt?.Normalize();
				string failure;
				if (receipt == null) continue;
				bool validSlot = receipt.Slot >= 0 && receipt.Slot < used.Length
					&& !used[receipt.Slot];
				bool valid = validSlot && KingdomStasisVaultRules.Validate(receipt,
					out failure);
				if (!valid)
				{
					int recoverySlot = validSlot ? receipt.Slot : FirstUnused(used);
					if (recoverySlot < 0) continue;
					receipt = KingdomStasisVaultRules.QuarantineMalformed(receipt,
						recoverySlot, "Malformed or duplicate stasis custody was preserved without targeting an object.");
					if (receipt == null) continue;
				}
				used[receipt.Slot] = true;
				kept.Add(receipt);
				if (receipt.Generation > highest) highest = receipt.Generation;
			}
			kept.Sort((left, right) => left.Slot.CompareTo(right.Slot));
			Vault.Slots = kept;
			int afterHighest = highest == int.MaxValue ? int.MaxValue : highest + 1;
			Vault.NextGeneration = Math.Max(Math.Max(1, Vault.NextGeneration), afterHighest);
		}

		private static int FirstUnused(bool[] Used)
		{
			for (int i = 0; i < Used.Length; i++) if (!Used[i]) return i;
			return -1;
		}

		internal static KingdomStasisCustodyReceipt Slot(r_KingdomStasisVault Vault,
			int Index)
		{
			if (Vault?.Slots == null) return null;
			for (int i = 0; i < Vault.Slots.Count; i++)
				if (Vault.Slots[i]?.Slot == Index) return Vault.Slots[i];
			return null;
		}

		internal static void Put(r_KingdomStasisVault Vault,
			KingdomStasisCustodyReceipt Receipt)
		{
			if (Vault == null || Receipt == null) return;
			Normalize(Vault);
			for (int i = 0; i < Vault.Slots.Count; i++)
			{
				if (Vault.Slots[i].Slot != Receipt.Slot) continue;
				Vault.Slots[i] = Receipt.Copy();
				return;
			}
			if (Vault.Slots.Count < KingdomStasisVaultRules.MaxSlots)
				Vault.Slots.Add(Receipt.Copy());
			Vault.Slots.Sort((left, right) => left.Slot.CompareTo(right.Slot));
		}

		internal static string Description(r_KingdomStasisVault Vault)
		{
			if (Vault == null) return "";
			Normalize(Vault);
			int active = 0;
			int recovering = 0;
			for (int i = 0; i < Vault.Slots.Count; i++)
			{
				KingdomStasisCustodyPhase phase = Vault.Slots[i].Phase;
				if (phase == KingdomStasisCustodyPhase.Active) active++;
				else if (phase != KingdomStasisCustodyPhase.Released) recovering++;
			}
			return "\n{{rules|Stasis custody: " + active + " of four bays held"
				+ (recovering > 0 ? "; " + recovering + " awaiting recovery" : "")
				+ ". Bodies retain their own gear and effects. This is not a surgery theatre.}}";
		}

		internal static void Open(r_KingdomStasisVault Vault, GameObject Actor)
		{
			if (Vault?.ParentObject == null || Actor == null || !Actor.IsPlayer()) return;
			KingdomSystem.Guard("stasis vault", delegate
			{
				Reconcile(Vault);
				List<string> options = new List<string>();
				List<int> actions = new List<int>();
				if (CurrentDominator(Actor) != null)
				{
					options.Add("seal your dormant body in its cradle");
					actions.Add(-1);
				}
				for (int slot = 0; slot < KingdomStasisVaultRules.MaxSlots; slot++)
				{
					KingdomStasisCustodyReceipt receipt = Slot(Vault, slot);
					if (receipt == null || receipt.Phase == KingdomStasisCustodyPhase.Released)
						continue;
					options.Add("release bay " + (slot + 1) + ": "
						+ (receipt.BodyName.Length == 0 ? "unknown body" : receipt.BodyName)
						+ PhaseSuffix(receipt.Phase));
					actions.Add(slot);
				}
				options.Add("read all four custody bays");
				actions.Add(-2);
				int picked = Popup.PickOption(Title: "The stasis vault",
					Options: options.ToArray(), AllowEscape: true);
				if (picked < 0 || picked >= actions.Count) return;
				if (actions[picked] == -2) { Popup.Show(Status(Vault)); return; }
				string failure;
				bool changed;
				if (actions[picked] == -1)
				{
					if (Popup.ShowYesNo("Seal the exact dormant body already lying on a vault cradle? Its gear stays with it, and you must release it before ending domination.") != DialogResult.Yes) return;
					changed = TryEnter(Vault, Actor, out failure);
				}
				else
				{
					KingdomStasisCustodyReceipt receipt = Slot(Vault, actions[picked]);
					if (Popup.ShowYesNo("Release " + (receipt?.BodyName ?? "this body")
						+ " from stasis? This does not end domination by itself.") != DialogResult.Yes) return;
					changed = TryRelease(Vault, actions[picked], "the founder opened the bay",
						out failure);
				}
				Popup.Show(changed
					? "{{G|The custody change is exact and complete.}}"
						+ (string.IsNullOrEmpty(failure) ? "" : "\n\n{{W|" + failure + "}}")
					: failure);
				if (changed) Actor.UseEnergy(KingdomGovernanceRules.NominalEnergyCost,
					KingdomGovernanceRules.EnergyReason("stasis vault"));
			});
		}

		private static string Status(r_KingdomStasisVault Vault)
		{
			StringBuilder text = new StringBuilder("Four physically separate bays:\n");
			for (int slot = 0; slot < KingdomStasisVaultRules.MaxSlots; slot++)
			{
				KingdomStasisCustodyReceipt receipt = Slot(Vault, slot);
				text.Append("\nBay ").Append(slot + 1).Append(": ");
				if (receipt == null || receipt.Phase == KingdomStasisCustodyPhase.Released)
					text.Append("empty");
				else text.Append(receipt.BodyName).Append(PhaseSuffix(receipt.Phase));
				if (receipt?.Phase == KingdomStasisCustodyPhase.Released
					&& !string.IsNullOrEmpty(receipt.Fault))
					text.Append(" {{W|[last release warning: ").Append(receipt.Fault)
						.Append("]}}");
			}
			text.Append("\n\nStored bodies keep their own inventory, equipment, and effects. The vault never clones, transfers, or operates on them.");
			return text.ToString();
		}

		private static string PhaseSuffix(KingdomStasisCustodyPhase Phase)
		{
			return " {{K|[" + Phase.ToString().ToLowerInvariant() + "]}}";
		}
	}
}
