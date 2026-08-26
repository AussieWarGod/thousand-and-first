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
		private static bool StrikeDealCore(KingdomSystem System, string DealKey,
			string FactionName, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (!Enabled)
			{
				Failure = "Trade is disabled. Existing receipts remain recorded, but no new charter is struck.";
				return false;
			}
			if (!KingdomData.TryGetDeal(DealKey, out KingdomRules.DealEntry deal))
			{
				Failure = "No such charter.";
				return false;
			}
			Faction faction = Factions.GetIfExists(FactionName);
			if (faction == null)
			{
				Failure = "No such faction.";
				return false;
			}
			if (System.GetStanding(FactionName) < deal.MinStanding)
			{
				Failure = faction.DisplayName + " will not treat with the kingdom yet (standing "
					+ System.GetStanding(FactionName) + " of " + deal.MinStanding + " needed).";
				return false;
			}
			long now = The.Game.TimeTicks;
			KingdomTradeBook book = EnsureBook(System);
			if (book == null)
			{
				Failure = "The trade book uses an unknown or quarantined schema.";
				return false;
			}
			if (System.City == null
				|| !KingdomTradeRules.ValidId(System.City.SettlementId))
			{
				Failure = "The seated city has no exact identity; no charter was changed.";
				return false;
			}
			if (book.OpenOperation != null)
			{
				Failure = "Another trade receipt is still being reconciled.";
				return false;
			}
			ApplyOption(book, true, now);
			if (!KingdomTradeRules.BookUsable(book))
			{
				Failure = book.SchemaFault ?? "Trade option evidence is not authoritative.";
				return false;
			}
			int active = 0;
			for (int i = 0; i < book.Charters.Count; i++)
			{
				KingdomTradeCharter row = book.Charters[i];
				if (row == null || row.Quarantined) continue;
				active++;
			}
			if (active >= KingdomTradeRules.MaxCharters
				|| book.Charters.Count >= KingdomTradeRules.MaxCharters
				|| book.NextCharterSequence == long.MaxValue)
			{
				Failure = "The kingdom already keeps as many charters as it can honor.";
				return false;
			}
			long sequence = book.NextCharterSequence;
			string nextCharterId = KingdomTradeRules.CharterId(book.RealmId, sequence);
			if (!KingdomTradeRules.ValidId(nextCharterId))
			{
				Failure = "The charter identity could not be encoded exactly.";
				return false;
			}
			bool collision = false;
			for (int i = 0; i < book.Charters.Count; i++)
			{
				KingdomTradeCharter row = book.Charters[i];
				if (row == null) continue;
				if (!string.Equals(row.Id, nextCharterId, StringComparison.Ordinal)
					&& !(string.Equals(row.DealKey, DealKey, StringComparison.Ordinal)
						&& string.Equals(row.Faction, FactionName, StringComparison.Ordinal))) continue;
				collision = true;
				row.Quarantined = true;
				row.Fault = AppendFault(row.Fault, "new charter identity or schedule pair collided with preserved evidence");
			}
			if (collision)
			{
				Failure = "Charter evidence collides; every matching row was quarantined before mutation.";
				return false;
			}
			book.NextCharterSequence++;
			KingdomTradeCharter charter = new KingdomTradeCharter
			{
				Sequence = sequence,
				Id = nextCharterId,
				DealKey = DealKey,
				Faction = FactionName,
				CreatedTick = now,
				NextTick = KingdomTradeRules.SaturatingAdd(now, deal.IntervalTicks)
			};
			book.Charters.Add(charter);
			string charterId = charter.Id;
			long charterNext = charter.NextTick;
			TradeLiveFrame frame;
			if (!TryBindFrame(System, book, null, null, out frame)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				charter.Quarantined = true;
				charter.Fault = "The struck charter lost its exact authority before publication.";
				Failure = charter.Fault;
				return false;
			}
			CallbackWitness callback = CaptureCallbackWitness(frame);
			if (callback == null)
			{
				KingdomTradeRules.QuarantineBook(book, "Charter commit frame could not be frozen.");
				Failure = book.SchemaFault;
				return false;
			}
			KingdomGovernanceScope.Commit("strike trade charter");
			if (!ExactCallbackWitness(frame, callback)
				|| !ExactAuthority(frame, KingdomTradePhase.Invalid)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				KingdomTradeRules.QuarantineBook(System.TradeBook,
					"The charter commit callback changed its exact authority.");
				Failure = "The charter commit changed its authority and was quarantined.";
				return false;
			}
			string eventId = charter.Id + ":struck";
			callback = CaptureCallbackWitness(frame);
			if (callback == null)
			{
				KingdomTradeRules.QuarantineBook(book, "Charter chronicle frame could not be frozen.");
				Failure = book.SchemaFault;
				return false;
			}
			bool recorded = KingdomChronicle.RecordOnce(System, eventId,
				KingdomPresentation.Rich(System.KingdomDisplayName) + " struck "
				+ XRL.Language.Grammar.A(KingdomRules.StripParenthetical(deal.DisplayName))
				+ " with " + Faction.GetFormattedName(FactionName), Accomplishment: true);
			if (!recorded || !ExactCallbackWitness(frame, callback)
				|| !ExactAuthority(frame, KingdomTradePhase.Invalid)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				charter.Quarantined = true;
				charter.Fault = AppendFault(charter.Fault,
					"The charter chronicle callback was lost or changed exact authority.");
				Failure = charter.Fault;
				return false;
			}
			callback = CaptureCallbackWitness(frame);
			if (callback == null)
			{
				KingdomTradeRules.QuarantineBook(book, "Charter message frame could not be frozen.");
				Failure = book.SchemaFault;
				return false;
			}
			MessageQueue.AddPlayerMessage("{{G|The charter is struck. Caravans of "
				+ Faction.GetFormattedName(FactionName) + " will come.}}");
			if (!ExactCallbackWitness(frame, callback)
				|| !ExactAuthority(frame, KingdomTradePhase.Invalid)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				KingdomTradeRules.QuarantineBook(System.TradeBook,
					"The charter message callback changed its exact authority.");
				Failure = "The charter telling changed its authority and was quarantined.";
				return false;
			}
			KingdomLog.Log("trade: struck id=" + charter.Id + " next=" + charter.NextTick);
			return true;
		}

		private static bool ExactCharter(TradeLiveFrame Frame,
			KingdomTradeCharter Charter, string Id, string Deal, string Faction, long Created,
			long Next)
		{
			if (Frame == null || Charter == null || Frame.Charters == null
				|| Charter.Quarantined || !string.Equals(Charter.Id, Id, StringComparison.Ordinal)
				|| !string.Equals(Charter.DealKey, Deal, StringComparison.Ordinal)
				|| !string.Equals(Charter.Faction, Faction, StringComparison.Ordinal)
				|| Charter.CreatedTick != Created || Charter.NextTick != Next) return false;
			int identity = 0;
			int pair = 0;
			for (int i = 0; i < Frame.Charters.Count; i++)
			{
				KingdomTradeCharter row = Frame.Charters[i];
				if (ReferenceEquals(row, Charter)) identity++;
				if (row != null && string.Equals(row.Id, Id, StringComparison.Ordinal))
					identity += ReferenceEquals(row, Charter) ? 0 : 1;
				if (row != null && string.Equals(row.DealKey, Deal, StringComparison.Ordinal)
					&& string.Equals(row.Faction, Faction, StringComparison.Ordinal)) pair++;
			}
			return identity == 1 && pair == 1;
		}

	}
}
