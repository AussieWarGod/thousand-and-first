using System;
using XRL.World;

namespace XRL.World.ZoneParts
{
	/// <summary>
	/// Exact ownership marker paired with one native <see cref="AmbientStabilization"/>. Its
	/// presence is what lets TAF update or remove that otherwise anonymous vanilla zone part.
	/// </summary>
	[Serializable]
	public sealed class KingdomAssentingWardAuthority : IZonePart
	{
		public int Version = 1;
		public string RealmId = "";
		public string SettlementId = "";
		public string AuthorityId = "";
		public string BuildingObjectId = "";
		public int Generation;
		public int Strength;

		internal void Stamp(ThousandAndFirst.KingdomAssentingMootReceipt Receipt)
		{
			Version = Receipt?.Version ?? 0;
			RealmId = Receipt?.RealmId ?? "";
			SettlementId = Receipt?.SettlementId ?? "";
			AuthorityId = Receipt?.AuthorityId ?? "";
			BuildingObjectId = Receipt?.BuildingObjectId ?? "";
			Generation = Receipt?.Generation ?? 0;
			Strength = Receipt?.Strength ?? 0;
		}

		internal bool Matches(ThousandAndFirst.KingdomAssentingMootReceipt Receipt)
		{
			return Receipt != null && Version == Receipt.Version
				&& string.Equals(RealmId, Receipt.RealmId, StringComparison.Ordinal)
				&& string.Equals(SettlementId, Receipt.SettlementId, StringComparison.Ordinal)
				&& string.Equals(AuthorityId, Receipt.AuthorityId, StringComparison.Ordinal)
				&& string.Equals(BuildingObjectId, Receipt.BuildingObjectId,
					StringComparison.Ordinal)
				&& Generation == Receipt.Generation;
		}

		public override void Write(Zone Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomAssentingWardAuthority));
		}

		public override void Read(Zone Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomAssentingWardAuthority));
			RealmId = RealmId ?? "";
			SettlementId = SettlementId ?? "";
			AuthorityId = AuthorityId ?? "";
			BuildingObjectId = BuildingObjectId ?? "";
		}
	}
}
