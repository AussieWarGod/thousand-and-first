using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static int DueCharterIndex(KingdomTradeBook Book, long Now)
		{
			if (Book == null || Book.OpenOperation != null || Book.Charters == null) return -1;
			int chosen = -1;
			for (int i = 0; i < Book.Charters.Count; i++)
			{
				KingdomTradeCharter row = Book.Charters[i];
				if (row == null || row.Quarantined || row.NextTick > Now) continue;
				if (chosen < 0 || row.NextTick < Book.Charters[chosen].NextTick
					|| (row.NextTick == Book.Charters[chosen].NextTick
						&& string.CompareOrdinal(row.Id, Book.Charters[chosen].Id) < 0)) chosen = i;
			}
			return chosen;
		}

		public static int ConservedManifestWater(int SourceBefore, int ProvedDebited,
			int EscrowBefore, int ProvedDelivered, int Retained)
		{
			long value = (long)SourceBefore - ProvedDebited + EscrowBefore
				- ProvedDelivered + Retained;
			if (value <= 0L) return 0;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}

		public static bool TryReconcileEscrow(int Before, int Debit, int Current,
			out int After, out bool Apply)
		{
			After = 0;
			Apply = false;
			if (Before < 0 || Debit < 0 || Debit > Before || Current < 0) return false;
			After = Before - Debit;
			if (Current == Before) { Apply = true; return true; }
			return Current == After;
		}

		public static bool TryReconcileRetained(long Before, long Delta, long Current,
			out long After, out bool Apply)
		{
			After = 0L;
			Apply = false;
			if (Before < 0L || Delta < 0L || Current < 0L
				|| Delta > long.MaxValue - Before) return false;
			After = Before + Delta;
			if (Current == Before) { Apply = true; return true; }
			return Current == After;
		}

		public static bool TryAddEscrow(long Left, long Right, out long Result)
		{
			Result = 0L;
			if (Left < 0L || Right < 0L || Right > long.MaxValue - Left) return false;
			Result = Left + Right;
			return true;
		}

		private static bool ValidAccountingEvidence(KingdomTradeOperation Operation)
		{
			if (Operation == null || Operation.ManifestEscrowBefore < 0
				|| Operation.ManifestEscrowDebit < 0
				|| Operation.ManifestEscrowDebit > Operation.ManifestEscrowBefore
				|| Operation.ManifestEscrowAfter
					!= Operation.ManifestEscrowBefore - Operation.ManifestEscrowDebit
				|| Operation.RetainedBefore < 0L || Operation.RetainedDelta < 0L
				|| Operation.RetainedDelta > long.MaxValue - Operation.RetainedBefore
				|| Operation.RetainedAfter != Operation.RetainedBefore + Operation.RetainedDelta)
				return false;
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.ManifestDelivery:
				return Operation.ManifestEscrowBefore == Operation.RequestedWater
					&& (Operation.ManifestEscrowState == KingdomTradePhysicalState.Prepared
						? Operation.ManifestEscrowDebit == 0
						: Operation.ManifestEscrowDebit == Operation.ProvedWater)
					&& Operation.ManifestEscrowState != KingdomTradePhysicalState.None
					&& Operation.RetainedBefore == 0L && Operation.RetainedDelta == 0L
					&& Operation.RetainedAfter == 0L
					&& Operation.RetainedState == KingdomTradePhysicalState.None;
			case KingdomTradeOperationKind.ManifestLapse:
				return Operation.RetainedDelta == Operation.RequestedWater
					&& Operation.RetainedState != KingdomTradePhysicalState.None
					&& Operation.ManifestEscrowBefore == 0
					&& Operation.ManifestEscrowDebit == 0
					&& Operation.ManifestEscrowAfter == 0
					&& Operation.ManifestEscrowState == KingdomTradePhysicalState.None;
			case KingdomTradeOperationKind.PolityConsignmentDelivery:
				bool retained = Operation.Phase == KingdomTradePhase.Quarantined &&
					(Operation.ProvedWater == 0 ? Operation.RetainedBefore == 0L &&
						Operation.RetainedDelta == 0L && Operation.RetainedAfter == 0L &&
						Operation.RetainedState == KingdomTradePhysicalState.None :
						Operation.RetainedDelta == Operation.ProvedWater &&
						Operation.RetainedState == KingdomTradePhysicalState.Proved);
				bool landed = Operation.Phase != KingdomTradePhase.Quarantined &&
					Operation.RetainedBefore == 0L && Operation.RetainedDelta == 0L &&
					Operation.RetainedAfter == 0L &&
					Operation.RetainedState == KingdomTradePhysicalState.None;
				return (retained || landed) && Operation.ManifestEscrowBefore == 0 &&
					Operation.ManifestEscrowDebit == 0 && Operation.ManifestEscrowAfter == 0 &&
					Operation.ManifestEscrowState == KingdomTradePhysicalState.None;
			default:
				return Operation.ManifestEscrowBefore == 0
					&& Operation.ManifestEscrowDebit == 0
					&& Operation.ManifestEscrowAfter == 0
					&& Operation.ManifestEscrowState == KingdomTradePhysicalState.None
					&& Operation.RetainedBefore == 0L && Operation.RetainedDelta == 0L
					&& Operation.RetainedAfter == 0L
					&& Operation.RetainedState == KingdomTradePhysicalState.None;
			}
		}

		/// <summary>Strict repair. Malformed authority is quarantined, never guessed into validity.</summary>
	}
}
