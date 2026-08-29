namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		/// <summary>True only for one receipt mutation representable by one public CAS operation.</summary>
		public static bool ValidReceiptUpdate(KingdomConstructionInputReceipt Current,
			KingdomConstructionInputReceipt Next)
		{
			KingdomConstructionInputFault ignored;
			if (!TryValidate(Current, out ignored) || !TryValidate(Next, out ignored)
				|| Current.Schema != Next.Schema || Current.ReceiptId != Next.ReceiptId
				|| !FixedEquals(Current.PlanDigest, Next.PlanDigest)
				|| Current.Revision == int.MaxValue || Next.Revision != Current.Revision + 1
				|| Current.SourceCount != Next.SourceCount || Current.CargoCount != Next.CargoCount
				|| Current.ChildCount != Next.ChildCount) return false;

			int groups = 0;
			bool transaction = Current.TxPhase != Next.TxPhase;
			bool pause = Current.PauseStartedTick != Next.PauseStartedTick
				|| Current.PausedTicks != Next.PausedTicks;
			if (transaction) groups++;
			if (pause) groups++;
			int changedSource = -1, changedCargo = -1, changedChild = -1;
			for (int i = 0; i < Current.SourceCount; i++)
				if (!SameSourceMutable(Current.SourceAt(i), Next.SourceAt(i)))
				{ groups++; changedSource = changedSource < 0 ? i : -2; }
			for (int i = 0; i < Current.CargoCount; i++)
				if (!SameCargoMutable(Current.CargoAt(i), Next.CargoAt(i)))
				{ groups++; changedCargo = changedCargo < 0 ? i : -2; }
			for (int i = 0; i < Current.ChildCount; i++)
				if (!SameChildMutable(Current.ChildAt(i), Next.ChildAt(i)))
				{ groups++; changedChild = changedChild < 0 ? i : -2; }
			if (groups != 1) return false;
			if (transaction) return ParentReady(Current, Next.TxPhase);
			if (pause) return PauseUpdate(Current, Next);
			if (changedSource >= 0) return SourceUpdate(Current.SourceAt(changedSource),
				Next.SourceAt(changedSource));
			if (changedCargo >= 0) return CargoUpdate(Current.CargoAt(changedCargo),
				Next.CargoAt(changedCargo))
				&& (Next.CargoAt(changedCargo).Phase != KingdomConstructionInputCargoPhase.InFlight
					|| SourcesAre(Current, SourceDebited));
			if (changedChild >= 0)
			{
				KingdomConstructionInputChild old = Current.ChildAt(changedChild);
				KingdomConstructionInputChild next = Next.ChildAt(changedChild);
				return next.CentralPhase >= 0 && next.CentralRevision > old.CentralRevision;
			}
			return false;
		}

		public static bool IsTerminal(KingdomConstructionInputReceipt Receipt)
		{
			return Receipt != null && Terminal(Receipt.TxPhase);
		}

		private static bool SourceUpdate(KingdomConstructionInputSourceLine old,
			KingdomConstructionInputSourceLine next)
		{
			if (old.Phase == next.Phase) return SourceEvidenceAdvance(old, next, true);
			return old.RemainderObjectId == next.RemainderObjectId
				&& old.BeforeWitnessHash == next.BeforeWitnessHash
				&& old.AfterWitnessHash == next.AfterWitnessHash
				&& old.ProvedLost == next.ProvedLost && SourceTransition(old, next.Phase);
		}

		private static bool CargoUpdate(KingdomConstructionInputCargoLine old,
			KingdomConstructionInputCargoLine next)
		{
			if (old.Phase == next.Phase) return CargoEvidenceAdvance(old, next, true);
			return CargoTransition(old, next.Phase) && CargoEvidenceAdvance(old, next, false);
		}

		private static bool PauseUpdate(KingdomConstructionInputReceipt old,
			KingdomConstructionInputReceipt next)
		{
			if (!old.Paused)
				return next.Paused && next.PausedTicks == old.PausedTicks
					|| !next.Paused && next.PausedTicks > old.PausedTicks;
			return !next.Paused && next.PausedTicks >= old.PausedTicks
				&& next.PausedTicks - old.PausedTicks <= long.MaxValue - old.PauseStartedTick;
		}

		private static bool SameSourceMutable(KingdomConstructionInputSourceLine a,
			KingdomConstructionInputSourceLine b)
		{
			return a.Phase == b.Phase && a.RemainderObjectId == b.RemainderObjectId
				&& a.BeforeWitnessHash == b.BeforeWitnessHash
				&& a.AfterWitnessHash == b.AfterWitnessHash && a.ProvedLost == b.ProvedLost;
		}

		private static bool SameCargoMutable(KingdomConstructionInputCargoLine a,
			KingdomConstructionInputCargoLine b)
		{
			return a.ObjectId == b.ObjectId && a.Phase == b.Phase
				&& a.CustodyTopology == b.CustodyTopology && a.CustodyOwnerId == b.CustodyOwnerId
				&& a.CustodyZoneId == b.CustodyZoneId && a.CustodyX == b.CustodyX
				&& a.CustodyY == b.CustodyY && a.BeforeWitnessHash == b.BeforeWitnessHash
				&& a.AfterWitnessHash == b.AfterWitnessHash && a.Spent == b.Spent
				&& a.Lost == b.Lost;
		}

		private static bool SameChildMutable(KingdomConstructionInputChild a,
			KingdomConstructionInputChild b)
		{
			return a.CentralPhase == b.CentralPhase && a.CentralRevision == b.CentralRevision;
		}
	}
}
