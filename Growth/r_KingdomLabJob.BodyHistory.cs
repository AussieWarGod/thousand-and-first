using System;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomLabJob
	{
		public int BodyHistoryContractVersion;
		public int BodyHistoryPhase;
		public long BodyHistoryWitnessedTick = -1L;
		public string BodyHistoryPartFact = "";
		public string BodyHistoryEffectNonce = "";
		public string BodyHistoryOwnerReceiptId = "";
		public string BodyHistoryFault = "";

		internal bool BodyHistoryRequiresRulerLife =>
			BodyHistoryContractVersion == KingdomBodyHistoryRules.LabContractVersion;

		internal KingdomLabBodyHistoryPhase BodyHistoryState
		{
			get { return (KingdomLabBodyHistoryPhase)BodyHistoryPhase; }
			set { BodyHistoryPhase = (int)value; }
		}

		private void NormalizeBodyHistory(ref bool Malformed)
		{
			BodyHistoryPartFact = BodyHistoryPartFact ?? "";
			BodyHistoryEffectNonce = BodyHistoryEffectNonce ?? "";
			BodyHistoryOwnerReceiptId = BodyHistoryOwnerReceiptId ?? "";
			BodyHistoryFault = BodyHistoryFault ?? "";
			bool rulerBound = KingdomBodyHistoryRulerLifeRules.ValidIdentity(RealmId,
				RulerSuccessionOrdinal, "taf:object:" + PatientId, RulerLifeId);
			if (!KingdomLabBodyHistoryContractRules.TryResolveLoaded(
				BodyHistoryContractVersion, BodyHistoryPhase, rulerBound,
				out BodyHistoryContractVersion, out KingdomLabBodyHistoryPhase resolved))
				Malformed = true;
			BodyHistoryState = resolved;
			if (BodyHistoryContractVersion == 0)
			{
				Malformed |= BodyHistoryState != KingdomLabBodyHistoryPhase.LegacyPhysicalOnly
					|| BodyHistoryWitnessedTick != -1L || BodyHistoryPartFact.Length != 0
					|| BodyHistoryEffectNonce.Length != 0
					|| BodyHistoryOwnerReceiptId.Length != 0;
				return;
			}

			bool phase = Enum.IsDefined(typeof(KingdomLabBodyHistoryPhase), BodyHistoryState)
				&& BodyHistoryState != KingdomLabBodyHistoryPhase.LegacyPhysicalOnly;
			bool fact = BodyHistoryWitnessedTick >= 0L && BodyHistoryPartFact.Length > 0;
			bool factPartial = (BodyHistoryWitnessedTick >= 0L)
				!= (BodyHistoryPartFact.Length > 0);
			bool owner = BodyHistoryEffectNonce.Length > 0
				&& BodyHistoryOwnerReceiptId.Length > 0;
			bool ownerPartial = (BodyHistoryEffectNonce.Length > 0)
				!= (BodyHistoryOwnerReceiptId.Length > 0);
			Malformed |= BodyHistoryContractVersion != KingdomBodyHistoryRules.LabContractVersion
				|| !phase || BodyHistoryWitnessedTick < -1L || factPartial || ownerPartial
				|| fact && !KingdomBodyHistoryRules.ValidWitnessFact(BodyHistoryPartFact)
				|| BodyHistoryFault.Length > 2048
				|| owner && (!KingdomBodyHistoryRules.ValidEffectNonce(BodyHistoryEffectNonce)
					|| !KingdomBodyHistoryRules.ValidCompletedLabOwner(
						BodyHistoryOwnerReceiptId))
				|| BodyHistoryState == KingdomLabBodyHistoryPhase.Applied && (!fact || !owner);
		}
	}
}
