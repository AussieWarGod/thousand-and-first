using System;
using System.Collections.Generic;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Physical owner of four bounded body-custody slots.</summary>
	[Serializable]
	public sealed class r_KingdomStasisVault : IPart
	{
		public int NextGeneration = 1;
		public List<KingdomStasisCustodyReceipt> Slots =
			new List<KingdomStasisCustodyReceipt>();

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == GetShortDescriptionEvent.ID
				|| ID == ZoneActivatedEvent.ID || ID == ZoneThawedEvent.ID
				|| ID == OnDestroyObjectEvent.ID;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Custody", "read the vault's custody slate",
				"r_OpenStasisVault", null, 'v', FireOnActor: false, 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenStasisVault" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomStasisVault.Open(this, E.Actor);
				return true;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomStasisVault.Description(this));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			KingdomStasisVault.Reconcile(this);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			KingdomStasisVault.Reconcile(this);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(OnDestroyObjectEvent E)
		{
			KingdomStasisVault.ReleaseAll(this, "the vault was destroyed");
			return base.HandleEvent(E);
		}

		public override bool CanGenerateStacked()
		{
			return false;
		}

		public override bool SameAs(IPart Part)
		{
			return ReferenceEquals(this, Part);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			Slots = new List<KingdomStasisCustodyReceipt>();
			NextGeneration = 1;
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomStasisVault));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomStasisVault));
			KingdomStasisVault.Normalize(this);
		}
	}
}
