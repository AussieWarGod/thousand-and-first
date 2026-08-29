using System;
using XRL;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomStasisVault
	{
		private static bool ProjectNativeField(r_KingdomStasisVault Vault,
			KingdomStasisCustodyReceipt Receipt, GameObject Body, GameObject Cradle,
			out string Failure)
		{
			Failure = "";
			if (!ExactMarkers(Receipt, Body, Cradle) || Body.GetPhase() != 1)
			{
				Failure = "Stasis projection lost its exact prepared evidence.";
				return false;
			}
			try
			{
				GameObject anchor = GameObject.Create("r_KingdomStasisFieldAnchor");
				if (anchor == null || Cradle.CurrentCell == null)
				{
					Failure = "The exact field carrier could not be created.";
					return false;
				}
				anchor.ID = Receipt.FieldObjectId;
				r_KingdomStasisFieldAnchor witness = new r_KingdomStasisFieldAnchor();
				witness.Stamp(Receipt);
				anchor.AddPart(witness);
				Cradle.CurrentCell.AddObject(anchor, Forced: true, System: true,
					NoStack: true, Silent: true);
				if (anchor.CurrentCell != Cradle.CurrentCell
					|| !anchor.ForceApplyEffect(new Phased(9999))
					|| !Body.ForceApplyEffect(new Phased(9999)))
				{
					Failure = "The phase interlock did not isolate the exact bay.";
					return false;
				}
				if (Body.GetPhase() != 2 || anchor.GetPhase() != 2)
				{
					Failure = "The exact body and field carrier did not enter one isolated phase.";
					return false;
				}
				Stasisfield field = new Stasisfield { Creator = Vault.ParentObject };
				anchor.AddPart(field);
				if (!ReferenceEquals(anchor.GetPart<Stasisfield>(), field)
					|| !witness.Matches(Receipt))
				{
					Failure = "The native stasis field did not bind to the exact cradle.";
					return false;
				}
				KingdomStasisCustodyReceipt projected =
					KingdomStasisVaultRules.FieldProjected(Receipt);
				if (projected == null)
				{
					Failure = "The field-projected receipt transition was refused.";
					return false;
				}
				StampAll(Vault, projected, Body, Cradle, anchor);
				field.ProcessStasis();
				if (!Body.IsInStasis())
				{
					Failure = "The native field did not still the exact dormant body.";
					return false;
				}
				KingdomStasisCustodyReceipt active =
					KingdomStasisVaultRules.Activated(projected);
				if (active == null)
				{
					Failure = "The active custody transition was refused.";
					return false;
				}
				StampAll(Vault, active, Body, Cradle, anchor);
				return true;
			}
			catch (Exception ex)
			{
				Failure = "Native stasis projection threw " + ex.GetType().Name + ".";
				return false;
			}
		}

		private static bool ExactMarkers(KingdomStasisCustodyReceipt Receipt,
			GameObject Body, GameObject Cradle)
		{
			return Body?.GetPart<r_KingdomStasisCustody>()?.Matches(Receipt) == true
				&& Cradle?.GetPart<r_KingdomStasisProjection>()?.Matches(Receipt) == true;
		}

		private static void StampAll(r_KingdomStasisVault Vault,
			KingdomStasisCustodyReceipt Receipt, GameObject Body, GameObject Cradle,
			GameObject Anchor = null)
		{
			Put(Vault, Receipt);
			Body?.GetPart<r_KingdomStasisCustody>()?.Stamp(Receipt);
			Cradle?.GetPart<r_KingdomStasisProjection>()?.Stamp(Receipt);
			Anchor?.GetPart<r_KingdomStasisFieldAnchor>()?.Stamp(Receipt);
		}

		private static void AbortEntry(r_KingdomStasisVault Vault,
			KingdomStasisCustodyReceipt Receipt, GameObject Body, GameObject Cradle,
			string Reason)
		{
			KingdomStasisCustodyReceipt releasing =
				KingdomStasisVaultRules.BeginRelease(Receipt);
			if (releasing == null) releasing = Receipt;
			GameObject anchor = GameObject.FindByID(Receipt.FieldObjectId);
			DetachOwned(releasing, Body, Cradle, anchor, out _);
			KingdomStasisCustodyReceipt terminal = releasing.Phase
				== KingdomStasisCustodyPhase.ReleasePrepared
				? KingdomStasisVaultRules.Released(releasing,
					Math.Max(releasing.EnteredTick, The.Game?.TimeTicks ?? 0L)) : null;
			Put(Vault, terminal ?? KingdomStasisVaultRules.Quarantined(Receipt, Reason));
		}
	}
}
