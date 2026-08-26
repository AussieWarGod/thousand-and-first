using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		private static KingdomSealRecord Best(KingdomSealRecord A, KingdomSealRecord B)
		{
			if (A == null)
			{
				return B;
			}
			if (B == null)
			{
				return A;
			}
			return KingdomSealRules.Later(A, B) ? A : B;
		}

		private static bool SameRecord(KingdomSealRecord A, KingdomSealRecord B)
		{
			try
			{
				return A != null && B != null && A.Compose() == B.Compose();
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool ValidStageRecord(KingdomSealRecord Record)
		{
			if (Record == null)
			{
				return false;
			}
			try
			{
				KingdomSealRecord parsed;
				KingdomSealFault fault;
				string detail;
				return KingdomSealRecord.TryParse(Record.Compose(), out parsed, out fault, out detail)
					&& SameRecord(parsed, Record);
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool ValidGenerationHandoff(KingdomSealRecord Previous, KingdomSealRecord Successor, out string Failure)
		{
			Failure = "";
			if (!ValidStageRecord(Previous) || !ValidStageRecord(Successor)
				|| (Previous.Status != KingdomSealStatus.Living && Previous.Status != KingdomSealStatus.Retired)
				|| Successor.Status != KingdomSealStatus.Living || Successor.IsResolved)
			{
				Failure = "the handoff requires one complete living or retired stage and one complete living successor";
				return false;
			}
			if (Previous.LineageId != Successor.LineageId || Previous.OriginGameId != Successor.OriginGameId)
			{
				Failure = "a generation handoff cannot change lineage or origin game";
				return false;
			}
			if (Previous.Generation == int.MaxValue || Successor.Generation != Previous.Generation + 1)
			{
				Failure = "a generation handoff must advance exactly one generation";
				return false;
			}
			if (Previous.LegacyId == Successor.LegacyId)
			{
				Failure = "every generation must mint a distinct legacy id";
				return false;
			}
			if (Previous.Revision == int.MaxValue || Successor.Revision != Previous.Revision + 1)
			{
				Failure = "a generation handoff must advance the origin revision exactly once";
				return false;
			}
			if (Successor.WrittenTick < Previous.WrittenTick)
			{
				Failure = "a generation handoff cannot move its diagnostic tick backwards";
				return false;
			}
			return true;
		}

		private bool SlotIsBroken(string PathValue, KingdomSealRecord Record)
		{
			try
			{
				bool exists;
				string failure;
				return !TrySafeLeaf(PathValue, out exists, out failure) || (exists && Record == null);
			}
			catch (Exception)
			{
				return true;
			}
		}

		private static bool MayRestoreOver(KingdomSealRecord Existing, KingdomSealRecord Saved)
		{
			if (Existing == null)
			{
				return true;
			}
			if (Existing.Status != KingdomSealStatus.Living || Existing.OriginGameId != Saved.OriginGameId
				|| Existing.LineageId != Saved.LineageId)
			{
				return false;
			}
			if (Existing.Generation == Saved.Generation)
			{
				return Existing.LegacyId == Saved.LegacyId;
			}
			return Existing.Generation > Saved.Generation && Existing.LegacyId != Saved.LegacyId
				&& Existing.Revision > Saved.Revision
				&& Existing.WrittenTick >= Saved.WrittenTick;
		}

		private static bool MayRestoreRetirementOver(KingdomSealRecord Existing,
			KingdomSealRecord Retired)
		{
			if (Existing == null || SameRecord(Existing, Retired))
			{
				return true;
			}
			if (Existing.Status != KingdomSealStatus.Living
				|| Existing.OriginGameId != Retired.OriginGameId
				|| Existing.LineageId != Retired.LineageId)
			{
				return false;
			}
			if (Existing.Generation == Retired.Generation)
			{
				return Existing.LegacyId == Retired.LegacyId
					&& Existing.Revision < Retired.Revision;
			}
			return Existing.Generation > Retired.Generation
				&& Existing.LegacyId != Retired.LegacyId
				&& Existing.Revision > Retired.Revision
				&& Existing.WrittenTick >= Retired.WrittenTick;
		}

		private static bool RecoverableRetirementJournal(KingdomSealRecord A,
			KingdomSealRecord B)
		{
			if (A == null || B == null || SameStageIdentity(A, B))
			{
				return true;
			}
			KingdomSealRecord newer;
			return TryRecoverableGenerationPair(A, B, out newer);
		}

		private static bool RecoverableRestoreJournal(KingdomSealRecord A, KingdomSealRecord B,
			KingdomSealRecord Saved)
		{
			if (A == null || B == null || SameStageIdentity(A, B))
			{
				return true;
			}
			if (SameStageIdentity(A, Saved) || SameStageIdentity(B, Saved))
			{
				return true;
			}
			KingdomSealRecord newer;
			return TryRecoverableGenerationPair(A, B, out newer);
		}

		private static bool TryRecoverableGenerationPair(KingdomSealRecord A, KingdomSealRecord B, out KingdomSealRecord Newer)
		{
			Newer = null;
			if (A == null || B == null || A.OriginGameId != B.OriginGameId || A.LineageId != B.LineageId
				|| A.Generation == B.Generation)
			{
				return false;
			}
			KingdomSealRecord older = (A.Generation < B.Generation) ? A : B;
			KingdomSealRecord newer = object.ReferenceEquals(older, A) ? B : A;
			if (older.Generation == int.MaxValue || newer.Generation != older.Generation + 1
				|| older.LegacyId == newer.LegacyId
				|| (older.Status != KingdomSealStatus.Living && older.Status != KingdomSealStatus.Retired)
				|| newer.Status != KingdomSealStatus.Living
				|| older.Revision == int.MaxValue || newer.Revision != older.Revision + 1
				|| newer.WrittenTick < older.WrittenTick)
			{
				return false;
			}
			Newer = newer;
			return true;
		}

		private static bool SameStageIdentity(KingdomSealRecord A, KingdomSealRecord B)
		{
			return A == null || B == null || (A.OriginGameId == B.OriginGameId
				&& A.LineageId == B.LineageId && A.LegacyId == B.LegacyId && A.Generation == B.Generation);
		}

		private static bool SameReceipt(KingdomSealReceipt A, KingdomSealReceipt B)
		{
			return A != null && B != null && A.LineageId == B.LineageId && A.LegacyId == B.LegacyId
				&& A.TargetGameId == B.TargetGameId && A.State == B.State && A.WrittenTick == B.WrittenTick;
		}

		private static bool ValidReceipt(KingdomSealReceipt Receipt)
		{
			return Receipt != null && KingdomSealReceipt.ValidId(Receipt.LineageId)
				&& KingdomSealReceipt.ValidId(Receipt.LegacyId) && KingdomSealReceipt.ValidId(Receipt.TargetGameId)
				&& Receipt.WrittenTick >= 0L && (int)Receipt.State >= 0 && (int)Receipt.State <= 2;
		}

	}
}
