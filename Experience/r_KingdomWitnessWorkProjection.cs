using System;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Object-local proof for one fixed witness-work projection. This part owns only
	/// appended look text and zero-value/non-replication event answers; it never rewrites the
	/// carrier's blueprint, description, ownership, location, or contents. Qud 2.0.211.51
	/// evidence: GetShortDescriptionEvent.cs 47-68 dispatches the postfix event; GameObject.cs
	/// 565-592 dispatches intrinsic adjustment then extrinsic value.</summary>
	[Serializable]
	public sealed class r_KingdomWitnessWorkProjection : IPart
	{
		public const int CurrentVersion = 1;

		public int ProjectionVersion = CurrentVersion;
		public string RealmId = "";
		public string SettlementId = "";
		public string WorkId = "";
		public string SourceSnapshotDigest = "";
		public string CarrierReceiptId = "";
		public string CarrierObjectId = "";
		public string CarrierEngineId = "";
		public string CarrierZoneId = "";
		public string CarrierConstructionReceiptId = "";
		public int CarrierX = -1;
		public int CarrierY = -1;
		public string ProjectedDescription = "";
		public string ProjectionProof = "";

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("CanBeTaken");
			base.Register(Object, Registrar);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID
				|| ID == GetIntrinsicValueEvent.ID || ID == GetExtrinsicValueEvent.ID
				|| ID == CanBeReplicatedEvent.ID;
		}

		public override bool FireEvent(Event E)
		{
			if (E != null && E.ID == "CanBeTaken" && ShapeMatchesParent()) return false;
			return base.FireEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			try
			{
				if (E?.Postfix != null && ShapeMatchesParent())
					E.Postfix.Append("\n\n" + ProjectedDescription);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst witness-work description", error);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetIntrinsicValueEvent E)
		{
			if (E != null && ShapeMatchesParent()) E.Value = 0.0;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetExtrinsicValueEvent E)
		{
			if (E != null && ShapeMatchesParent()) E.Value = 0.0;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CanBeReplicatedEvent E)
		{
			return ShapeMatchesParent() ? false : base.HandleEvent(E);
		}

		public override bool CanGenerateStacked()
		{
			return ShapeMatchesParent() ? false : base.CanGenerateStacked();
		}

		public override bool SameAs(IPart Part)
		{
			return ShapeMatchesParent() ? ReferenceEquals(this, Part) : base.SameAs(Part);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			ParentObject?.RemovePart(this);
		}

		internal bool FieldsAuthenticated()
		{
			return ProjectionVersion == CurrentVersion && ProjectionProof != null
				&& ProjectionProof == KingdomWitnessWorkRules.ProjectionProof(ProjectionVersion,
					RealmId, SettlementId, WorkId, SourceSnapshotDigest, CarrierReceiptId,
					CarrierObjectId, CarrierEngineId, CarrierZoneId,
					CarrierConstructionReceiptId, CarrierX, CarrierY, ProjectedDescription);
		}

		internal bool ShapeMatchesParent()
		{
			GameObject carrier = ParentObject;
			return FieldsAuthenticated() && GameObject.Validate(carrier)
				&& Bounded(RealmId, KingdomWitnessWorkRules.MaxIdBytes)
				&& Bounded(SettlementId, KingdomWitnessWorkRules.MaxIdBytes)
				&& Bounded(WorkId, KingdomWitnessWorkRules.MaxIdBytes)
				&& SourceSnapshotDigest?.Length == 64
				&& Bounded(CarrierReceiptId, KingdomWitnessWorkRules.MaxIdBytes)
				&& CarrierReceiptId == KingdomWitnessWorkRules.CarrierReceiptId(WorkId,
					CarrierObjectId, CarrierZoneId, CarrierConstructionReceiptId,
					CarrierX, CarrierY)
				&& Bounded(CarrierObjectId, KingdomWitnessWorkRules.MaxIdBytes)
				&& Bounded(CarrierEngineId, KingdomWitnessWorkRules.MaxIdBytes)
				&& Bounded(CarrierZoneId, KingdomWitnessWorkRules.MaxIdBytes)
				&& Bounded(CarrierConstructionReceiptId,
					KingdomWitnessWorkRules.MaxIdBytes)
				&& Bounded(ProjectionProof, KingdomWitnessWorkRules.MaxIdBytes)
				&& CarrierObjectId == "taf:object:" + CarrierEngineId
				&& carrier.IDIfAssigned == CarrierEngineId
				&& CarrierConstructionReceiptId == "taf:construction:"
					+ (carrier.GetStringProperty(KingdomConstruction.ReceiptProperty) ?? "")
				&& carrier.CurrentCell != null && carrier.CurrentZone != null
				&& CarrierZoneId == "taf:zone:" + carrier.CurrentZone.ZoneID
				&& carrier.CurrentCell.X == CarrierX && carrier.CurrentCell.Y == CarrierY
				&& Bounded(ProjectedDescription, KingdomWitnessWorkRules.MaxDerivedTextBytes);
		}

		private static bool Bounded(string Value, int MaxBytes)
		{
			if (string.IsNullOrWhiteSpace(Value) || Value.IndexOf('\0') >= 0) return false;
			try
			{
				return new UTF8Encoding(false, true).GetByteCount(Value) <= MaxBytes;
			}
			catch (EncoderFallbackException) { return false; }
		}
	}
}
