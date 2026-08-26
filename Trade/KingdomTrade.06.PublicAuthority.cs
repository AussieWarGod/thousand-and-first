using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static bool FailDetachedAuthority(TradeLiveFrame Frame, string Fault)
		{
			KingdomSystem system = Frame?.System;
			KingdomTradeBook original = Frame?.Book;
			KingdomTradeBook official = system?.TradeBook;
			long now = 0L;
			try { now = The.Game.TimeTicks; } catch { }
			if (official == null && system != null)
			{
				official = original;
				system.TradeBook = official;
			}
			if (original != null)
			{
				KingdomTradeRules.RecordIncident(original, now, Fault, original);
				KingdomTradeRules.QuarantineBook(original, Fault);
			}
			if (official != null && !ReferenceEquals(official, original))
				KingdomTradeRules.RecordIncident(official, now, Fault, original);
			KingdomTradeRules.QuarantineBook(official, Fault);
			system?.SynchronizeLegacyManifestProjection();
			return false;
		}

		public static KingdomTradeManifestState CurrentManifest(KingdomSystem System)
		{
			TradeLease lease;
			if (!TryEnter(System, out lease)) return null;
			using (lease)
			{
				return KingdomTradeRules.SnapshotManifest(EnsureBook(System)?.Manifest);
			}
		}

		public static bool ResetAuthority(KingdomSystem System, out string Failure)
		{
			Failure = null;
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Trade authority is busy; reset changed nothing.";
				return false;
			}
			using (lease)
			{
				System.TradeBook = new KingdomTradeBook();
				return true;
			}
		}

		public static bool StrikeDeal(KingdomSystem System, string DealKey,
			string FactionName, out string Failure)
		{
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Failure = "Settlement simulation is paused; no new trade charter was struck.";
				return false;
			}
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Another trade callback is already in flight; no charter was changed.";
				return false;
			}
			using (lease)
			{
				return StrikeDealCore(System, DealKey, FactionName, out Failure);
			}
		}

	}
}
