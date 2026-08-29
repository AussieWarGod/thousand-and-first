using System;
using XRL;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomStasisVault
	{
		internal static bool TryRelease(r_KingdomStasisVault Vault, int SlotIndex,
			string Reason, out string Failure)
		{
			Failure = "";
			KingdomStasisCustodyReceipt receipt = Slot(Vault, SlotIndex);
			if (receipt == null || receipt.Phase == KingdomStasisCustodyPhase.Released)
			{
				Failure = "That bay is already empty.";
				return false;
			}
			string shapeFailure;
			if (!KingdomStasisVaultRules.Validate(receipt, out shapeFailure))
			{
				Failure = "The bay receipt is malformed and was not allowed to name an object.";
				return false;
			}
			Resolve(receipt, out GameObject body, out GameObject cradle,
				out GameObject anchor);
			if (body == null)
			{
				Failure = "The exact held body is absent; the bay was quarantined instead of being declared empty.";
				QuarantineAfterCleanup(Vault, receipt, body, cradle, anchor, Failure);
				return false;
			}
			bool custodyDrift = BodyManifestChanged(receipt, body);
			KingdomStasisCustodyReceipt releasing = receipt.Phase
				== KingdomStasisCustodyPhase.Quarantined ? receipt
				: KingdomStasisVaultRules.BeginRelease(receipt);
			if (releasing == null)
			{
				Failure = "That custody phase cannot begin release.";
				return false;
			}
			StampAll(Vault, releasing, body, cradle, anchor);
			bool detached = DetachOwned(releasing, body, cradle, anchor, out Failure);
			if (!detached)
			{
				Put(Vault, releasing);
				return false;
			}
			long now = Math.Max(receipt.EnteredTick, The.Game?.TimeTicks ?? 0L);
			string warning = custodyDrift
				? "Inventory, equipment, or effect custody changed while stilled; the body was released without rewriting it."
				: "";
			KingdomStasisCustodyReceipt terminal = receipt.Phase
				== KingdomStasisCustodyPhase.Quarantined
				? KingdomStasisVaultRules.RetireQuarantine(receipt, now)
				: KingdomStasisVaultRules.Released(releasing, now, warning);
			if (terminal == null)
			{
				Failure = "The terminal custody receipt could not be persisted.";
				Put(Vault, KingdomStasisVaultRules.Quarantined(releasing, Failure));
				return false;
			}
			Put(Vault, terminal);
			if (custodyDrift)
			{
				Failure = warning;
			}
			return true;
		}

		private static bool DetachOwned(KingdomStasisCustodyReceipt Receipt,
			GameObject Body, GameObject Cradle, GameObject Anchor, out string Failure)
		{
			Failure = "";
			r_KingdomStasisCustody bodyMarker = Body?.GetPart<r_KingdomStasisCustody>();
			r_KingdomStasisProjection cradleMarker =
				Cradle?.GetPart<r_KingdomStasisProjection>();
			r_KingdomStasisFieldAnchor anchorMarker =
				Anchor?.GetPart<r_KingdomStasisFieldAnchor>();
			bool ownsBody = bodyMarker?.Matches(Receipt) == true;
			bool ownsCradle = cradleMarker?.Matches(Receipt) == true;
			bool ownsAnchor = anchorMarker?.Matches(Receipt) == true;
			if (Body != null && bodyMarker != null && !ownsBody)
			{
				Failure = "The exact body carries a different custody marker; it was not overwritten.";
				return false;
			}
			if (Body != null && bodyMarker == null
				&& (Body.IsInStasis() || Body.HasEffect<Phased>()))
			{
				Failure = "The exact body has an unowned stasis or phase projection; it was not overwritten.";
				return false;
			}
			if (Cradle != null && cradleMarker != null && !ownsCradle)
			{
				Failure = "The exact cradle carries a different projection marker; it was not overwritten.";
				return false;
			}
			if (Anchor != null && !ownsAnchor)
			{
				Failure = "The exact field carrier carries a different receipt; it was not overwritten.";
				return false;
			}
			try
			{
				if (ownsAnchor)
				{
					Stasisfield field = Anchor.GetPart<Stasisfield>();
					if (field != null) { field.ShutdownStasis(); Anchor.RemovePart(field); }
				}
				Stasis stasis = Body?.GetEffect<Stasis>();
				if (ownsBody && stasis != null) Body.RemoveEffect(stasis);
				if (ownsAnchor && GameObject.Validate(Anchor))
					Anchor.Obliterate(null, Silent: true);
				if (ownsCradle) Cradle.RemovePart(cradleMarker);
				Phased phase = Body?.GetEffect<Phased>();
				if (ownsBody && phase != null && !CanPhaseIn(Body))
				{
					bodyMarker.Stamp(AsReleasePrepared(Receipt));
					Failure = "Stasis is off, but solid matter blocks safe phase-in. End domination, move the released body to clear ground, and it will phase in automatically.";
					return false;
				}
				if (ownsBody && phase != null) Body.RemoveEffect(phase);
				if (ownsBody) Body.RemovePart(bodyMarker);
			}
			catch (Exception ex)
			{
				Failure = "Exact stasis release threw " + ex.GetType().Name + ".";
				return false;
			}
			if (ownsBody && (Body.GetPart<r_KingdomStasisCustody>() != null
				|| Body.IsInStasis() || Body.HasEffect<Phased>()))
			{
				Failure = "The body retained an owned stasis or phase projection.";
				return false;
			}
			return true;
		}

		private static KingdomStasisCustodyReceipt AsReleasePrepared(
			KingdomStasisCustodyReceipt Receipt)
		{
			if (Receipt.Phase == KingdomStasisCustodyPhase.ReleasePrepared) return Receipt;
			return KingdomStasisVaultRules.BeginRelease(Receipt) ?? Receipt;
		}

		private static bool BodyManifestChanged(KingdomStasisCustodyReceipt Receipt,
			GameObject Body)
		{
			return Body != null && (Receipt.InventoryFingerprint != InventoryFingerprint(Body)
				|| Receipt.EquipmentFingerprint != EquipmentFingerprint(Body)
				|| Receipt.EffectFingerprint != EffectFingerprint(Body));
		}
	}
}
