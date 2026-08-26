using System;
using System.Collections.Generic;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>One patient-owned, persisted graft-removal receipt.</summary>
	[Serializable]
	public class r_KingdomLabRemovalJob : IPart
	{
		public string RemovalId = "";
		public string ProcedureKey = "";
		public string OriginalJobId = "";
		public string PatientId = "";
		public string GameId = "";
		public string RealmId = "";
		public long RealmFoundedTick;
		public int BodyPartId;
		public string BearerId = "";
		public string City = "";
		public int ContractVersion;
		public string FrozenName = "";
		public string FrozenGrants = "";
		public int FrozenSource = -1;
		public int FrozenAttach = -1;
		public string FrozenManager = "";
		public string FrozenDetail = "";
		public string FrozenFingerprint = "";
		public string EffectNonce = "";
		public int PartOrdinal = -1;
		public int WaterOwed;
		public int WaterPaid;
		public int WaterLost;
		public bool WaterMeasurementExact = true;
		public bool WaterQuarantined;
		public int Phase;
		public bool GovernanceCommitted;
		public bool EffectRemoved;
		public bool OwnershipCleaned;
		public bool RecordCleaned;
		public bool Chronicled;
		public bool Announced;
		public int TerminalMessagePhase;
		public string TerminalMessageText = "";
		public bool ReceiptPresented;
		public bool SchemaQuarantined;
		public string ChronicleEventId = "";
		public string AnnounceEventId = "";
		public string Fault = "";

		internal KingdomLabRemovalPhase State
		{
			get { return (KingdomLabRemovalPhase)Phase; }
			set { Phase = (int)value; }
		}

		public void Normalize()
		{
			RemovalId = RemovalId ?? "";
			ProcedureKey = ProcedureKey ?? "";
			OriginalJobId = OriginalJobId ?? "";
			PatientId = PatientId ?? "";
			GameId = GameId ?? "";
			RealmId = RealmId ?? "";
			BearerId = BearerId ?? "";
			City = City ?? "";
			FrozenName = FrozenName ?? "";
			FrozenGrants = FrozenGrants ?? "";
			FrozenManager = FrozenManager ?? "";
			FrozenDetail = FrozenDetail ?? "";
			FrozenFingerprint = FrozenFingerprint ?? "";
			EffectNonce = EffectNonce ?? "";
			ChronicleEventId = ChronicleEventId ?? "";
			AnnounceEventId = AnnounceEventId ?? "";
			TerminalMessageText = TerminalMessageText ?? "";
			Fault = Fault ?? "";
			bool malformed = WaterOwed < 0 || WaterPaid < 0 || WaterPaid > WaterOwed
				|| WaterLost < 0 || string.IsNullOrEmpty(RemovalId)
				|| string.IsNullOrEmpty(OriginalJobId) || string.IsNullOrEmpty(PatientId)
				|| string.IsNullOrEmpty(GameId) || string.IsNullOrEmpty(RealmId)
				|| RealmFoundedTick < 0L || RemovalId.Length > 128 || OriginalJobId.Length > 128
				|| PatientId.Length > 128 || GameId.Length > 256 || RealmId.Length > 256
				|| !KingdomLabRules.ValidEffectContract(ContractVersion, ProcedureKey,
					FrozenGrants, FrozenSource, FrozenAttach, FrozenManager, FrozenFingerprint,
					FrozenDetail) || EffectNonce.Length != 32
				|| TerminalMessageText.Length > 2048
				|| !Enum.IsDefined(typeof(KingdomLabMessagePhase),
					(KingdomLabMessagePhase)TerminalMessagePhase);
			if (Announced && TerminalMessagePhase == (int)KingdomLabMessagePhase.Pending)
				TerminalMessagePhase = (int)KingdomLabMessagePhase.Lost;
			TerminalMessagePhase = (int)KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)TerminalMessagePhase);
			WaterOwed = Math.Max(0, WaterOwed);
			WaterPaid = Math.Min(WaterOwed, Math.Max(0, WaterPaid));
			WaterLost = Math.Max(0, WaterLost);
			if (!WaterMeasurementExact)
			{
				WaterQuarantined = true;
			}
			if (!Enum.IsDefined(typeof(KingdomLabRemovalPhase), (KingdomLabRemovalPhase)Phase))
			{
				malformed = true;
			}
			if (malformed)
			{
				SchemaQuarantined = true;
				State = KingdomLabRemovalPhase.Quarantined;
				Fault = "The removal receipt is malformed or predates exact effect contracts. It is quarantined rather than replayed.";
			}
		}

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			EffectNonce = Guid.NewGuid().ToString("N");
			SchemaQuarantined = true;
			State = KingdomLabRemovalPhase.Quarantined;
			Fault = "Copied removal receipt has a fresh nonce and no body authority.";
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomLabRemovalJob));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabRemovalJob));
			Normalize();
		}
	}
}
