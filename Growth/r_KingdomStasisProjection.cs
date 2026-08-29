using System;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Exact receipt beside one native Stasisfield projection.</summary>
	[Serializable]
	public sealed class r_KingdomStasisProjection : IPart
	{
		public KingdomStasisCustodyReceipt Receipt = new KingdomStasisCustodyReceipt();

		internal void Stamp(KingdomStasisCustodyReceipt Source)
		{
			Receipt = Source?.Copy() ?? new KingdomStasisCustodyReceipt();
		}

		internal bool Matches(KingdomStasisCustodyReceipt Authority)
		{
			return KingdomStasisVaultRules.SameAuthority(Receipt, Authority)
				&& ParentObject != null
				&& string.Equals(ParentObject.IDIfAssigned, Authority?.CradleObjectId,
					StringComparison.Ordinal);
		}

		public override bool CanGenerateStacked() { return false; }

		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomStasisProjection));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomStasisProjection));
			if (Receipt == null) Receipt = new KingdomStasisCustodyReceipt();
			Receipt.Normalize();
		}
	}
}
