using System;
using System.Collections.Generic;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>One persisted, paid procedure job owned by its physical hall.</summary>
	[Serializable]
	public partial class r_KingdomLabJob : IPart
	{
		public string JobId = "";
		public string BuildingId = "";
		public string ProcedureKey = "";
		public string PatientId = "";
		public string GameId = "";
		public string RealmId = "";
		public long RealmFoundedTick;
		public int RulerSuccessionOrdinal = -1;
		public string RulerLifeId = "";
		public int BodyPartId;
		public string BearerId = "";
		public string Stamp = "";
		public string City = "";
		public int ContractVersion;
		public string FrozenName = "";
		public string FrozenGrants = "";
		public int FrozenSource = -1;
		public int FrozenAttach = -1;
		public string FrozenManager = "";
		public string FrozenDetail = "";
		public string FrozenMagnitude = "";
		public string FrozenCreeds = "";
		public int FrozenClass = -1;
		public int FrozenStaffDays;
		public string FrozenFingerprint = "";
		public int Phase;
		public int RemainingTicks;
		public long LastWorkedTick;
		public int WaterOwed;
		public int WaterPaid;
		public int WaterLost;
		public bool WaterMeasurementExact = true;
		public bool WaterQuarantined;
		public int KeptOwed;
		public int KeptPaid;
		public int KeptLost;
		public bool KeptMeasurementExact = true;
		public bool KeptQuarantined;
		public string BitClaim = "";
		public string BitOutstanding = "";
		public List<string> StandingFactions = new List<string>();
		public List<int> StandingDeltas = new List<int>();
		public List<int> StandingBefore = new List<int>();
		public List<int> StandingTargets = new List<int>();
		public List<int> StandingPhases = new List<int>();
		public bool GovernanceCommitted;
		public bool IntentPublished;
		public bool EffectCommitted;
		public bool OwnershipPublished;
		public int EffectBodyPartId;
		public int EffectPartOrdinal = -1;
		public bool MarkerCleanupPending;
		public bool MarkerCleaned;
		public bool RegistryFinalized;
		public bool SchemaQuarantined;
		public bool StandingApplied;
		public int StandingAppliedCount;
		public bool Announced;
		public bool Chronicled;
		public bool Spoken;
		public bool ReadyAnnounced;
		public int ReadyMessagePhase;
		public int TerminalMessagePhase;
		public string ReadyMessageText = "";
		public string TerminalMessageText = "";
		public string ReadyMessageEventId = "";
		public string ChronicleEventId = "";
		public string PetitionEventId = "";
		public string AnnounceEventId = "";
		public long PetitionAttemptTick = -1L;
		public string PetitionFaction = "";
		public string Fault = "";

		internal KingdomLabJobPhase State
		{
			get { return (KingdomLabJobPhase)Phase; }
			set { Phase = (int)value; }
		}

		public void Normalize()
		{
			JobId = JobId ?? "";
			BuildingId = BuildingId ?? "";
			ProcedureKey = ProcedureKey ?? "";
			PatientId = PatientId ?? "";
			GameId = GameId ?? "";
			RealmId = RealmId ?? "";
			RulerLifeId = RulerLifeId ?? "";
			BearerId = BearerId ?? "";
			Stamp = Stamp ?? "";
			City = City ?? "";
			FrozenName = FrozenName ?? "";
			FrozenGrants = FrozenGrants ?? "";
			FrozenManager = FrozenManager ?? "";
			FrozenDetail = FrozenDetail ?? "";
			FrozenMagnitude = FrozenMagnitude ?? "";
			FrozenCreeds = FrozenCreeds ?? "";
			FrozenFingerprint = FrozenFingerprint ?? "";
			BitClaim = BitClaim ?? "";
			BitOutstanding = BitOutstanding ?? "";
			ChronicleEventId = ChronicleEventId ?? "";
			PetitionEventId = PetitionEventId ?? "";
			AnnounceEventId = AnnounceEventId ?? "";
			ReadyMessageEventId = ReadyMessageEventId ?? "";
			ReadyMessageText = ReadyMessageText ?? "";
			TerminalMessageText = TerminalMessageText ?? "";
			PetitionFaction = PetitionFaction ?? "";
			Fault = Fault ?? "";
			bool bodyHistoryMalformed = false;
			NormalizeBodyHistory(ref bodyHistoryMalformed);
			bool malformed = bodyHistoryMalformed
				|| WaterOwed < 0 || WaterPaid < 0 || WaterPaid > WaterOwed
				|| WaterLost < 0 || KeptOwed < 0 || KeptPaid < 0 || KeptPaid > KeptOwed
				|| KeptLost < 0 || RemainingTicks < 0 || StandingAppliedCount < 0
				|| PetitionAttemptTick < -1L
				|| !Enum.IsDefined(typeof(KingdomLabJobPhase), (KingdomLabJobPhase)Phase)
				|| !KingdomLabRules.ValidEffectContract(ContractVersion, ProcedureKey,
					FrozenGrants, FrozenSource, FrozenAttach, FrozenManager, FrozenFingerprint,
					FrozenDetail)
				|| string.IsNullOrEmpty(JobId) || string.IsNullOrEmpty(BuildingId)
				|| string.IsNullOrEmpty(PatientId) || string.IsNullOrEmpty(GameId)
				|| string.IsNullOrEmpty(RealmId) || RealmFoundedTick < 0L
				|| (BodyHistoryRequiresRulerLife
					&& !KingdomBodyHistoryRulerLifeRules.ValidIdentity(RealmId,
						RulerSuccessionOrdinal, "taf:object:" + PatientId, RulerLifeId))
				|| JobId.Length > 128 || BuildingId.Length > 128 || PatientId.Length > 128
				|| GameId.Length > 256 || RealmId.Length > 256 || ProcedureKey.Length > 128
				|| FrozenName.Length > 512 || FrozenDetail.Length > 512
				|| FrozenMagnitude.Length > 512 || FrozenCreeds.Length > 4096
				|| !Enum.IsDefined(typeof(LabClass), (LabClass)FrozenClass) || FrozenStaffDays < 0
				|| Stamp.Length > 32768 || City.Length > 512
				|| BitClaim.Length > 4096 || BitOutstanding.Length > 4096
				|| ReadyMessageText.Length > 2048 || TerminalMessageText.Length > 2048
				|| !Enum.IsDefined(typeof(KingdomLabMessagePhase),
					(KingdomLabMessagePhase)ReadyMessagePhase)
				|| !Enum.IsDefined(typeof(KingdomLabMessagePhase),
					(KingdomLabMessagePhase)TerminalMessagePhase);
			if (ReadyAnnounced && ReadyMessagePhase == (int)KingdomLabMessagePhase.Pending)
				ReadyMessagePhase = (int)KingdomLabMessagePhase.Lost;
			if (Announced && TerminalMessagePhase == (int)KingdomLabMessagePhase.Pending)
				TerminalMessagePhase = (int)KingdomLabMessagePhase.Lost;
			ReadyMessagePhase = (int)KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)ReadyMessagePhase);
			TerminalMessagePhase = (int)KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)TerminalMessagePhase);
			WaterOwed = Math.Max(0, WaterOwed);
			WaterPaid = Math.Min(WaterOwed, Math.Max(0, WaterPaid));
			WaterLost = Math.Max(0, WaterLost);
			KeptOwed = Math.Max(0, KeptOwed);
			KeptPaid = Math.Min(KeptOwed, Math.Max(0, KeptPaid));
			KeptLost = Math.Max(0, KeptLost);
			RemainingTicks = Math.Max(0, RemainingTicks);
			if (!WaterMeasurementExact)
			{
				WaterQuarantined = true;
			}
			if (!KeptMeasurementExact)
			{
				KeptQuarantined = true;
			}
			StandingFactions = StandingFactions ?? new List<string>();
			StandingDeltas = StandingDeltas ?? new List<int>();
			StandingBefore = StandingBefore ?? new List<int>();
			StandingTargets = StandingTargets ?? new List<int>();
			StandingPhases = StandingPhases ?? new List<int>();
			if (StandingFactions.Count > KingdomLabRules.MaxStandingRows
				|| StandingDeltas.Count > KingdomLabRules.MaxStandingRows
				|| StandingBefore.Count > KingdomLabRules.MaxStandingRows
				|| StandingTargets.Count > KingdomLabRules.MaxStandingRows
				|| StandingPhases.Count > KingdomLabRules.MaxStandingRows)
			{
				malformed = true;
				Trim(StandingFactions, KingdomLabRules.MaxStandingRows);
				Trim(StandingDeltas, KingdomLabRules.MaxStandingRows);
				Trim(StandingBefore, KingdomLabRules.MaxStandingRows);
				Trim(StandingTargets, KingdomLabRules.MaxStandingRows);
				Trim(StandingPhases, KingdomLabRules.MaxStandingRows);
			}
			if (StandingDeltas.Count != StandingFactions.Count
				|| (StandingBefore.Count != 0 && StandingBefore.Count != StandingFactions.Count)
				|| StandingTargets.Count != StandingFactions.Count
				|| (StandingPhases.Count != 0 && StandingPhases.Count != StandingFactions.Count))
			{
				malformed = true;
			}
			while (StandingDeltas.Count < StandingFactions.Count)
			{
				StandingDeltas.Add(0);
			}
			while (StandingTargets.Count < StandingFactions.Count)
			{
				StandingTargets.Add(int.MinValue);
			}
			while (StandingBefore.Count < StandingFactions.Count)
			{
				StandingBefore.Add(int.MinValue);
			}
			while (StandingPhases.Count < StandingFactions.Count)
			{
				// A legacy row with a persisted target lacks a CAS before-value and is not
				// authorized to overwrite current standing.
				StandingPhases.Add(StandingTargets[StandingPhases.Count] == int.MinValue
					? (int)KingdomLabStandingPhase.Pending
					: (int)KingdomLabStandingPhase.Quarantined);
			}
			Trim(StandingDeltas, StandingFactions.Count);
			Trim(StandingBefore, StandingFactions.Count);
			Trim(StandingTargets, StandingFactions.Count);
			Trim(StandingPhases, StandingFactions.Count);
			for (int i = 0; i < StandingPhases.Count; i++)
			{
				if (!Enum.IsDefined(typeof(KingdomLabStandingPhase),
					(KingdomLabStandingPhase)StandingPhases[i])) malformed = true;
			}
			if (StandingAppliedCount > StandingFactions.Count)
			{
				malformed = true;
				StandingAppliedCount = StandingFactions.Count;
			}
			bool fundedPhase = State == KingdomLabJobPhase.Working
				|| State == KingdomLabJobPhase.Ready || State == KingdomLabJobPhase.Applying
				|| State == KingdomLabJobPhase.ApplicationRecovery || State == KingdomLabJobPhase.Complete;
			if (fundedPhase && (WaterPaid != WaterOwed || WaterQuarantined
				|| !string.IsNullOrEmpty(BitOutstanding) || KeptPaid != KeptOwed
				|| KeptQuarantined))
			{
				malformed = true;
			}
			if (State == KingdomLabJobPhase.Complete
				&& (!EffectCommitted || !OwnershipPublished || !MarkerCleaned
					|| !RegistryFinalized))
			{
				malformed = true;
			}
			if (malformed)
			{
				SchemaQuarantined = true;
				State = KingdomLabJobPhase.ApplicationRecovery;
				Fault = "The application receipt is malformed or predates exact effect contracts. It is quarantined; no payment, body mutation, cleanup, or telling will replay.";
			}
		}

		private static void Trim<T>(List<T> Values, int Count)
		{
			if (Values != null && Values.Count > Count)
			{
				Values.RemoveRange(Count, Values.Count - Count);
			}
		}

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
		{
			r_KingdomLabJob copy = (r_KingdomLabJob)base.DeepCopy(Parent, MapInv);
			copy.StandingFactions = new List<string>(StandingFactions ?? new List<string>());
			copy.StandingDeltas = new List<int>(StandingDeltas ?? new List<int>());
			copy.StandingBefore = new List<int>(StandingBefore ?? new List<int>());
			copy.StandingTargets = new List<int>(StandingTargets ?? new List<int>());
			copy.StandingPhases = new List<int>(StandingPhases ?? new List<int>());
			return copy;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			SchemaQuarantined = true;
			State = KingdomLabJobPhase.ApplicationRecovery;
			Fault = "Copied commission receipt has no canonical mutation authority.";
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomLabJob));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabJob));
			Normalize();
		}
	}
}
