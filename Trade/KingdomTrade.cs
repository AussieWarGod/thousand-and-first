using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomTrade
	{
		public static bool Enabled => XRL.UI.Options.GetOption("r_TAF_OptionTrade") != "No";

		public static bool StrikeDeal(KingdomSystem System, string DealKey, string FactionName, out string Failure)
		{
			Failure = null;
			if (!System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (!KingdomData.TryGetDeal(DealKey, out var deal))
			{
				Failure = "No such charter.";
				return false;
			}
			Faction faction = Factions.Get(FactionName);
			if (faction == null)
			{
				Failure = "No such faction.";
				return false;
			}
			if (System.GetStanding(FactionName) < deal.MinStanding)
			{
				Failure = faction.DisplayName + " will not treat with the kingdom yet (standing " + System.GetStanding(FactionName) + " of " + deal.MinStanding + " needed).";
				return false;
			}
			if (System.ActiveDealKeys.Count >= KingdomRules.MaxCharters)
			{
				Failure = "The kingdom already keeps as many charters as it can honor.";
				return false;
			}
			for (int i = 0; i < System.ActiveDealKeys.Count; i++)
			{
				if (System.ActiveDealKeys[i] == DealKey && System.ActiveDealFactions[i] == FactionName)
				{
					Failure = "That charter already stands.";
					return false;
				}
			}
			System.ActiveDealKeys.Add(DealKey);
			System.ActiveDealFactions.Add(FactionName);
			System.DealNextTicks.Add(The.Game.TimeTicks + deal.IntervalTicks);
			KingdomChronicle.Record(System, System.KingdomDisplayName + " struck " + XRL.Language.Grammar.A(KingdomRules.StripParenthetical(deal.DisplayName)) + " with " + Faction.GetFormattedName(FactionName), Accomplishment: true);
			MessageQueue.AddPlayerMessage("{{G|The charter is struck. Caravans of " + Faction.GetFormattedName(FactionName) + " will come.}}");
			KingdomLog.Log("trade: struck " + DealKey + " with " + FactionName + " next=" + System.DealNextTicks[System.DealNextTicks.Count - 1]);
			return true;
		}

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)
		{
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID) || System.ActiveDealKeys.Count == 0)
			{
				return;
			}
			KingdomSurvey survey = Shared ?? KingdomSurvey.Take(Z);
			long timeTicks = The.Game.TimeTicks;
			DespawnCaravans(Z);
			for (int i = 0; i < System.ActiveDealKeys.Count; i++)
			{
				if (timeTicks < System.DealNextTicks[i] || !KingdomData.TryGetDeal(System.ActiveDealKeys[i], out var deal))
				{
					continue;
				}
				int cycles = KingdomRules.BankedCycles(timeTicks, System.DealNextTicks[i], deal.IntervalTicks);
				int delivered = survey.Store(deal.IncomeDrams * cycles);
				SpawnCaravan(Z, deal.CaravanBlueprint);
				System.AdjustStanding(System.ActiveDealFactions[i], KingdomRules.DealTrickleStanding);
				string displayName = Faction.GetFormattedName(System.ActiveDealFactions[i]);
				KingdomChronicle.Record(System, ((cycles > 1) ? (cycles + " caravans of ") : "a caravan of ") + displayName + " came to " + System.KingdomDisplayName + " and delivered " + delivered + " drams under charter");
				System.Ledger.Delivered += delivered;
				System.Ledger.Note("{{G|" + ((cycles > 1) ? (cycles + " caravans of ") : "A caravan of ") + displayName + " came under charter: " + delivered + " drams" + ((delivered < deal.IncomeDrams * cycles) ? ", and the stores overflowed" : "") + ".}}");
				KingdomLog.Log("trade: caravan deal=" + deal.Key + " faction=" + System.ActiveDealFactions[i] + " delivered=" + delivered + "/" + deal.IncomeDrams);
				System.DealNextTicks[i] = timeTicks + deal.IntervalTicks;
				System.RecordDeed("the caravans that come to " + System.KingdomDisplayName);
			}
		}

		public static void SpawnCaravan(Zone Z, string Blueprint)
		{
			List<Cell> emptyCells = Z.GetEmptyCells((Cell c) => c.X == 0 || c.X == Z.Width - 1 || c.Y == 0 || c.Y == Z.Height - 1);
			if (emptyCells == null || emptyCells.Count == 0)
			{
				emptyCells = Z.GetEmptyCells();
			}
			if (emptyCells == null || emptyCells.Count == 0)
			{
				return;
			}
			Cell cell = emptyCells.GetRandomElement();
			GameObject caravan = GameObject.Create(Blueprint);
			if (caravan != null)
			{
				cell.AddObject(caravan);
				caravan.MakeActive();
				caravan.SetIntProperty("KingdomCaravan", 1);
				if (caravan.Brain != null)
				{
					caravan.Brain.Allegiance.Calm = true;
				}
			}
		}

		public static void DespawnCaravans(Zone Z)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCaravan") == 1)
				{
					list.Add(item);
				}
			}
			foreach (GameObject item in list)
			{
				item.Obliterate();
			}
		}
	}
}
