using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[Serializable]
	public class r_FounderBasin : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Found", "found a settlement", "r_FoundKingdom", null, 'f', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_FoundKingdom" && E.Actor != null && E.Actor.IsPlayer())
			{
				AttemptFounding(E.Actor);
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The rite. It is the same rite the second time: the same basin, the same eight drams of
		/// fresh water, the same refusals. What changes is where it is performed &mdash; poured on
		/// ground the realm does not hold and does not border, while the realm already stands, it
		/// founds a second city rather than a first; poured on ground a living village already
		/// answers to, it asks instead of taking (<see cref="AttemptVillageCharter"/>); poured on
		/// ground anything else already answers to, it refuses outright &mdash; this rite has
		/// never had a way to claim ground without asking, it simply used to skip asking.
		/// </summary>
		/// <param name="Actor">The founder. The zone they are standing in is the site.</param>
		public void AttemptFounding(GameObject Actor)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone site = Actor?.CurrentZone;
			string siteFaction = site?.GetZoneProperty("faction") ?? "";
			bool siteFactionIsVillage = !string.IsNullOrEmpty(siteFaction) && Factions.Get(siteFaction)?.GetIntProperty("Village") == 1;
			KingdomRules.GroundClaimVerdict groundVerdict = KingdomRules.JudgeGroundFaction(siteFaction, system.KingdomFactionName, siteFactionIsVillage);
			if (groundVerdict == KingdomRules.GroundClaimVerdict.ForeignVillage)
			{
				AttemptVillageCharter(system, siteFaction);
				return;
			}
			if (groundVerdict == KingdomRules.GroundClaimVerdict.ForeignOther)
			{
				Popup.Show("This ground already answers to someone else. Pouring here would not found anything; it would only spend the water.");
				return;
			}
			// Ground the realm that put the founder out still holds is not ground to found on.
			// Its city goes on without them; taking it back is the return path, not the rite.
			// Judged before the water is measured, so a refusal never costs a dram.
			if (site != null && system.ExiledRealmHolds(site.ZoneID))
			{
				Popup.Show("This ground is {{C|" + system.ExiledDisplayName + "}}'s, and it is not yours to pour on any more. Ask it to take you back, or walk until the ground answers to nobody.");
				return;
			}
			bool second = system.Founded;
			if (second)
			{
				// Judged before the water is measured, so a refusal never costs a dram.
				KingdomSettlement.SecondFoundingVerdict verdict = KingdomFounding.JudgeSite(system, site);
				if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed)
				{
					Popup.Show(KingdomSettlement.SecondFoundingRefusal(verdict, system.KingdomDisplayName));
					return;
				}
			}
			LiquidVolume liquidVolume = ParentObject.GetPart<LiquidVolume>();
			int drams = KingdomLiquids.HasFreshWater(liquidVolume) ? liquidVolume.Volume : 0;
			if (drams < KingdomRules.FoundingCostDrams)
			{
				int volume = (liquidVolume != null && liquidVolume.Volume > 0) ? liquidVolume.Volume : 0;
				string reason;
				if (volume > 0 && drams == 0)
				{
					reason = " It holds " + volume + " drams, but the liquid is not pure water.";
				}
				else
				{
					reason = " It holds " + drams + ".";
				}
				Popup.Show("The rite asks for {{C|" + KingdomRules.FoundingCostDrams + " drams}} of fresh water pooled in the basin." + reason);
				return;
			}
			string name = Popup.AskString(second ? "Name the second city." : "Name the settlement.", "", MaxLength: 30, ReturnNullForEscape: true);
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			if (second)
			{
				FoundSecondCity(system, site, liquidVolume, name);
				return;
			}
			if (Factions.Exists(name))
			{
				// A runtime faction is registered forever, so a name once used is used up - most
				// often the realm that put this founder out, which is still standing.
				Popup.Show("There is already a {{C|" + name + "}} in the world, and there can only be one of anything named. Nothing has been poured.");
				return;
			}
			KingdomLiquids.Drain(liquidVolume, KingdomRules.FoundingCostDrams);
			KingdomFounding.Found(name);
			KingdomFounding.ClaimZone(Actor.CurrentZone);
			bool isRuin = KingdomRules.IsRuinSite(system.FoundingTerrainBlueprint);
			string verb = isRuin ? "reclaimed" : "founded";
			string openingLine = isRuin
				? "You pour the first water over ground the world already built, and those who came drink among walls that stood before you."
				: "You pour the first water, and those gathered drink.";
			Popup.Show(openingLine + "\n\n{{C|" + name + "}} is " + verb + " on " + KingdomFounding.StyleGroundClause(system.Style) + ". Your thirst is theirs; their water is yours.\n\nLive and drink.");
		}

		/// <summary>
		/// The rite asks rather than takes here: <paramref name="VillageFactionName"/> already
		/// owns this ground, and nothing about that changes &mdash; not the zone's faction, not a
		/// single villager's allegiance, not one stone. What can change is standing, the same way
		/// it changes for any faction, sealed with the same water the founding rite spends. See
		/// <see cref="KingdomFounding.CharterVillage"/> for exactly what "chartered" means here,
		/// and why it stops short of a second city.
		/// </summary>
		/// <param name="System">The kingdom system.</param>
		/// <param name="VillageFactionName">The village's own faction name, read from the site's
		/// zone property before this was called.</param>
		private void AttemptVillageCharter(KingdomSystem System, string VillageFactionName)
		{
			Faction villageFaction = Factions.Get(VillageFactionName);
			string villageName = villageFaction?.DisplayName ?? VillageFactionName;
			int reputation = The.Game.PlayerReputation.Get(VillageFactionName);
			bool alreadyChartered = System.GetStanding(VillageFactionName) >= KingdomRules.VillageCharterSealedStanding;
			KingdomRules.VillageCharterVerdict verdict = KingdomRules.JudgeVillageCharter(System.Founded, alreadyChartered, reputation);
			if (verdict != KingdomRules.VillageCharterVerdict.Allowed)
			{
				Popup.Show(KingdomRules.VillageCharterRefusal(verdict, villageName));
				return;
			}
			LiquidVolume liquidVolume = ParentObject.GetPart<LiquidVolume>();
			int drams = KingdomLiquids.HasFreshWater(liquidVolume) ? liquidVolume.Volume : 0;
			if (drams < KingdomRules.FoundingCostDrams)
			{
				Popup.Show("Sealing a charter with {{C|" + villageName + "}} asks the same {{C|" + KingdomRules.FoundingCostDrams + " drams}} of fresh water the founding rite does. It holds " + drams + ".");
				return;
			}
			if (Popup.ShowYesNo("Ask {{C|" + villageName + "}} to stand with {{C|" + System.KingdomDisplayName + "}}? Their ground stays theirs; nothing here is founded, claimed, or taken — only water, and a word kept.") != DialogResult.Yes)
			{
				return;
			}
			KingdomLiquids.Drain(liquidVolume, KingdomRules.FoundingCostDrams);
			KingdomFounding.CharterVillage(System, VillageFactionName, villageName);
			Popup.Show("You pour, and they drink.\n\n{{C|" + villageName + "}} stands with {{C|" + System.KingdomDisplayName + "}} now — their own place, their own people, and a covenant between you.\n\nLive and drink.");
		}

		/// <summary>
		/// Commits the second city: its purpose, then the pour. The water is drawn only after the
		/// founding takes, so a refusal at the last moment leaves the basin as full as it was.
		/// </summary>
		private static void FoundSecondCity(KingdomSystem System, Zone Site, LiquidVolume Basin, string Name)
		{
			string vocation = AskVocation(Name);
			if (vocation == null)
			{
				return;
			}
			if (!KingdomFounding.FoundSecond(Name, vocation, Site))
			{
				Popup.Show("The ground will not take a founding. Nothing has been poured.");
				return;
			}
			KingdomLiquids.Drain(Basin, KingdomRules.FoundingCostDrams);
			bool isRuin = KingdomRules.IsRuinSite(System.FoundingTerrainBlueprint);
			string verb = isRuin ? "reclaimed" : "founded";
			string openingLine = isRuin
				? "You pour again, a long way from the first pouring, over ground the world already built, and those who walked out with you drink among walls that stood before them."
				: "You pour again, a long way from the first pouring, and those who walked out with you drink.";
			Popup.Show(openingLine + "\n\n{{C|" + Name + "}} is " + verb + " on " + KingdomFounding.StyleGroundClause(System.Style) + ", " + KingdomSettlement.VocationClause(vocation) + ".\n\n{{C|" + System.KingdomDisplayName + "}} keeps its other ground without you. Come back to it and it will tell you what it did.");
		}

		/// <summary>
		/// Asks what the city is for. Every site offers the same readings, including the neutral
		/// one: terrain narrows what a place is good at, never whether it may exist.
		/// </summary>
		/// <param name="Name">The city's name, for the menu title.</param>
		/// <returns>A vocation from <see cref="KingdomSettlement.Vocations"/>, or null if the
		/// founder walked away from the question.</returns>
		private static string AskVocation(string Name)
		{
			string[] vocations = KingdomSettlement.Vocations;
			string[] options = new string[vocations.Length];
			for (int i = 0; i < vocations.Length; i++)
			{
				options[i] = "{{C|" + vocations[i] + "}} — " + KingdomSettlement.VocationBlurb(vocations[i]);
			}
			int picked = Popup.PickOption(Title: "What is " + Name + " for?", Intro: "A city is founded for something. Say it now, and the people who come will know what they came for.", Options: options, AllowEscape: true);
			if (picked < 0 || picked >= vocations.Length)
			{
				return null;
			}
			return vocations[picked];
		}
	}
}
