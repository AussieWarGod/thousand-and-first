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
		public static bool TryOnExile(KingdomSystem System, long Now, string ExactRealmId,
			List<string> ExactSettlementIds, out long SettledTick, out string Failure)
		{
			SettledTick = -1L;
			Failure = null;
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Trade exile deferred because another synchronous trade lease is active.";
				return false;
			}
			using (lease)
			{
				if (System == null || System.TradeBook == null || Now < 0L)
				{
					Failure = "Trade exile requires a live system, book, and nonnegative tick.";
					return false;
				}
				KingdomTradeBook original = System.TradeBook;
				byte[] before;
				try { before = KingdomTradeCodec.EncodePayload(original); }
				catch
				{
					Failure = "Trade exile could not freeze the current bounded authority graph.";
					return false;
				}
				List<string> exact = new List<string>();
				if (ExactSettlementIds == null || ExactSettlementIds.Count < 1
					|| ExactSettlementIds.Count > KingdomTradeRules.MaxSettlementIds)
				{
					Failure = "Trade exile requires complete exact settlement topology within product cap.";
					return false;
				}
				for (int i = 0; i < ExactSettlementIds.Count; i++)
				{
					string id = ExactSettlementIds[i];
					if (!KingdomTradeRules.ValidId(id) || exact.Contains(id))
					{
						Failure = "Trade exile requires distinct exact settlement ids.";
						return false;
					}
					exact.Add(id);
				}
				exact.Sort(StringComparer.Ordinal);
				List<string> liveTopology;
				string topologyFailure;
				if (!System.TryRetainedSettlementIds(true, false,
					out liveTopology, out topologyFailure)
					|| !ExactSettlementTopology(liveTopology, exact))
				{
					Failure = "Trade exile could not reprove the complete exact settlement topology: "
						+ (topologyFailure ?? "topology differs");
					return false;
				}
				string currentRealm = System.CurrentRealmId;
				string currentSettlement = System.CurrentSettlementId;
				if (!string.Equals(currentRealm, ExactRealmId, StringComparison.Ordinal)
					|| !KingdomTradeRules.ValidId(currentSettlement) || !exact.Contains(currentSettlement))
				{
					Failure = "Trade exile could not prove the exact current realm and seated city topology.";
					return false;
				}
				KingdomTradeAuthoritySeal seal = KingdomTradeRules.CaptureAuthoritySeal(original,
					System.ClaimedZones, System.City?.ZoneIds);
				if (seal == null)
				{
					Failure = "Trade exile could not seal exact Core and Trade authority.";
					return false;
				}
				if (!TryCaptureExileCoreSeal(System, out TradeExileCoreSeal coreSeal,
					out string coreFailure))
				{
					Failure = "Trade exile could not freeze the complete Core topology: "
						+ (coreFailure ?? "unknown Core graph failure");
					return false;
				}
				KingdomTradeBook replacement;
				long closedTick;
				if (!KingdomTradeRules.TryPrepareExile(original, Now, ExactRealmId, exact,
					out replacement, out closedTick, out Failure)) return false;
				byte[] after;
				try { after = KingdomTradeCodec.EncodePayload(original); }
				catch
				{
					Failure = "Trade exile authority became unencodable during preflight.";
					return false;
				}
				List<string> finalTopology;
				if (!System.TryRetainedSettlementIds(true, false,
					out finalTopology, out topologyFailure)
					|| !ExactSettlementTopology(finalTopology, exact)
					|| !ReferenceEquals(System.TradeBook, original) || !ExactBytes(before, after)
					|| !string.Equals(System.CurrentRealmId, currentRealm, StringComparison.Ordinal)
					|| !string.Equals(System.CurrentSettlementId, currentSettlement,
						StringComparison.Ordinal)
					|| !ExactExileCoreSeal(System, coreSeal)
					|| !KingdomTradeRules.ExactAuthoritySeal(original,
						System.ClaimedZones, System.City?.ZoneIds, seal))
				{
					Failure = "Trade exile exact authority or topology changed during preflight.";
					return false;
				}
				if (!ReferenceEquals(replacement, original)) System.TradeBook = replacement;
				SettledTick = closedTick;
				return true;
			}
		}

		private static KingdomTradeBook EnsureBook(KingdomSystem System)
		{
			if (System == null) return null;
			if (System.TradeBook == null) System.TradeBook = new KingdomTradeBook();
			KingdomTradeBook book = System.TradeBook;
			KingdomTradeRules.Normalize(book);
			return KingdomTradeRules.BookUsable(book) ? book : null;
		}

		private static KingdomTradeOptionAction ApplyOption(KingdomTradeBook Book,
			bool IsEnabled, long Now)
		{
			KingdomTradeOptionAction action = KingdomTradeRules.ObserveOption(
				Book.OptionState, IsEnabled);
			KingdomTradeOptionState next = IsEnabled ? KingdomTradeOptionState.Enabled
				: KingdomTradeOptionState.Disabled;
			if (Book.OptionState != next)
			{
				if (Book.OptionEpoch == long.MaxValue)
				{
					KingdomTradeRules.QuarantineBook(Book, "trade option epoch overflow");
					return KingdomTradeOptionAction.None;
				}
				Book.OptionEpoch++;
			}
			Book.OptionState = next;
			Book.OptionObservedTick = Now < 0L ? 0L : Now;
			if (action == KingdomTradeOptionAction.EnableAndRestamp)
				Book.RestampPending = true;
			return action;
		}

		private static void RestampTradeClocks(KingdomTradeBook Book, long Now)
		{
			if (Book == null) return;
			for (int i = 0; i < Book.Charters.Count; i++)
			{
				KingdomTradeCharter charter = Book.Charters[i];
				if (charter == null || charter.Quarantined) continue;
				if (!KingdomData.TryGetDeal(charter.DealKey,
					out KingdomRules.DealEntry deal) || deal.IntervalTicks <= 0L)
				{
					charter.Quarantined = true;
					charter.Fault = "Charter content no longer resolves during enable restamp.";
					continue;
				}
				charter.NextTick = KingdomTradeRules.SaturatingAdd(Now, deal.IntervalTicks);
			}
			if (Book.Manifest != null
				&& Book.Manifest.Status == KingdomTradeManifestStatus.InFlight)
			{
				Book.Manifest.LoadedTick = Now;
				Book.Manifest.DeadlineTick = KingdomTradeRules.SaturatingAdd(Now,
					KingdomManifestRules.ManifestWindowTicks);
			}
			Book.RestampPending = false;
		}

	}
}
