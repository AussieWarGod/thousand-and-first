using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static string LodgeSourceProof(KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt receipt)
		{
			return HashId("lodge-source", delegate(BinaryWriter w)
			{
				CanonicalString(w, op.Id); CanonicalString(w, op.PlanHash);
				CanonicalString(w, receipt.ReceiptId); CanonicalString(w, receipt.SettlementId);
				CanonicalString(w, receipt.ObjectId); CanonicalString(w, receipt.Blueprint);
				w.Write(receipt.ResidentId); CanonicalString(w, receipt.ResidentName);
				CanonicalString(w, receipt.ResidentOrigin);
				CanonicalString(w, receipt.ResidentArrival);
				w.Write(receipt.ResidentArrivalTick);
				CanonicalString(w, receipt.ResidentBoundZoneId);
			});
		}

		private static string LodgeDeathProof(KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt receipt)
		{
			return HashId(receipt.ResidentId > 0 ? "lodge-resident-death" : "lodge-body-death",
				delegate(BinaryWriter w)
				{
					CanonicalString(w, op.Id); CanonicalString(w, op.PlanHash);
					CanonicalString(w, receipt.ReceiptId);
					CanonicalString(w, receipt.SourceProofId);
					CanonicalString(w, receipt.MarketSourceProofId);
					CanonicalString(w, receipt.ObjectId); CanonicalString(w, receipt.Blueprint);
					CanonicalString(w, receipt.ResidentBoundZoneId);
					w.Write(receipt.ResidentId); w.Write(receipt.DeathCause);
					w.Write(receipt.TerminalTick);
				});
		}

		private static string LodgeDeathProofV9(KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt receipt)
		{
			return HashId(receipt.ResidentId > 0 ? "lodge-resident-death" : "lodge-body-death",
				delegate(BinaryWriter w)
				{
					CanonicalString(w, op.Id); CanonicalString(w, op.PlanHash);
					CanonicalString(w, receipt.ReceiptId);
					CanonicalString(w, receipt.SourceProofId);
					CanonicalString(w, receipt.ObjectId); CanonicalString(w, receipt.Blueprint);
					CanonicalString(w, receipt.ResidentBoundZoneId);
					w.Write(receipt.ResidentId); w.Write(receipt.DeathCause);
					w.Write(receipt.TerminalTick);
				});
		}

		internal static string LodgeDeathProofForV9Wire(KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt receipt)
		{
			return op == null || receipt == null ? null : LodgeDeathProofV9(op, receipt);
		}

		private static string LegacyLodgeMarketSourceProof(KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt receipt)
		{
			return HashId("lodge-market-source", delegate(BinaryWriter w)
			{
				CanonicalString(w, op.Id); CanonicalString(w, op.PlanHash); w.Write(op.Sequence);
				CanonicalString(w, receipt.ReceiptId);
				CanonicalString(w, receipt.SettlementId);
				CanonicalString(w, receipt.ObjectId); w.Write(receipt.ResidentId);
				CanonicalString(w, receipt.MarketSourceBodyObjectId);
				w.Write(receipt.MarketSourceResidentId); w.Write(receipt.MarketTier);
				CanonicalString(w, receipt.MarketIntent);
			});
		}

		private static string LodgeMarketSourceProof(KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt receipt)
		{
			return HashId("lodge-market-source-state", delegate(BinaryWriter w)
			{
				CanonicalString(w, LegacyLodgeMarketSourceProof(op, receipt));
				w.Write(receipt.MarketSourcePrepared);
			});
		}

		private static bool LodgeTerminalShape(KingdomLifecycleOperation op, bool publication)
		{
			KingdomLifecycleLodgeTerminalReceipt r = op == null ? null : op.LodgeTerminal;
			if (op == null || op.Action != KingdomLifecycleAction.Lodge) return r == null;
			if (publication) return r == null;
			if (r == null) return true;
			bool common = r.Version == KingdomLifecycleLodgeTerminalReceipt.CurrentVersion
				&& r.OperationId == op.Id && r.PlanHash == op.PlanHash
				&& r.ReceiptId == ChildId(op.Id, "lodge-terminal", 0)
				&& r.SettlementId == op.SettlementId && r.ObjectId == op.ObjectId
				&& r.Blueprint == op.Blueprint && ValidRootId(r.ObjectId)
				&& ValidName(r.Blueprint) && r.TerminalTick >= 0L
				&& !TooLong(r.ResidentName, MaxNameChars)
				&& !TooLong(r.ResidentOrigin, MaxNameChars)
				&& !TooLong(r.ResidentArrival, MaxNameChars)
				&& !TooLong(r.ResidentBoundZoneId, MaxNameChars);
			if (!common) return false;
			bool resident = r.ResidentId > 0 && r.ResidentName == op.ObjectName
				&& r.ResidentOrigin == (op.Origin ?? "")
				&& r.ResidentArrival == (op.Faction ?? "")
				&& r.ResidentArrivalTick >= 0L && r.ResidentBoundZoneId == op.ZoneId
				&& r.SourceProofId == LodgeSourceProof(op, r);
			bool body = r.ResidentId == 0 && r.ResidentName == null
				&& r.ResidentOrigin == null && r.ResidentArrival == null
				&& r.ResidentArrivalTick == 0L && r.ResidentBoundZoneId == op.ZoneId
				&& r.SourceProofId == null;
			if (!resident && !body) return false;
			bool noMarket = r.MarketSourcePrepared == KingdomLifecycleLodgeTerminalReceipt.MarketNone
				&& r.MarketSourceBodyObjectId == null && r.MarketSourceResidentId == 0
				&& r.MarketTier == 0 && r.MarketIntent == null
				&& r.MarketSourceProofId == null;
			bool marketProof = r.MarketSourceProofId == LodgeMarketSourceProof(op, r)
				|| r.MarketSourcePrepared == KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
					&& r.MarketSourceProofId == LegacyLodgeMarketSourceProof(op, r);
			bool market = resident && r.MarketSourcePrepared >= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
				&& r.MarketSourcePrepared <= KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
				&& ValidRootId(r.MarketSourceBodyObjectId)
				&& r.MarketSourceBodyObjectId != r.ObjectId && r.MarketSourceResidentId > 0
				&& r.MarketSourceResidentId != r.ResidentId
				&& r.MarketTier > 0 && r.MarketTier == op.PlunderRequested
				&& !string.IsNullOrEmpty(r.MarketIntent)
				&& !TooLong(r.MarketIntent, MaxTextChars)
				&& marketProof;
			if (!noMarket && !market || body && !noMarket) return false;
			if (r.State == KingdomLifecycleLodgeTerminalState.ResidentSourceProved)
				return resident && r.DeathCause == 0 && r.TerminalTick == 0L
					&& r.DeathProofId == null && op.Phase >= KingdomLifecyclePhase.DomainIntent;
			if (r.State == KingdomLifecycleLodgeTerminalState.BodyDeathProved)
				return body && r.DeathCause == 0 && r.TerminalTick >= op.CreatedTick
					&& r.DeathProofId == LodgeDeathProof(op, r)
					&& op.Phase == KingdomLifecyclePhase.DomainIntent;
			bool abandoning = r.State == KingdomLifecycleLodgeTerminalState.AbandonIntent;
			bool terminal = r.State == KingdomLifecycleLodgeTerminalState.Abandoned
				|| r.State == KingdomLifecycleLodgeTerminalState.AuthorityReleased;
			return (abandoning || terminal)
				&& (body ? r.DeathCause == 0 : r.DeathCause >= 1 && r.DeathCause <= 4)
				&& r.TerminalTick >= op.CreatedTick && r.DeathProofId == LodgeDeathProof(op, r)
				&& (abandoning ? op.Phase == KingdomLifecyclePhase.DomainIntent
					: op.Phase == KingdomLifecyclePhase.Terminal);
		}

		internal static bool TryObserveLodgeBodyDeath(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, string objectId, string blueprint, string zoneId, long tick)
		{
			if (!ExactOperationAuthority(book, op) || op.Action != KingdomLifecycleAction.Lodge
				|| op.Phase != KingdomLifecyclePhase.DomainIntent || tick < op.UpdatedTick
				|| objectId != op.ObjectId || blueprint != op.Blueprint || zoneId != op.ZoneId) return false;
			if (op.LodgeTerminal != null)
				return op.LodgeTerminal.State == KingdomLifecycleLodgeTerminalState.BodyDeathProved
					&& LodgeTerminalShape(op, false);
			KingdomLifecycleLodgeTerminalReceipt r = new KingdomLifecycleLodgeTerminalReceipt
			{
				OperationId = op.Id, ReceiptId = ChildId(op.Id, "lodge-terminal", 0),
				PlanHash = op.PlanHash, SettlementId = op.SettlementId, ObjectId = objectId,
				Blueprint = blueprint, ResidentBoundZoneId = zoneId,
				State = KingdomLifecycleLodgeTerminalState.BodyDeathProved, TerminalTick = tick
			};
			r.DeathProofId = LodgeDeathProof(op, r); op.LodgeTerminal = r; op.UpdatedTick = tick;
			return ExactOperationAuthority(book, op);
		}

		internal static bool TryFreezeLodgeResident(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, int residentId, string name, string origin, string arrived,
			long arrivedTick, string boundZoneId)
		{
			if (!ExactOperationAuthority(book, op) || op.Action != KingdomLifecycleAction.Lodge
				|| op.Phase != KingdomLifecyclePhase.DomainIntent || residentId <= 0
				|| name != op.ObjectName || (origin ?? "") != (op.Origin ?? "")
				|| (arrived ?? "") != (op.Faction ?? "") || arrivedTick < 0L
				|| boundZoneId != op.ZoneId) return false;
			if (op.LodgeTerminal != null) return LodgeTerminalShape(op, false)
				&& op.LodgeTerminal.State == KingdomLifecycleLodgeTerminalState.ResidentSourceProved
				&& op.LodgeTerminal.ResidentId == residentId
				&& op.LodgeTerminal.ResidentName == name
				&& op.LodgeTerminal.ResidentOrigin == (origin ?? "")
				&& op.LodgeTerminal.ResidentArrival == (arrived ?? "")
				&& op.LodgeTerminal.ResidentArrivalTick == arrivedTick
				&& op.LodgeTerminal.ResidentBoundZoneId == boundZoneId;
			KingdomLifecycleLodgeTerminalReceipt r = new KingdomLifecycleLodgeTerminalReceipt
			{
				OperationId = op.Id, ReceiptId = ChildId(op.Id, "lodge-terminal", 0),
				PlanHash = op.PlanHash, SettlementId = op.SettlementId, ObjectId = op.ObjectId,
				Blueprint = op.Blueprint, ResidentId = residentId, ResidentName = name,
				ResidentOrigin = origin ?? "", ResidentArrival = arrived ?? "",
				ResidentArrivalTick = arrivedTick, ResidentBoundZoneId = boundZoneId,
				State = KingdomLifecycleLodgeTerminalState.ResidentSourceProved
			};
			r.SourceProofId = LodgeSourceProof(op, r); op.LodgeTerminal = r;
			return ExactOperationAuthority(book, op);
		}

		internal static bool TryUpgradeV9LodgeTerminal(KingdomLifecycleOperation op)
		{
			KingdomLifecycleLodgeTerminalReceipt r = op?.LodgeTerminal;
			if (r == null) return true;
			string held = r.DeathProofId;
			if (held != null)
			{
				if (held != LodgeDeathProofV9(op, r)) return false;
				r.DeathProofId = LodgeDeathProof(op, r);
			}
			if (LodgeTerminalShape(op, false)) return true;
			r.DeathProofId = held;
			return false;
		}

		internal static bool TryFreezeNoLodgeMarketSource(KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (!ExactOperationAuthority(book, op) || op.Action != KingdomLifecycleAction.Lodge
				|| op.Phase != KingdomLifecyclePhase.DomainIntent || op.LodgeTerminal == null
				|| op.LodgeTerminal.State != KingdomLifecycleLodgeTerminalState.ResidentSourceProved)
				return false;
			return LodgeTerminalShape(op, false);
		}

		internal static bool TryFreezeLodgeMarketSource(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, string sourceBodyObjectId, int sourceResidentId,
			int marketTier, string intent)
		{
			if (!ExactOperationAuthority(book, op) || op.Action != KingdomLifecycleAction.Lodge
				|| op.Phase != KingdomLifecyclePhase.DomainIntent || op.LodgeTerminal == null
				|| op.LodgeTerminal.State != KingdomLifecycleLodgeTerminalState.ResidentSourceProved
				|| !ValidRootId(sourceBodyObjectId) || sourceBodyObjectId == op.ObjectId
				|| sourceResidentId <= 0 || sourceResidentId == op.LodgeTerminal.ResidentId
				|| marketTier <= 0 || marketTier != op.PlunderRequested
				|| string.IsNullOrEmpty(intent) || TooLong(intent, MaxTextChars)) return false;
			KingdomLifecycleLodgeTerminalReceipt r = op.LodgeTerminal;
			if (r.MarketSourcePrepared >= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared)
			{
				bool exact = r.MarketSourceBodyObjectId == sourceBodyObjectId
					&& r.MarketSourceResidentId == sourceResidentId && r.MarketTier == marketTier
					&& r.MarketIntent == intent && LodgeTerminalShape(op, false);
				if (exact && r.MarketSourcePrepared
					== KingdomLifecycleLodgeTerminalReceipt.MarketPrepared)
					r.MarketSourceProofId = LodgeMarketSourceProof(op, r);
				return exact && LodgeTerminalShape(op, false);
			}
			if (r.MarketSourcePrepared != KingdomLifecycleLodgeTerminalReceipt.MarketNone
				|| r.MarketSourceResidentId != 0 || r.MarketTier != 0 || r.MarketIntent != null
				|| r.MarketSourceProofId != null) return false;
			r.MarketSourcePrepared = 1; r.MarketSourceBodyObjectId = sourceBodyObjectId;
			r.MarketSourceResidentId = sourceResidentId; r.MarketTier = marketTier;
			r.MarketIntent = intent; r.MarketSourceProofId = LodgeMarketSourceProof(op, r);
			return ExactOperationAuthority(book, op);
		}

		internal static bool TryBeginLodgeAbandon(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, int exactMatches, int residentId, string name,
			string origin, string arrived, long arrivedTick, string boundZoneId,
			byte standing, byte cause, long tick)
		{
			if (!ExactOperationAuthority(book, op) || op.Phase != KingdomLifecyclePhase.DomainIntent
				|| op.LodgeTerminal == null || tick < op.UpdatedTick) return false;
			KingdomLifecycleLodgeTerminalReceipt r = op.LodgeTerminal;
			if (r.State == KingdomLifecycleLodgeTerminalState.AbandonIntent)
			{
				if (r.ResidentId == 0) return exactMatches == 0 && residentId == 0
					&& name == null && origin == null && arrived == null && arrivedTick == 0L
					&& boundZoneId == null && standing == 0 && cause == 0
					&& LodgeTerminalShape(op, false);
				return exactMatches == 1 && standing == 2 && cause == r.DeathCause
					&& residentId == r.ResidentId && name == r.ResidentName
					&& (origin ?? "") == r.ResidentOrigin
					&& (arrived ?? "") == r.ResidentArrival
					&& arrivedTick == r.ResidentArrivalTick
					&& boundZoneId == r.ResidentBoundZoneId && LodgeTerminalShape(op, false);
			}
			if (r.State == KingdomLifecycleLodgeTerminalState.BodyDeathProved)
			{
				if (residentId != 0 || exactMatches != 0 || name != null || origin != null
					|| arrived != null || arrivedTick != 0L || boundZoneId != null
					|| standing != 0 || cause != 0) return false;
			}
			else if (r.State == KingdomLifecycleLodgeTerminalState.ResidentSourceProved)
			{
				if (exactMatches != 1 || standing != 2 || cause < 1 || cause > 4
					|| residentId != r.ResidentId || name != r.ResidentName
					|| (origin ?? "") != r.ResidentOrigin || (arrived ?? "") != r.ResidentArrival
					|| arrivedTick != r.ResidentArrivalTick || boundZoneId != r.ResidentBoundZoneId)
					return false;
				r.DeathCause = cause; r.TerminalTick = tick;
				r.DeathProofId = LodgeDeathProof(op, r);
			}
			else return false;
			r.State = KingdomLifecycleLodgeTerminalState.AbandonIntent; op.UpdatedTick = tick;
			return ExactOperationAuthority(book, op);
		}
	}
}
