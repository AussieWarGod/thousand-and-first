using System;
using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_FounderBasin
	{
		public override void Initialize()
		{
			base.Initialize();
			EnsureOwnerNonce();
		}

		public override void ObjectLoaded()
		{
			base.ObjectLoaded();
			EnsureOwnerNonce();
		}

		public override bool CanGenerateStacked()
		{
			return false;
		}

		public override bool SameAs(IPart Part)
		{
			return ReferenceEquals(this, Part);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			// GameObject copies string/int property maps before parts. Strip every receipt and
			// authority key from clone, including CopyID=true paths, then mint new physical owner.
			ClearPendingRite();
			ParentObject?.RemoveProperty(OwnerNonceKey);
			TransientOwnerNonce = null;
			EnsureOwnerNonce();
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID ||
				ID == InventoryActionEvent.ID || ID == CanBeReplicatedEvent.ID;
		}

		public override bool HandleEvent(CanBeReplicatedEvent E)
		{
			// Polygel and other ordinary replication routes ask this event before DeepCopy.
			// A paid receipt belongs to one basin and cannot be copied into a second claimant.
			return !HasAnyReceiptState && base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Found", "found a settlement", "r_FoundKingdom", null, 'f', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_FoundKingdom" && E.Actor != null && E.Actor.IsPlayer())
			{
				TransientCompletion = null;
				KingdomFoundingResult result = AttemptFounding(E.Actor);
				if (result.ChargesEnergy)
				{
					E.Actor.UseEnergy(KingdomGovernanceRules.NominalEnergyCost,
						KingdomGovernanceRules.EnergyReason("found place"));
					E.RequestInterfaceExit();
					string completion = TransientCompletion;
					TransientCompletion = null;
					if (!string.IsNullOrEmpty(completion))
					{
						KingdomSystem.Guard("founding completion presentation", delegate
						{
							Popup.Show(completion);
						});
					}
				}
				else
				{
					TransientCompletion = null;
				}
			}
			return base.HandleEvent(E);
		}
	}
}
