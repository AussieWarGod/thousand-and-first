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
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			KingdomSurvey survey = Shared ?? KingdomSurvey.Take(Z);
			long timeTicks = The.Game.TimeTicks;
			// Independent of the foreign-charter loop below: a realm may hold a manifest with
			// zero external deals struck, and the deal loop's own early return must not skip it.
			ResolveManifest(System, survey, timeTicks);
			if (System.ActiveDealKeys.Count == 0)
			{
				return;
			}
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

		/// <summary>
		/// Settles the realm's one in-flight water manifest against this attended pass, if it
		/// has one. Delivery and lapse both fire only from here and from
		/// <see cref="ExpireManifestIfStale"/> &mdash; the same witnessed-only idiom as every
		/// other clock in this mod. A manifest addressed to a city that never activates simply
		/// keeps waiting; nothing about it resolves on a background timer.
		/// </summary>
		/// <param name="System">The realm. <see cref="KingdomSystem.SeatName"/> names whichever
		/// city just activated, since <c>TrySeat</c> already ran before this is called.</param>
		/// <param name="Survey">The activated zone's survey, whose dedicated stores receive the
		/// delivery through the ordinary <see cref="KingdomSurvey.Store"/> path.</param>
		/// <param name="TimeTicks">Current tick.</param>
		private static void ResolveManifest(KingdomSystem System, KingdomSurvey Survey, long TimeTicks)
		{
			if (System.Manifest == null || ExpireManifestIfStale(System, TimeTicks) != null || System.SeatName != System.Manifest.DestinationName)
			{
				return;
			}
			KingdomManifest manifest = System.Manifest;
			int delivered = Survey.Store(manifest.Drams);
			System.Manifest = null;
			System.Ledger.Delivered += delivered;
			System.Ledger.Note(KingdomManifestRules.ManifestArrivalNote(manifest.OriginName, delivered, manifest.Drams));
			KingdomChronicle.Record(System, KingdomManifestRules.ManifestArrivalDeed(manifest.OriginName, System.SeatName, delivered, manifest.Drams));
			KingdomLog.Log("manifest: delivered " + delivered + "/" + manifest.Drams + " from " + manifest.OriginName + " to " + System.SeatName);
			System.RecordDeed("the water that reached " + System.SeatName + " from " + manifest.OriginName);
		}

		/// <summary>
		/// Clears the realm's manifest if its arrival window has already closed, chronicling and
		/// ledgering the loss. Called from every witnessed moment that might notice it &mdash; an
		/// attended pass at either city via <see cref="ResolveManifest"/>, or the founder trying
		/// to load a new one from the Charter &mdash; and never from a background clock. Safe to
		/// call when there is no manifest, or when it has not lapsed.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Now">Current tick.</param>
		/// <returns>The manifest that lapsed, for a caller that wants to tell the founder
		/// directly; null if there was nothing to clear.</returns>
		public static KingdomManifest ExpireManifestIfStale(KingdomSystem System, long Now)
		{
			KingdomManifest manifest = System.Manifest;
			if (manifest == null || !KingdomManifestRules.ManifestExpired(Now, manifest.DeadlineTick))
			{
				return null;
			}
			System.Manifest = null;
			string deed = KingdomManifestRules.ManifestLapseDeed(manifest.OriginName, manifest.DestinationName, manifest.Drams);
			System.Ledger.Note("{{r|" + deed + ".}}");
			KingdomChronicle.Record(System, deed);
			KingdomLog.Log("manifest: lapsed " + manifest.Drams + " from " + manifest.OriginName + " to " + manifest.DestinationName);
			return manifest;
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
