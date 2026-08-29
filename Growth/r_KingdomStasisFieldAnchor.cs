using System;
using XRL.World;
using XRL.World.Effects;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Receipt on a runtime-only, phase-isolated native field carrier.</summary>
	[Serializable]
	public sealed class r_KingdomStasisFieldAnchor : IPart
	{
		public KingdomStasisCustodyReceipt Receipt = new KingdomStasisCustodyReceipt();

		internal void Stamp(KingdomStasisCustodyReceipt Source)
		{
			Receipt = Source?.Copy() ?? new KingdomStasisCustodyReceipt();
		}

		internal bool Matches(KingdomStasisCustodyReceipt Authority)
		{
			return KingdomStasisVaultRules.SameAuthority(Receipt, Authority)
				&& ParentObject != null && ParentObject.IDIfAssigned == Authority?.FieldObjectId;
		}

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			Stasisfield field = ParentObject?.GetPart<Stasisfield>();
			if (field != null) ParentObject.RemovePart(field);
			Phased phase = ParentObject?.GetEffect<Phased>();
			if (phase != null) ParentObject.RemoveEffect(phase);
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomStasisFieldAnchor));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomStasisFieldAnchor));
			if (Receipt == null) Receipt = new KingdomStasisCustodyReceipt();
			Receipt.Normalize();
		}
	}
}
