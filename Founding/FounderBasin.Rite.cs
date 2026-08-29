using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_FounderBasin
	{
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
		public KingdomFoundingResult AttemptFounding(GameObject Actor)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone site = Actor?.CurrentZone;
			if (HasPendingRite)
			{
				KingdomFoundingKind kind = PendingKind;
				string pendingName = PendingName;
				string pendingVocation = PendingVocation;
				string pendingVillage = PendingVillageDisplayName;
				KingdomFoundingResult resumed = KingdomFoundingTransaction.Resume(this, Actor, site);
				if (resumed.Committed)
				{
					TransientCompletion = CompletionText(system, kind, pendingName,
						pendingVocation, pendingVillage);
				}
				else
				{
					ShowFailure(resumed);
				}
				return resumed;
			}
			string siteFaction = site?.GetZoneProperty("faction") ?? "";
			bool siteFactionIsVillage = !string.IsNullOrEmpty(siteFaction) && Factions.GetIfExists(siteFaction)?.GetIntProperty("Village") == 1;
			KingdomRules.GroundClaimVerdict groundVerdict = KingdomRules.JudgeGroundFaction(siteFaction, system.KingdomFactionName, siteFactionIsVillage);
			if (groundVerdict == KingdomRules.GroundClaimVerdict.ForeignVillage)
			{
				return AttemptVillageCharter(system, Actor, site, siteFaction);
			}
			if (groundVerdict == KingdomRules.GroundClaimVerdict.ForeignOther)
			{
				Popup.Show("This ground already answers to someone else. Pouring here would not found anything; it would only spend the water.");
				return Refused();
			}
			// Ground the realm that put the founder out still holds is not ground to found on.
			// Its city goes on without them; taking it back is the return path, not the rite.
			// Judged before the water is measured, so a refusal never costs a dram.
			if (site != null && system.ExiledRealmHolds(site.ZoneID))
			{
				Popup.Show("This ground is {{C|" +
					KingdomPresentation.Rich(system.ExiledDisplayName) +
					"}}'s, and it is not yours to pour on any more. Ask it to take you back, or walk until the ground answers to nobody.");
				return Refused();
			}
			bool second = system.Founded;
			if (second)
			{
				// Judged before the water is measured, so a refusal never costs a dram.
				KingdomSettlement.SecondFoundingVerdict verdict = KingdomFounding.JudgeSite(system, site);
				if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed)
				{
					Popup.Show(KingdomSettlement.SecondFoundingRefusal(verdict,
						KingdomPresentation.Rich(system.KingdomDisplayName)));
					return Refused();
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
				return Refused();
			}
			string name = Popup.AskString(second ? "Name the second city." : "Name the settlement.",
				"", MaxLength: KingdomPresentationRules.MaxRawCodeUnits,
				ReturnNullForEscape: true);
			if (name == null)
			{
				return Refused();
			}
			if (!KingdomPresentationRules.TryNormalizeName(name, out name,
				out string nameFailure))
			{
				Popup.Show(nameFailure);
				return Refused();
			}
			if (second)
			{
				return FoundSecondCity(system, Actor, site, name);
			}
			KingdomFoundingResult result = KingdomFoundingTransaction.BeginFirst(
				this, Actor, site, name);
			if (!result.Committed)
			{
				ShowFailure(result);
				return result;
			}
			bool isRuin = KingdomRules.IsRuinSite(system.FoundingTerrainBlueprint);
			string verb = isRuin ? "reclaimed" : "founded";
			string openingLine = isRuin
				? "You pour the first water over ground the world already built, and those who came drink among walls that stood before you."
				: "You pour the first water, and those gathered drink.";
			TransientCompletion = openingLine + "\n\n{{C|" +
				KingdomPresentation.Rich(name) + "}} is " + verb +
				" on " + KingdomFounding.StyleGroundClause(system.Style) +
				". Your thirst is theirs; their water is yours.\n\nLive and drink.";
			return result;
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
		private KingdomFoundingResult AttemptVillageCharter(KingdomSystem System,
			GameObject Actor, Zone Site, string VillageFactionName)
		{
			// GetIfExists, not Get: Factions.Get throws on an unknown name, and a zone's faction
			// property can name anything at all - including a faction from a mod that is no
			// longer installed. A stranger's zone must refuse the rite, not crash it.
			Faction villageFaction = Factions.GetIfExists(VillageFactionName);
			string villageName = villageFaction?.DisplayName ?? VillageFactionName;
			int reputation = The.Game.PlayerReputation.Get(VillageFactionName);
			bool alreadyChartered = System.GetRegardForRealm(VillageFactionName) >=
				KingdomRules.VillageCharterSealedStanding;
			KingdomRules.VillageCharterVerdict verdict = KingdomRules.JudgeVillageCharter(System.Founded, alreadyChartered, reputation);
			if (verdict != KingdomRules.VillageCharterVerdict.Allowed)
			{
				Popup.Show(KingdomRules.VillageCharterRefusal(verdict,
					KingdomPresentation.Rich(villageName)));
				return Refused();
			}
			LiquidVolume liquidVolume = ParentObject.GetPart<LiquidVolume>();
			int drams = KingdomLiquids.HasFreshWater(liquidVolume) ? liquidVolume.Volume : 0;
			if (drams < KingdomRules.FoundingCostDrams)
			{
				Popup.Show("Sealing a charter with {{C|" +
					KingdomPresentation.Rich(villageName) +
					"}} asks the same {{C|" + KingdomRules.FoundingCostDrams +
					" drams}} of fresh water the founding rite does. It holds " + drams + ".");
				return Refused();
			}
			long tick = The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
			string facts = KingdomFoundingTransaction.VillageCharterPreview(
				KingdomPresentation.Rich(villageName),
				System.GetRegardForRealm(VillageFactionName));
			string settlement = System.City?.SettlementId;
			string source = string.IsNullOrEmpty(settlement) ? null
				: KingdomLifecycleRules.ChildId(settlement,
					"civic-covenant-" + VillageFactionName + "-" + tick, 0);
			KingdomExperienceRuntime.TryPrepareCivicVoice(System,
				KingdomCivicVoiceFixture.VillageCovenant, 1, source, settlement, facts, tick,
				out KingdomCivicVoiceReceipt voice, out string rendering);
			string precedent = KingdomDecisionTagRules.CovenantScene(System.City?.AssentingMoot);
			if (!string.IsNullOrEmpty(precedent)) rendering += "\n\n" + precedent;
			if (Popup.ShowYesNo(rendering) != DialogResult.Yes)
			{
				return Refused();
			}
			KingdomFoundingResult result = KingdomFoundingTransaction.BeginVillageCharter(
				this, Actor, Site, VillageFactionName, villageName);
			if (!result.Committed)
			{
				ShowFailure(result);
				return result;
			}
			KingdomExperienceRuntime.TryPublishCivicVoice(System, voice);
			TransientCompletion = "You pour, and they drink.\n\n{{C|" +
				KingdomPresentation.Rich(villageName) +
				"}} stands with {{C|" + KingdomPresentation.Rich(System.KingdomDisplayName) +
				"}} now — their own place, their own people, and a covenant between you.\n\nLive and drink.";
			return result;
		}

		/// <summary>
		/// Commits the second city: its purpose, then the pour. The water is drawn only after the
		/// founding takes, so a refusal at the last moment leaves the basin as full as it was.
		/// </summary>
		private KingdomFoundingResult FoundSecondCity(KingdomSystem System, GameObject Actor,
			Zone Site, string Name)
		{
			string vocation = AskVocation(Name);
			if (vocation == null)
			{
				return Refused();
			}
			KingdomFoundingResult result = KingdomFoundingTransaction.BeginSecond(
				this, Actor, Site, Name, vocation);
			if (!result.Committed)
			{
				ShowFailure(result);
				return result;
			}
			bool isRuin = KingdomRules.IsRuinSite(System.FoundingTerrainBlueprint);
			string verb = isRuin ? "reclaimed" : "founded";
			string openingLine = isRuin
				? "You pour again, a long way from the first pouring, over ground the world already built, and those who walked out with you drink among walls that stood before them."
				: "You pour again, a long way from the first pouring, and those who walked out with you drink.";
			TransientCompletion = openingLine + "\n\n{{C|" +
				KingdomPresentation.Rich(Name) + "}} is " + verb +
				" on " + KingdomFounding.StyleGroundClause(System.Style) + ", " +
				KingdomSettlement.VocationClause(vocation) + ".\n\n{{C|" +
				KingdomPresentation.Rich(System.KingdomDisplayName) +
				"}} keeps its other ground without you. Come back to it and it will tell you what it did.";
			return result;
		}

	}
}
