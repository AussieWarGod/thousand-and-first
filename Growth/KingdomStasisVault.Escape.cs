using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomStasisVault
	{
		internal static void ReleaseFromBody(r_KingdomStasisCustody Marker,
			string Reason)
		{
			KingdomStasisCustodyReceipt receipt = Marker?.Receipt;
			GameObject body = Marker?.ParentObject;
			if (receipt == null || body == null) return;
			GameObject root = GameObject.FindByID(receipt.VaultObjectId);
			r_KingdomStasisVault vault = root?.GetPart<r_KingdomStasisVault>();
			if (vault != null && Slot(vault, receipt.Slot) != null)
			{
				TryRelease(vault, receipt.Slot, Reason, out _);
				return;
			}
			Resolve(receipt, out GameObject exactBody, out GameObject cradle,
				out GameObject anchor);
			if (exactBody != body) return;
			KingdomStasisCustodyReceipt releasing = AsReleasePrepared(receipt);
			Marker.Stamp(releasing);
			DetachOwned(releasing, body, cradle, anchor, out _);
		}

		internal static void ReleaseAll(r_KingdomStasisVault Vault, string Reason)
		{
			if (Vault == null) return;
			Normalize(Vault);
			for (int slot = 0; slot < KingdomStasisVaultRules.MaxSlots; slot++)
			{
				KingdomStasisCustodyReceipt receipt = Slot(Vault, slot);
				if (receipt != null && receipt.Phase != KingdomStasisCustodyPhase.Released)
					TryRelease(Vault, slot, Reason, out _);
			}
		}
	}
}
