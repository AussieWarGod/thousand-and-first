using System;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		public static bool TryUpdateSourceEvidence(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, int Ordinal, string RemainderObjectId,
			string BeforeWitnessHash, string AfterWitnessHash, int ProvedLost,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Ordinal < 0 || Ordinal >= Receipt.SourceCount)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			KingdomConstructionInputSourceLine old = Receipt.SourceAt(Ordinal);
			KingdomConstructionInputSourceLine next = old.WithEvidence(RemainderObjectId,
				BeforeWitnessHash, AfterWitnessHash, ProvedLost);
			if (ExpectedRevision == int.MaxValue || !SourceEvidenceAdvance(old, next, true))
				return Refuse(KingdomConstructionInputFault.Witness, out Fault);
			KingdomConstructionInputSourceLine[] rows = Receipt.CopySources(); rows[Ordinal] = next;
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1,
				Receipt.PauseStartedTick, Receipt.PausedTicks, rows, null, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		public static bool TryUpdateCargoEvidence(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, int Ordinal, string ObjectId,
			KingdomConstructionInputTopology CustodyTopology, string CustodyOwnerId,
			string CustodyZoneId, int CustodyX, int CustodyY, string BeforeWitnessHash,
			string AfterWitnessHash, int Spent, int Lost,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Ordinal < 0 || Ordinal >= Receipt.CargoCount)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			KingdomConstructionInputCargoLine old = Receipt.CargoAt(Ordinal);
			KingdomConstructionInputCargoLine next = old.WithEvidence(ObjectId, CustodyTopology,
				CustodyOwnerId, CustodyZoneId, CustodyX, CustodyY, BeforeWitnessHash,
				AfterWitnessHash, Spent, Lost);
			if (ExpectedRevision == int.MaxValue || !CargoEvidenceAdvance(old, next, true))
				return Refuse(KingdomConstructionInputFault.Witness, out Fault);
			KingdomConstructionInputCargoLine[] rows = Receipt.CopyCargo(); rows[Ordinal] = next;
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1,
				Receipt.PauseStartedTick, Receipt.PausedTicks, null, rows, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		public static bool TryTransitionCargoWithEvidence(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, int Ordinal, KingdomConstructionInputCargoPhase ExpectedPhase,
			KingdomConstructionInputCargoPhase NextPhase, string ObjectId,
			KingdomConstructionInputTopology CustodyTopology, string CustodyOwnerId,
			string CustodyZoneId, int CustodyX, int CustodyY, string BeforeWitnessHash,
			string AfterWitnessHash, int Spent, int Lost,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Ordinal < 0 || Ordinal >= Receipt.CargoCount)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			KingdomConstructionInputCargoLine old = Receipt.CargoAt(Ordinal);
			if (old.Phase != ExpectedPhase || !CargoTransition(old, NextPhase)
				|| (NextPhase == KingdomConstructionInputCargoPhase.InFlight
					&& !SourcesAre(Receipt, SourceDebited))
				|| ExpectedRevision == int.MaxValue)
				return Refuse(KingdomConstructionInputFault.Transition, out Fault);
			KingdomConstructionInputCargoLine next = old.WithEvidence(ObjectId, CustodyTopology,
				CustodyOwnerId, CustodyZoneId, CustodyX, CustodyY, BeforeWitnessHash,
				AfterWitnessHash, Spent, Lost).WithPhase(NextPhase);
			if (!CargoEvidenceAdvance(old, next, false))
				return Refuse(KingdomConstructionInputFault.Witness, out Fault);
			KingdomConstructionInputCargoLine[] rows = Receipt.CopyCargo(); rows[Ordinal] = next;
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1,
				Receipt.PauseStartedTick, Receipt.PausedTicks, null, rows, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		public static bool TryUpdateChildCentral(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, int Ordinal, int CentralPhase, long CentralRevision,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Ordinal < 0 || Ordinal >= Receipt.ChildCount)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			KingdomConstructionInputChild old = Receipt.ChildAt(Ordinal);
			if (ExpectedRevision == int.MaxValue || CentralPhase < 0
				|| CentralRevision <= old.CentralRevision)
				return Refuse(KingdomConstructionInputFault.Revision, out Fault);
			KingdomConstructionInputChild[] rows = Receipt.CopyChildren();
			rows[Ordinal] = old.WithCentral(CentralPhase, CentralRevision);
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1,
				Receipt.PauseStartedTick, Receipt.PausedTicks, null, null, rows);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		private static bool SourceEvidenceAdvance(KingdomConstructionInputSourceLine old,
			KingdomConstructionInputSourceLine next, bool exactlyOne)
		{
			int changed = 0;
			if (!AdvanceText(old.RemainderObjectId, next.RemainderObjectId, ref changed)
				|| !AdvanceText(old.BeforeWitnessHash, next.BeforeWitnessHash, ref changed)
				|| !AdvanceText(old.AfterWitnessHash, next.AfterWitnessHash, ref changed)
				|| next.ProvedLost < old.ProvedLost) return false;
			if (next.RemainderObjectId != old.RemainderObjectId
				&& old.Phase != KingdomConstructionInputSourcePhase.SplitIntent) return false;
			if (next.ProvedLost != old.ProvedLost)
			{
				if (old.Phase < KingdomConstructionInputSourcePhase.Debited
					&& old.Phase != KingdomConstructionInputSourcePhase.Quarantined) return false;
				changed++;
			}
			return exactlyOne ? changed == 1 : changed >= 0;
		}

		private static bool CargoEvidenceAdvance(KingdomConstructionInputCargoLine old,
			KingdomConstructionInputCargoLine next, bool exactlyOne)
		{
			int changed = 0;
			if (!AdvanceText(old.ObjectId, next.ObjectId, ref changed)
				|| !AdvanceText(old.BeforeWitnessHash, next.BeforeWitnessHash, ref changed)
				|| !AdvanceText(old.AfterWitnessHash, next.AfterWitnessHash, ref changed)
				|| next.Spent < old.Spent || next.Lost < old.Lost) return false;
			if (next.ObjectId != old.ObjectId
				&& ((old.Kind == KingdomConstructionInputKind.Water
					&& old.Phase != KingdomConstructionInputCargoPhase.CreateIntent)
					|| (old.Kind != KingdomConstructionInputKind.Water
						&& old.Phase != KingdomConstructionInputCargoPhase.Planned))) return false;
			bool custody = old.CustodyTopology != next.CustodyTopology
				|| old.CustodyOwnerId != next.CustodyOwnerId || old.CustodyZoneId != next.CustodyZoneId
				|| old.CustodyX != next.CustodyX || old.CustodyY != next.CustodyY;
			if (custody) changed++;
			if (next.Spent != old.Spent) changed++;
			if (next.Lost != old.Lost) changed++;
			return exactlyOne ? changed == 1 : changed >= 0;
		}

		private static bool AdvanceText(string old, string next, ref int changed)
		{
			old = old ?? string.Empty; next = next ?? string.Empty;
			if (old == next) return true;
			if (old.Length != 0 || next.Length == 0) return false;
			changed++; return true;
		}
	}
}
