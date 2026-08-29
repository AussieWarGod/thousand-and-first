namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		public static bool TryTransitionTransaction(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, KingdomConstructionInputTxPhase ExpectedPhase,
			KingdomConstructionInputTxPhase NextPhase,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Receipt.TxPhase != ExpectedPhase || !Defined(NextPhase)
				|| !ParentReady(Receipt, NextPhase) || ExpectedRevision == int.MaxValue)
				return Refuse(KingdomConstructionInputFault.Transition, out Fault);
			Updated = Receipt.Copy(NextPhase, ExpectedRevision + 1,
				Receipt.PauseStartedTick, Receipt.PausedTicks, null, null, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		public static bool TryTransitionSource(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, int Ordinal, KingdomConstructionInputSourcePhase ExpectedPhase,
			KingdomConstructionInputSourcePhase NextPhase,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Ordinal < 0 || Ordinal >= Receipt.SourceCount)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			KingdomConstructionInputSourceLine line = Receipt.SourceAt(Ordinal);
			if (line.Phase != ExpectedPhase || !SourceTransition(line, NextPhase)
				|| ExpectedRevision == int.MaxValue)
				return Refuse(KingdomConstructionInputFault.Transition, out Fault);
			KingdomConstructionInputSourceLine[] rows = Receipt.CopySources();
			rows[Ordinal] = line.WithPhase(NextPhase);
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1,
				Receipt.PauseStartedTick, Receipt.PausedTicks, rows, null, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		public static bool TryTransitionCargo(KingdomConstructionInputReceipt Receipt,
			int ExpectedRevision, int Ordinal, KingdomConstructionInputCargoPhase ExpectedPhase,
			KingdomConstructionInputCargoPhase NextPhase,
			out KingdomConstructionInputReceipt Updated,
			out KingdomConstructionInputFault Fault)
		{
			Updated = null;
			if (!Expected(Receipt, ExpectedRevision, out Fault)) return false;
			if (Ordinal < 0 || Ordinal >= Receipt.CargoCount)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			KingdomConstructionInputCargoLine line = Receipt.CargoAt(Ordinal);
			if (line.Phase != ExpectedPhase || !CargoTransition(line, NextPhase)
				|| (NextPhase == KingdomConstructionInputCargoPhase.InFlight
					&& !SourcesAre(Receipt, SourceDebited))
				|| ExpectedRevision == int.MaxValue)
				return Refuse(KingdomConstructionInputFault.Transition, out Fault);
			KingdomConstructionInputCargoLine[] rows = Receipt.CopyCargo();
			rows[Ordinal] = line.WithPhase(NextPhase);
			Updated = Receipt.Copy(Receipt.TxPhase, ExpectedRevision + 1,
				Receipt.PauseStartedTick, Receipt.PausedTicks, null, rows, null);
			if (!TryValidate(Updated, out Fault)) { Updated = null; return false; }
			return true;
		}

		private static bool Expected(KingdomConstructionInputReceipt receipt, int revision,
			out KingdomConstructionInputFault fault)
		{
			if (receipt == null) return Refuse(KingdomConstructionInputFault.Null, out fault);
			if (!TryValidate(receipt, out fault)) return false;
			if (revision < 0 || receipt.Revision != revision)
				return Refuse(KingdomConstructionInputFault.Revision, out fault);
			fault = KingdomConstructionInputFault.None;
			return true;
		}

		private static bool SourceTransition(KingdomConstructionInputSourceLine line,
			KingdomConstructionInputSourcePhase next)
		{
			if (!Defined(next) || line.Phase == next) return false;
			if (next == KingdomConstructionInputSourcePhase.Quarantined)
				return line.Phase != KingdomConstructionInputSourcePhase.Restored
					&& line.Phase != KingdomConstructionInputSourcePhase.Spent
					&& line.Phase != KingdomConstructionInputSourcePhase.Compensated
					&& line.Phase != KingdomConstructionInputSourcePhase.Quarantined;
			switch (line.Phase)
			{
			case KingdomConstructionInputSourcePhase.Reserved:
				return next == (PartialMaterial(line)
					? KingdomConstructionInputSourcePhase.SplitIntent
					: KingdomConstructionInputSourcePhase.TransferIntent);
			case KingdomConstructionInputSourcePhase.SplitIntent:
				return next == KingdomConstructionInputSourcePhase.SplitProved
					|| next == KingdomConstructionInputSourcePhase.RestoreIntent;
			case KingdomConstructionInputSourcePhase.SplitProved:
				return next == KingdomConstructionInputSourcePhase.TransferIntent
					|| next == KingdomConstructionInputSourcePhase.RestoreIntent;
			case KingdomConstructionInputSourcePhase.TransferIntent:
				return next == KingdomConstructionInputSourcePhase.Debited
					|| next == KingdomConstructionInputSourcePhase.RestoreIntent;
			case KingdomConstructionInputSourcePhase.RestoreIntent:
				return next == KingdomConstructionInputSourcePhase.Restored;
			case KingdomConstructionInputSourcePhase.Debited:
				return next == KingdomConstructionInputSourcePhase.Spent
					|| next == KingdomConstructionInputSourcePhase.CompensationIntent;
			case KingdomConstructionInputSourcePhase.CompensationIntent:
				return next == KingdomConstructionInputSourcePhase.Compensated;
			default:
				return false;
			}
		}

		private static bool CargoTransition(KingdomConstructionInputCargoLine line,
			KingdomConstructionInputCargoPhase next)
		{
			if (!Defined(next) || line.Phase == next) return false;
			if (next == KingdomConstructionInputCargoPhase.Quarantined)
				return line.Phase != KingdomConstructionInputCargoPhase.Released
					&& line.Phase != KingdomConstructionInputCargoPhase.Spent
					&& line.Phase != KingdomConstructionInputCargoPhase.Compensated
					&& line.Phase != KingdomConstructionInputCargoPhase.Quarantined;
			switch (line.Phase)
			{
			case KingdomConstructionInputCargoPhase.Planned:
				return next == (line.Kind == KingdomConstructionInputKind.Water
					? KingdomConstructionInputCargoPhase.CreateIntent
					: KingdomConstructionInputCargoPhase.AtSource);
			case KingdomConstructionInputCargoPhase.CreateIntent:
				return next == KingdomConstructionInputCargoPhase.AtSource
					|| next == KingdomConstructionInputCargoPhase.ReleaseIntent;
			case KingdomConstructionInputCargoPhase.AtSource:
				return next == KingdomConstructionInputCargoPhase.PickupIntent
					|| next == KingdomConstructionInputCargoPhase.ReleaseIntent;
			case KingdomConstructionInputCargoPhase.PickupIntent:
				return next == KingdomConstructionInputCargoPhase.InFlight
					|| next == KingdomConstructionInputCargoPhase.ReleaseIntent;
			case KingdomConstructionInputCargoPhase.InFlight:
				return next == KingdomConstructionInputCargoPhase.Landed
					|| next == KingdomConstructionInputCargoPhase.CompensationIntent;
			case KingdomConstructionInputCargoPhase.Landed:
				return next == KingdomConstructionInputCargoPhase.DebitIntent
					|| next == KingdomConstructionInputCargoPhase.CompensationIntent;
			case KingdomConstructionInputCargoPhase.DebitIntent:
				return next == KingdomConstructionInputCargoPhase.Spent
					|| next == KingdomConstructionInputCargoPhase.CompensationIntent;
			case KingdomConstructionInputCargoPhase.ReleaseIntent:
				return next == KingdomConstructionInputCargoPhase.Released;
			case KingdomConstructionInputCargoPhase.CompensationIntent:
				return next == KingdomConstructionInputCargoPhase.Compensated;
			default:
				return false;
			}
		}

		private static bool PartialMaterial(KingdomConstructionInputSourceLine line)
		{
			return line.Kind != KingdomConstructionInputKind.Water && line.ResidualAfter > 0;
		}
	}
}
