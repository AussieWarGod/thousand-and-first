using System;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Exact body-side receipt for one city appointment. It never owns learned recipes.</summary>
	[Serializable]
	public sealed class r_KingdomNamedCook : IPart
	{
		public KingdomNamedCookReceipt Receipt = new KingdomNamedCookReceipt();

		internal void Stamp(KingdomNamedCookReceipt Source)
		{
			Receipt = Source?.Copy() ?? new KingdomNamedCookReceipt();
		}

		internal bool Matches(KingdomNamedCookReceipt Authority, GameObject Body)
		{
			string failure;
			return Authority != null && Receipt != null && Body != null
				&& KingdomNamedCookRules.Validate(Authority, out failure)
				&& KingdomNamedCookRules.Validate(Receipt, out failure)
				&& Authority.Phase != KingdomNamedCookPhase.Quarantined
				&& Receipt.Phase != KingdomNamedCookPhase.Quarantined
				&& string.Equals(Authority.RealmId, Receipt.RealmId, StringComparison.Ordinal)
				&& string.Equals(Authority.SettlementId, Receipt.SettlementId,
					StringComparison.Ordinal)
				&& Authority.ResidentId == Receipt.ResidentId
				&& Authority.Generation == Receipt.Generation
				&& string.Equals(Authority.BodyObjectId, Receipt.BodyObjectId,
					StringComparison.Ordinal)
				&& string.Equals(Authority.RecipeId, Receipt.RecipeId, StringComparison.Ordinal)
				&& string.Equals(Authority.GraphFingerprint, Receipt.GraphFingerprint,
					StringComparison.Ordinal)
				&& string.Equals(Body.IDIfAssigned, Authority.BodyObjectId,
					StringComparison.Ordinal);
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
			TeachesDish teaching = ParentObject?.GetPart<TeachesDish>();
			if (teaching != null && KingdomNamedCook.ExactTeaching(teaching, Receipt))
				ParentObject.RemovePart(teaching);
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomNamedCook));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomNamedCook));
			if (Receipt == null) Receipt = new KingdomNamedCookReceipt();
			Receipt.Normalize();
		}
	}
}
