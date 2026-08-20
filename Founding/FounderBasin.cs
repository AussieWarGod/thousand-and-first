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
		/// founds a second city rather than a first.
		/// </summary>
		/// <param name="Actor">The founder. The zone they are standing in is the site.</param>
		public void AttemptFounding(GameObject Actor)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone site = Actor?.CurrentZone;
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
			KingdomLiquids.Drain(liquidVolume, KingdomRules.FoundingCostDrams);
			KingdomFounding.Found(name);
			KingdomFounding.ClaimZone(Actor.CurrentZone);
			Popup.Show("You pour the first water, and those gathered drink.\n\n{{C|" + name + "}} is founded on " + KingdomFounding.StyleGroundClause(system.Style) + ". Your thirst is theirs; their water is yours.\n\nLive and drink.");
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
			Popup.Show("You pour again, a long way from the first pouring, and those who walked out with you drink.\n\n{{C|" + Name + "}} is founded on " + KingdomFounding.StyleGroundClause(System.Style) + ", " + KingdomSettlement.VocationClause(vocation) + ".\n\n{{C|" + System.KingdomDisplayName + "}} keeps its other ground without you. Come back to it and it will tell you what it did.");
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
