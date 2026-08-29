using System;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>
	/// Exact, reversible warrant for one object deliberately given to a realm. Native
	/// <see cref="Physics.Owner"/> remains theft authority; this receipt owns only that slot.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomProperty : IPart
	{
		public int ReceiptVersion;
		public KingdomPropertyPhase Phase;
		public string OwnerRealmId = "";
		public string OwnerSettlementId = "";
		public string FactionId = "";
		public string ObjectId = "";
		public string PriorOwner = "";
		public long DesignatedTick;
		public long ReleasedTick;
		public string Fault = "";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID
				|| ID == CanBeReplicatedEvent.ID;
		}

		public override bool CanGenerateStacked()
		{
			return false;
		}

		public override bool SameAs(IPart Part)
		{
			return ReferenceEquals(this, Part);
		}

		public override bool HandleEvent(CanBeReplicatedEvent E)
		{
			if (Phase == KingdomPropertyPhase.Prepared
				|| Phase == KingdomPropertyPhase.Designated
				|| Phase == KingdomPropertyPhase.ReleasePrepared
				|| Phase == KingdomPropertyPhase.Quarantined) return false;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (Phase == KingdomPropertyPhase.Designated)
			{
				E.Postfix.Append("\n{{rules|Realm property: deliberately entered in the Charter. "
					+ "Taking or damaging it invokes Qud's native ownership law.}}");
			}
			else if (Phase == KingdomPropertyPhase.Prepared
				|| Phase == KingdomPropertyPhase.ReleasePrepared)
			{
				E.Postfix.Append("\n{{W|Realm property receipt awaits exact recovery at the Charter.}}");
			}
			else if (Phase == KingdomPropertyPhase.Quarantined)
			{
				E.Postfix.Append("\n{{R|Realm property receipt quarantined: live ownership changed "
					+ "outside its warrant, so the mod will not overwrite it.}}");
			}
			return base.HandleEvent(E);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			// Replication normally refuses before copying. Defensive nonstandard copy paths get
			// an ordinary object with its prior owner, never a second receipt for one object ID.
			if (ParentObject?.Physics != null
				&& string.Equals(ParentObject.Physics.Owner ?? "", FactionId ?? "",
					StringComparison.Ordinal))
			{
				ParentObject.Physics.Owner = string.IsNullOrEmpty(PriorOwner) ? null : PriorOwner;
			}
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomProperty));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomProperty));
			OwnerRealmId = OwnerRealmId ?? "";
			OwnerSettlementId = OwnerSettlementId ?? "";
			FactionId = FactionId ?? "";
			ObjectId = ObjectId ?? "";
			PriorOwner = PriorOwner ?? "";
			Fault = Fault ?? "";
		}
	}
}
