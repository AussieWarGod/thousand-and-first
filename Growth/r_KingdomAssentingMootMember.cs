using System;
using XRL;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Reversible exact body projection for assent and/or ambient-field exemption.</summary>
	[Serializable]
	public sealed class r_KingdomAssentingMootMember : IPart
	{
		public int Version = 1;
		public string RealmId = "";
		public string SettlementId = "";
		public string AuthorityId = "";
		public string BuildingObjectId = "";
		public string BodyObjectId = "";
		public int Generation;
		public int ResidentId;
		public int Roles;

		internal void Stamp(KingdomAssentingMootReceipt Receipt, int Resident,
			string BodyId, int MemberRoles)
		{
			Version = Receipt?.Version ?? 0;
			RealmId = Receipt?.RealmId ?? "";
			SettlementId = Receipt?.SettlementId ?? "";
			AuthorityId = Receipt?.AuthorityId ?? "";
			BuildingObjectId = Receipt?.BuildingObjectId ?? "";
			BodyObjectId = BodyId ?? "";
			Generation = Receipt?.Generation ?? 0;
			ResidentId = Resident;
			Roles = MemberRoles;
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("ApplyAmbientRealityStabilized");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "ApplyAmbientRealityStabilized" && (Roles & 2) != 0
				&& KingdomAssentingMoot.ExemptionStillActive(this, ParentObject)) return false;
			return base.FireEvent(E);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			try { KingdomAssentingMoot.OnMemberDeath(this, ParentObject); }
			catch (Exception ex)
			{
				KingdomLog.Log("assenting moot: death recovery pending (" + ex.GetType().Name + ")");
			}
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

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomAssentingMootMember));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomAssentingMootMember));
			RealmId = RealmId ?? "";
			SettlementId = SettlementId ?? "";
			AuthorityId = AuthorityId ?? "";
			BuildingObjectId = BuildingObjectId ?? "";
			BodyObjectId = BodyObjectId ?? "";
		}
	}
}
