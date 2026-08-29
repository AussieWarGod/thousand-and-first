using System;
using System.Collections.Generic;
using ThousandAndFirst;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Physical owner of one realm authority and its bounded hosted-lot receipts.</summary>
	[Serializable]
	public sealed class r_KingdomArcology : IPart
	{
		public List<string> LotReceipts = new List<string>();
		public string QuarantineReason;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == GetShortDescriptionEvent.ID
				|| ID == ZoneActivatedEvent.ID || ID == ZoneThawedEvent.ID
				|| ID == BeforeDestroyObjectEvent.ID;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Hosted lots", "read or commission hosted floors",
				"r_OpenHostedArcology", null, 'h', FireOnActor: false, 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenHostedArcology" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("hosted arcology", delegate {
					KingdomHostedArcology.Open(this, E.Actor); });
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n\n" + KingdomHostedArcology.Status(this));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			Reconcile(); return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			Reconcile(); return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeDestroyObjectEvent E)
		{
			return false;
		}

		private void Reconcile()
		{
			string failure;
			if (!KingdomHostedArcology.ReconcileRoot(ParentObject, out failure))
				KingdomHostedArcology.Quarantine(this, failure);
		}

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			LotReceipts = new List<string>();
			QuarantineReason = "copied hosted carriers cannot acquire realm authority";
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomArcology));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomArcology));
			if (LotReceipts == null) LotReceipts = new List<string>();
			if (LotReceipts.Count > KingdomHostedArcologyRules.MaxHostedLots)
				QuarantineReason = "hosted-lot slate exceeds its bounded capacity";
		}
	}
}
