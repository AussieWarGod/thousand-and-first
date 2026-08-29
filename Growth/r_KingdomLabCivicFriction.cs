using System;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Per-hall nonvaluable cause/choice/closure receipts.</summary>
	[Serializable]
	public sealed class r_KingdomLabCivicFriction : IPart
	{
		public KingdomLabCivicReceipt SavantPrice = new KingdomLabCivicReceipt();
		public KingdomLabCivicReceipt RefusalDeparture = new KingdomLabCivicReceipt();

		internal KingdomLabCivicReceipt Receipt(KingdomLabCivicKind Kind)
		{
			return Kind == KingdomLabCivicKind.SavantPrice ? SavantPrice
				: Kind == KingdomLabCivicKind.RefusalDeparture ? RefusalDeparture : null;
		}

		internal void Stamp(KingdomLabCivicReceipt Receipt)
		{
			if (Receipt == null) return;
			if (Receipt.Kind == KingdomLabCivicKind.SavantPrice) SavantPrice = Receipt.Copy();
			else if (Receipt.Kind == KingdomLabCivicKind.RefusalDeparture)
				RefusalDeparture = Receipt.Copy();
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeDeathRemovalEvent.ID
				|| ID == OnDestroyObjectEvent.ID || ID == GetShortDescriptionEvent.ID
				|| ID == CanBeReplicatedEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			KingdomLabCivicRuntime.OnOwnerRemoving(this, "the laboratory owner died");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(OnDestroyObjectEvent E)
		{
			KingdomLabCivicRuntime.OnOwnerRemoving(this, "the laboratory owner was removed");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			string savant = KingdomLabCivicRules.StatusLine(SavantPrice);
			string leaving = KingdomLabCivicRules.StatusLine(RefusalDeparture);
			if (!string.IsNullOrEmpty(savant)) E.Postfix.Append("\n" + savant);
			if (!string.IsNullOrEmpty(leaving)) E.Postfix.Append("\n" + leaving);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CanBeReplicatedEvent E) { return false; }
		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			// A copy is not the object named by the global owner CAS. Strip its borrowed receipts;
			// a duplicate live ID is separately detected by the active-zone owner scan.
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomLabCivicFriction));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabCivicFriction));
			if (SavantPrice == null) SavantPrice = new KingdomLabCivicReceipt();
			if (RefusalDeparture == null) RefusalDeparture = new KingdomLabCivicReceipt();
		}
	}
}
