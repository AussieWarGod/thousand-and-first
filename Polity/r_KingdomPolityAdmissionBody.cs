using System;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Exact temporary custody bridge while a transferred cohort representative waits
	/// for its source cohort cleanup. Copies are inert and carry no authority.</summary>
	[Serializable]
	public sealed class r_KingdomPolityAdmissionBody : IPart
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public string RealmId;
		public string SettlementId;
		public string HandoffId;
		public string CohortId;
		public string MemberId;
		public string ProjectionId;
		public string SourceZoneId;
		public string BodyObjectId;
		public int ResidentId;
		public string AdmissionReceiptId;
		public string BodyReceiptId;
		public bool Inert;

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == CanBeReplicatedEvent.ID;
		}

		public override bool HandleEvent(CanBeReplicatedEvent E) { return false; }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			RealmId = SettlementId = HandoffId = CohortId = MemberId = ProjectionId =
				SourceZoneId = BodyObjectId = AdmissionReceiptId = BodyReceiptId = null;
			ResidentId = 0; Inert = true;
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
		}
	}
}
