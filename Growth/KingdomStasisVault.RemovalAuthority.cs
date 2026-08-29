using System.Collections.Generic;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal sealed class KingdomStasisVaultRemovalPlan
	{
		internal r_KingdomStasisVault Vault;
		internal List<KingdomStasisCustodyReceipt> Receipts =
			new List<KingdomStasisCustodyReceipt>();
	}

	internal static partial class KingdomStasisVault
	{
		internal static bool TryPrepareRealmRemoval(KingdomSystem System,
			r_KingdomStasisVault Vault, out KingdomStasisVaultRemovalPlan Plan,
			out string Failure)
		{
			Plan = null; Failure = null;
			GameObject root = Vault?.ParentObject;
			Zone zone = root?.CurrentZone;
			if (System == null || Vault == null || zone == null || Vault.Slots == null
				|| !System.ClaimedZones.Contains(zone.ZoneID))
				return Refuse("stasis vault lacks exact active realm ground", out Failure);
			KingdomStasisVaultRemovalPlan plan = new KingdomStasisVaultRemovalPlan
			{
				Vault = Vault
			};
			bool[] slots = new bool[KingdomStasisVaultRules.MaxSlots];
			for (int i = 0; i < Vault.Slots.Count; i++)
			{
				KingdomStasisCustodyReceipt receipt = Vault.Slots[i]?.Copy();
				if (receipt == null || !KingdomStasisVaultRules.Validate(receipt,
					out Failure) || receipt.Slot < 0 || receipt.Slot >= slots.Length
					|| slots[receipt.Slot])
					return Refuse(Failure ?? "stasis custody is malformed or duplicated",
						out Failure);
				slots[receipt.Slot] = true;
				if (receipt.Phase == KingdomStasisCustodyPhase.Released) continue;
				if (receipt.RealmId != System.RealmId || receipt.ZoneId != zone.ZoneID
					|| receipt.VaultObjectId != root.IDIfAssigned)
					return Refuse("stasis custody belongs to another realm, zone, or vault",
						out Failure);
				Resolve(receipt, out GameObject body, out GameObject cradle,
					out GameObject anchor);
				if (!GameObject.Validate(body) || body.CurrentZone != zone
					|| !GameObject.Validate(cradle) || cradle.CurrentZone != zone)
					return Refuse("the exact held body and cradle must be loaded on this ground",
						out Failure);
				if (GameObject.Validate(anchor) && anchor.CurrentZone != zone)
					return Refuse("the exact stasis field anchor is not on this loaded ground",
						out Failure);
				r_KingdomStasisCustody bodyMarker = body.GetPart<r_KingdomStasisCustody>();
				r_KingdomStasisProjection cradleMarker =
					cradle.GetPart<r_KingdomStasisProjection>();
				if (bodyMarker?.Matches(receipt) != true
					|| cradleMarker?.Matches(receipt) != true
					|| (GameObject.Validate(anchor)
						&& anchor.GetPart<r_KingdomStasisFieldAnchor>()?.Matches(receipt) != true))
					return Refuse("stasis custody projection differs from its exact receipt",
						out Failure);
				if (body.HasEffect<Phased>() && !CanPhaseIn(body))
					return Refuse("solid matter blocks the held body from phasing in safely",
						out Failure);
				plan.Receipts.Add(receipt);
			}
			Plan = plan; return true;
		}

		internal static bool TryReleaseForRealmRemoval(KingdomStasisVaultRemovalPlan Plan,
			out string Failure)
		{
			Failure = null;
			if (Plan == null) return true;
			for (int i = 0; i < Plan.Receipts.Count; i++)
			{
				KingdomStasisCustodyReceipt expected = Plan.Receipts[i];
				KingdomStasisCustodyReceipt live = Slot(Plan.Vault, expected.Slot);
				if (!ExactReceipt(live, expected))
					return Refuse("stasis custody changed after removal preview", out Failure);
				if (!TryRelease(Plan.Vault, expected.Slot,
					"the realm was prepared for assembly removal", out Failure)) return false;
				live = Slot(Plan.Vault, expected.Slot);
				if (live == null || live.Phase != KingdomStasisCustodyPhase.Released)
					return Refuse("stasis custody did not reach its terminal receipt", out Failure);
			}
			return true;
		}

		internal static void CollectRealmRemovalArtifacts(KingdomStasisVaultRemovalPlan Plan,
			HashSet<GameObject> Into)
		{
			if (Plan == null || Into == null) return;
			for (int i = 0; i < Plan.Receipts.Count; i++)
			{
				GameObject anchor = GameObject.FindByID(Plan.Receipts[i].FieldObjectId);
				if (GameObject.Validate(anchor)) Into.Add(anchor);
			}
		}

		internal static string RealmRemovalEvidence(KingdomStasisVaultRemovalPlan Plan)
		{
			List<string> rows = new List<string>();
			for (int i = 0; i < (Plan?.Receipts?.Count ?? 0); i++)
			{
				KingdomStasisCustodyReceipt r = Plan.Receipts[i];
				rows.Add(r.CustodyId + "|" + (int)r.Phase + "|" +
					r.InventoryFingerprint + "|" + r.EquipmentFingerprint + "|" +
					r.EffectFingerprint + "|" + r.ReleasedTick);
			}
			rows.Sort(System.StringComparer.Ordinal);
			return KingdomStasisVaultRules.Fingerprint(rows.ToArray());
		}

		private static bool ExactReceipt(KingdomStasisCustodyReceipt A,
			KingdomStasisCustodyReceipt B)
		{
			return KingdomStasisVaultRules.SameAuthority(A, B) && A.Phase == B.Phase
				&& A.BodyBlueprint == B.BodyBlueprint && A.BodyName == B.BodyName
				&& A.InventoryFingerprint == B.InventoryFingerprint
				&& A.EquipmentFingerprint == B.EquipmentFingerprint
				&& A.EffectFingerprint == B.EffectFingerprint
				&& A.EnteredTick == B.EnteredTick && A.ReleasedTick == B.ReleasedTick
				&& A.Fault == B.Fault;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
