using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Object-local half of the realm's singular departure journal. It grants no
	/// authority without the exact matching system operation.</summary>
	[Serializable]
	public sealed class r_KingdomResidentDeparture : IPart
	{
		public int Version = KingdomResidentDepartureOperation.CurrentVersion;
		public string OperationId = "";
		public string RealmId = "";
		public int ResidentId;
		public string BodyObjectId = "";
		public bool TalliesClosed;

		internal bool Matches(KingdomResidentDepartureOperation Operation, GameObject Body)
		{
			return Operation != null && Body != null
				&& Version == KingdomResidentDepartureOperation.CurrentVersion
				&& OperationId == Operation.OperationId && RealmId == Operation.RealmId
				&& ResidentId == Operation.ResidentId
				&& BodyObjectId == Operation.BodyObjectId
				&& Body.IDIfAssigned == BodyObjectId
				&& Body.GetIntProperty(Simulation.City.KingdomResidents.ResidentIdProperty)
					== ResidentId;
		}

		public override bool CanGenerateStacked() => false;
		public override bool SameAs(IPart Part) => ReferenceEquals(this, Part);

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomResidentDeparture));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomResidentDeparture));
			OperationId = OperationId ?? ""; RealmId = RealmId ?? "";
			BodyObjectId = BodyObjectId ?? "";
		}
	}
}
