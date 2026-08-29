using System;
using XRL;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomStasisVault
	{
		internal static void Reconcile(r_KingdomStasisVault Vault)
		{
			if (Vault?.ParentObject == null) return;
			Normalize(Vault);
			for (int i = 0; i < KingdomStasisVaultRules.MaxSlots; i++)
			{
				KingdomStasisCustodyReceipt receipt = Slot(Vault, i);
				if (receipt == null || receipt.Phase == KingdomStasisCustodyPhase.Released)
					continue;
				ReconcileSlot(Vault, receipt);
			}
		}

		private static void ReconcileSlot(r_KingdomStasisVault Vault,
			KingdomStasisCustodyReceipt Receipt)
		{
			string failure;
			bool valid = KingdomStasisVaultRules.Validate(Receipt, out failure);
			Resolve(Receipt, out GameObject body, out GameObject cradle,
				out GameObject anchor);
			bool vaultExact = Vault?.ParentObject?.IDIfAssigned == Receipt.VaultObjectId
				&& LotOf(Vault.ParentObject) == Receipt.LotId;
			bool cradleExact = cradle != null && cradle.CurrentZone?.ZoneID == Receipt.ZoneId
				&& cradle.GetStringProperty(KingdomPlots.PlotIdProperty) == Receipt.LotId;
			bool bodyExact = body != null && body.CurrentCell == cradle?.CurrentCell
				&& body.Blueprint == Receipt.BodyBlueprint;
			bool marker = body?.GetPart<r_KingdomStasisCustody>()?.Matches(Receipt) == true;
			bool projection = cradle?.GetPart<r_KingdomStasisProjection>()?.Matches(Receipt)
				== true;
			bool field = anchor?.GetPart<r_KingdomStasisFieldAnchor>()?.Matches(Receipt)
				== true && anchor.GetPart<Stasisfield>() != null
				&& anchor.CurrentCell == cradle?.CurrentCell;
			bool domination = ExactDomination(Receipt, body);
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			bool owned = system != null && system.Founded && system.CurrentRealmId == Receipt.RealmId
				&& system.OwnedZone(Receipt.ZoneId);

			if (Receipt.Phase == KingdomStasisCustodyPhase.Prepared && valid && owned
				&& vaultExact && cradleExact && bodyExact && marker && projection
				&& anchor == null && domination && body.GetPhase() == 1)
			{
				if (!ProjectNativeField(Vault, Receipt, body, cradle, out failure))
					QuarantineAfterCleanup(Vault, Receipt, body, cradle, anchor, failure);
				return;
			}
			if (Receipt.Phase == KingdomStasisCustodyPhase.FieldProjected && valid
				&& owned && vaultExact && cradleExact && bodyExact && marker && projection
				&& field && domination && body.GetPhase() == 2)
			{
				anchor.GetPart<Stasisfield>().ProcessStasis();
				if (body.IsInStasis())
				{
					KingdomStasisCustodyReceipt active =
						KingdomStasisVaultRules.Activated(Receipt);
					if (active != null) StampAll(Vault, active, body, cradle, anchor);
					return;
				}
			}
			KingdomStasisRecoveryVerdict verdict = Receipt.Phase
				== KingdomStasisCustodyPhase.ReleasePrepared
				|| Receipt.Phase == KingdomStasisCustodyPhase.Quarantined
				? KingdomStasisRecoveryVerdict.Release
				: KingdomStasisVaultRules.JudgeRecovery(valid, owned, vaultExact,
					cradleExact, bodyExact, domination, marker, projection && field,
					body != null && body.IsInStasis(), body != null && body.GetPhase() == 2);
			if (verdict == KingdomStasisRecoveryVerdict.KeepActive)
			{
				if (BodyManifestChanged(Receipt, body))
					QuarantineAfterCleanup(Vault, Receipt, body, cradle, anchor,
						"Whole-body custody evidence changed while stilled.");
				return;
			}
			if (verdict == KingdomStasisRecoveryVerdict.ContinueForward && field)
			{
				anchor.GetPart<Stasisfield>().ProcessStasis();
				return;
			}
			if (verdict == KingdomStasisRecoveryVerdict.Release)
			{
				TryRelease(Vault, Receipt.Slot, "custody prerequisite ended", out failure);
				return;
			}
			QuarantineAfterCleanup(Vault, Receipt, body, cradle, anchor,
				failure ?? "Exact stasis evidence diverged.");
		}

		private static void QuarantineAfterCleanup(r_KingdomStasisVault Vault,
			KingdomStasisCustodyReceipt Receipt, GameObject Body, GameObject Cradle,
			GameObject Anchor, string Reason)
		{
			DetachOwned(Receipt, Body, Cradle, Anchor, out string cleanup);
			Put(Vault, KingdomStasisVaultRules.Quarantined(Receipt,
				string.IsNullOrEmpty(cleanup) ? Reason : Reason + " " + cleanup));
		}

		private static bool ExactDomination(KingdomStasisCustodyReceipt Receipt,
			GameObject Body)
		{
			GameObject subject = GameObject.FindByID(Receipt?.SubjectObjectId);
			Dominated dominated = subject?.GetEffect<Dominated>();
			Dominating projection = Body?.GetEffect<Dominating>();
			return subject != null && dominated?.Dominator == Body
				&& projection?.Target == subject;
		}

		private static void Resolve(KingdomStasisCustodyReceipt Receipt,
			out GameObject Body, out GameObject Cradle, out GameObject Anchor)
		{
			Body = GameObject.FindByID(Receipt?.BodyObjectId);
			Cradle = GameObject.FindByID(Receipt?.CradleObjectId);
			Anchor = GameObject.FindByID(Receipt?.FieldObjectId);
		}
	}
}
