using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TrySelectBodyAuthority(KingdomSystem System, GameObject Work,
			GameObject Actor, KingdomPurposePairReceipt Pair, KingdomPurposeKind Source,
			string OperationId, out string ProcedureKey, out string Receipt,
			out string Quote, out string Failure)
		{
			ProcedureKey = null;
			Receipt = null;
			Quote = null;
			Failure = null;
			if (Source == KingdomPurposeKind.Flesh)
				return KingdomLab.TrySelectPurposeProcedure(Work, Actor, System, Pair.PairId,
					Pair.Epoch, OperationId, out ProcedureKey, out Receipt, out Quote,
					out Failure);
			if (Source == KingdomPurposeKind.Chrome)
				return KingdomAnnexe.TrySelectPurposeEnrollment(System, Work, Actor, Pair.PairId,
					Pair.Epoch, OperationId, out ProcedureKey, out Receipt, out Quote,
					out Failure);
			return true;
		}

		private static bool TryPreflightBodyAuthority(KingdomSystem System,
			GameObject Work, KingdomPurposeOperationReceipt Operation, out string Failure)
		{
			Failure = null;
			bool body = Operation.SourceKind == KingdomPurposeKind.Flesh
				|| Operation.SourceKind == KingdomPurposeKind.Chrome;
			if (!body) return string.IsNullOrEmpty(Operation.ProcedureKey)
				&& string.IsNullOrEmpty(Operation.ProcedureReceipt)
				|| Fail("A non-body operation carries a body-service authority.", out Failure);
			if (!KingdomPurposeBodyAuthorityRules.TryDecode(Operation.ProcedureReceipt,
				out KingdomPurposeBodyAuthority authority)
				|| authority.Kind != Operation.SourceKind || authority.PairId != Operation.PairId
				|| authority.PairEpoch != Operation.PairEpoch
				|| authority.OperationId != Operation.OperationId
				|| authority.ProcedureKey != Operation.ProcedureKey)
				return Fail("The frozen body-service authority does not match this operation.",
					out Failure);
			if (Operation.SourceKind == KingdomPurposeKind.Flesh)
			{
				GameObject actor = GameObject.FindByID(authority.SubjectObjectId);
				return KingdomLab.TryPreflightPurposeProcedure(Work, actor, System, authority,
					Operation.WaterRequested, out Failure);
			}
			return KingdomAnnexe.TryPreflightPurposeEnrollment(System, Work, authority,
				Operation.WaterRequested, out Failure);
		}

		private static KingdomPurposeBodyDriveState DriveBodyAuthority(KingdomSystem System,
			GameObject Work, KingdomPurposeOperationReceipt Operation, out string Failure)
		{
			Failure = null;
			if (!KingdomPurposeBodyAuthorityRules.TryDecode(Operation.ProcedureReceipt,
				out KingdomPurposeBodyAuthority authority)
				|| authority.OperationId != Operation.OperationId
				|| authority.PairId != Operation.PairId || authority.PairEpoch != Operation.PairEpoch
				|| authority.Kind != Operation.SourceKind
				|| authority.ProcedureKey != Operation.ProcedureKey)
			{
				Failure = "The operation's body-service authority is malformed or mismatched.";
				return KingdomPurposeBodyDriveState.Invalid;
			}
			return Operation.SourceKind == KingdomPurposeKind.Flesh
				? KingdomLab.DrivePurposeProcedure(Work, System, authority, out Failure)
				: KingdomAnnexe.DrivePurposeEnrollment(System, Work, authority, out Failure);
		}
	}
}
