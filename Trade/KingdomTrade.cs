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
			ResolveManifest(System, Z, survey, timeTicks);
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
				// A charter may carry material as well as water. Absent means water alone, which is
				// every charter written before materials existed.
				KingdomMaterialTally carried = KingdomMaterials.DealMaterialsFor(deal.Key).Scaled(cycles * 100);
				if (!carried.IsEmpty())
				{
					KingdomMaterials.Deliver(System, Z, carried);
					KingdomChronicle.Record(System, "a charter of " + displayName + " set down " + carried.Describe() + " at " + System.KingdomDisplayName);
					System.Ledger.Note("{{G|The caravan also set down " + carried.Describe() + ".}}");
				}
				KingdomLog.Log("trade: caravan deal=" + deal.Key + " faction=" + System.ActiveDealFactions[i] + " delivered=" + delivered + "/" + deal.IncomeDrams);
				System.DealNextTicks[i] = timeTicks + deal.IntervalTicks;
				System.RecordDeed("the caravans that come to " + System.KingdomDisplayName);
			}
			KingdomCeremony.OnCaravanArrived(System, Z);
		}

		/// <summary>
		/// Settles the realm's one in-flight water manifest against this attended pass, if it
		/// has one. Delivery and lapse both fire only from here and from
		/// <see cref="ExpireManifestIfStale"/> &mdash; Addendum 8 clause 3's crystallise-at-
		/// awareness, which is the shape every deadline in this mod keeps. A manifest addressed to
		/// a city that never activates simply keeps waiting; nothing about it resolves on a
		/// background timer, and nothing about it is forgiven for having waited.
		/// </summary>
		/// <param name="System">The realm. <see cref="KingdomSystem.SeatName"/> names whichever
		/// city just activated, since <c>TrySeat</c> already ran before this is called.</param>
		/// <param name="Survey">The activated zone's survey, whose dedicated stores receive the
		/// delivery through the ordinary <see cref="KingdomSurvey.Store"/> path.</param>
		/// <param name="TimeTicks">Current tick.</param>
		private static void ResolveManifest(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (System.Manifest == null || ExpireManifestIfStale(System, Z, TimeTicks) != null || System.SeatName != System.Manifest.DestinationName)
			{
				return;
			}
			KingdomManifest manifest = System.Manifest;
			int delivered = Survey.Store(manifest.Drams);
			// Store() pours only what the casks can hold and returns that. Whatever did not fit
			// is poured out here, onto the ground, as a real pool the settlement can fetch back
			// once there is room for it.
			//
			// Not kept on the cart. A load that waits indefinitely for space is storage the
			// founder never had to build - uncapped, outside the dedicated-vessel limit, and
			// invisible - which would make cask racks and cisterns pointless and put the
			// settlement's water somewhere nobody can walk up to. The treasury is casks.
			int remainder = manifest.Drams - delivered;
			int spilled = 0;
			if (remainder > 0)
			{
				spilled = KingdomLiquids.PourOnGround(KingdomCommission.FindBuildCell(Z) ?? Z?.GetEmptyCells()?.GetRandomElement(), remainder);
			}
			System.Manifest = null;
			System.Ledger.Delivered += delivered;
			if (spilled > 0)
			{
				System.Ledger.Note("{{y|The carters came expecting room and found none: " + spilled
					+ " drams stand in a pool on the ground until the settlement can make space for them. Nobody is pleased about it.}}");
				KingdomChronicle.Record(System, "water sent from " + manifest.OriginName + " reached " + System.SeatName
					+ " to find the casks already full, and " + spilled + " drams were set down in the open");
			}
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
		public static KingdomManifest ExpireManifestIfStale(KingdomSystem System, Zone Here, long Now)
		{
			KingdomManifest manifest = System.Manifest;
			if (manifest == null || !KingdomManifestRules.ManifestExpired(Now, manifest.DeadlineTick))
			{
				return null;
			}
			if (!manifest.TurnedBack)
			{
				// The window closing is a fact about where the founder was, and absence must
				// never produce a debt: the carters give up on the road and start for home
				// rather than pouring sixty drams into the sand. The water is not lost, only
				// the errand. It turns back exactly once, so a load cannot bounce forever.
				//
				// The fresh window comes from KingdomRules.RestampDeadline, the one helper the
				// raid re-warn and the arrival queue also read: a full window from the moment of
				// witnessing, with no witness band at all, because a caravan's deadline is spent
				// the instant it passes and there is no version of "close enough" for a load that
				// is already standing in the sand. The sentences below stay this file's own.
				string turned = KingdomManifestRules.ManifestTurnedBackDeed(manifest.OriginName, manifest.DestinationName, manifest.Drams);
				string wasOrigin = manifest.OriginName;
				manifest.OriginName = manifest.DestinationName;
				manifest.DestinationName = wasOrigin;
				manifest.TurnedBack = true;
				manifest.LoadedTick = Now;
				manifest.DeadlineTick = KingdomRules.RestampDeadline(
					manifest.DeadlineTick, Now, KingdomManifestRules.ManifestWindowTicks, 0);
				System.Ledger.Note("{{y|" + turned + ".}}");
				KingdomChronicle.Record(System, turned);
				KingdomLog.Log("manifest: turned back " + manifest.Drams + " drams toward " + manifest.DestinationName);
				return null;
			}
			// Second window closed: the errand is over. The water is set down here rather than
			// destroyed - poured out on the ground the founder is standing on, where the
			// settlement can fetch it back. A load is never carried forever (that would be free,
			// invisible storage) and never evaporates (that would make being elsewhere a debt).
			// It ends as a puddle somebody can walk up to.
			System.Manifest = null;
			int setDown = KingdomLiquids.PourOnGround(KingdomCommission.FindBuildCell(Here) ?? Here?.GetEmptyCells()?.GetRandomElement(), manifest.Drams);
			string deed = KingdomManifestRules.ManifestLapseDeed(manifest.OriginName, manifest.DestinationName, manifest.Drams);
			System.Ledger.Note("{{y|" + deed + ((setDown > 0) ? (", and the " + setDown + " drams were set down here") : "") + ".}}");
			KingdomChronicle.Record(System, deed);
			KingdomLog.Log("manifest: lapsed " + manifest.Drams + " from " + manifest.OriginName + " to " + manifest.DestinationName + " set down=" + setDown);
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
