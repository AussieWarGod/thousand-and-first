using System;
using XRL.World;
using XRL.World.Effects;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Body-side escape receipt. Domination ending always asks it to release first.</summary>
	[Serializable]
	public sealed class r_KingdomStasisCustody : IPart
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
				&& string.Equals(ParentObject.IDIfAssigned, Authority?.BodyObjectId,
					StringComparison.Ordinal);
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("DominationBroken");
			base.Register(Object, Registrar);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeDeathRemovalEvent.ID
				|| ID == OnDestroyObjectEvent.ID || ID == EnteredCellEvent.ID
				|| ID == GetShortDescriptionEvent.ID || ID == CanBeReplicatedEvent.ID;
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "DominationBroken")
				KingdomStasisVault.ReleaseFromBody(this, "domination ended");
			return base.FireEvent(E);
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			KingdomStasisVault.ReleaseFromBody(this, "the held body died");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(OnDestroyObjectEvent E)
		{
			KingdomStasisVault.ReleaseFromBody(this, "the held body was destroyed");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			if (Receipt?.Phase == KingdomStasisCustodyPhase.ReleasePrepared)
				KingdomStasisVault.ReleaseFromBody(this, "the released body found clear ground");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(Receipt?.Phase == KingdomStasisCustodyPhase.Active
				? "\n{{rules|Held by one exact stasis-vault custody receipt. Gear remains on this body.}}"
				: "\n{{W|Stasis custody is recovering; inspect its vault.}}");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CanBeReplicatedEvent E) { return false; }
		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			Stasis stasis = ParentObject?.GetEffect<Stasis>();
			if (stasis != null) ParentObject.RemoveEffect(stasis);
			Phased phase = ParentObject?.GetEffect<Phased>();
			if (phase != null) ParentObject.RemoveEffect(phase);
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomStasisCustody));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomStasisCustody));
			if (Receipt == null) Receipt = new KingdomStasisCustodyReceipt();
			Receipt.Normalize();
		}
	}
}
