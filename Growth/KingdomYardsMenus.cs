using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;


namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomYards
	{
		// --- The Charter's own menu ------------------------------------------------------------

		/// <summary>
		/// The founder's own way in: pick an eligible house standing on claimed ground, then pick
		/// a trade for it, or pick a worked house to let its trade go. Shaped like
		/// <c>KingdomUpgrade.ShowImprovements</c> and <c>KingdomCharterPart.DedicateVessel</c> on
		/// purpose &mdash; a founder who knows either of those menus already knows this one.
		/// </summary>
		public static void ShowYardTrades(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			Zone zone = The.Player?.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Yard trades are looked over on the kingdom's own ground.");
				return;
			}
			while (true)
			{
				List<GameObject> houses = ListHousesWithYards(zone);
				if (houses.Count == 0)
				{
					Popup.Show(KingdomYardRules.RefuseNoneStanding);
					return;
				}
				List<string> lines = new List<string>();
				for (int i = 0; i < houses.Count; i++)
				{
					string houseName = KingdomDesign.ReferenceFor(houses[i], houses[i].ShortDisplayName);
					string key = houses[i].GetStringProperty(YardKeyProperty);
					if (!string.IsNullOrEmpty(key) && TryGetSpec(key, out var existing))
					{
						lines.Add(houseName + " {{G|[" + existing.DisplayName + "]}} - let this trade go");
					}
					else
					{
						lines.Add(houseName + " - take up a yard trade");
					}
				}
				int picked = Popup.PickOption(Title: "Yard trades of " + KingdomPresentation.Rich(System.SeatName), Intro: "A small or middling house with room in its own yard can take up one trade. The household takes it up; letting one go is free and returns nothing.", Options: lines, AllowEscape: true);
				if (picked < 0 || picked >= houses.Count)
				{
					return;
				}
				GameObject chosen = houses[picked];
				string chosenKey = chosen.GetStringProperty(YardKeyProperty);
				if (!string.IsNullOrEmpty(chosenKey))
				{
					if (!TryReleaseTrade(System, chosen, out var releaseFailure))
					{
						Popup.Show(releaseFailure);
					}
					else
					{
						return;
					}
					continue;
				}
				List<string> workKeys = AllSpecKeys();
				List<string> workLines = new List<string>();
				for (int i = 0; i < workKeys.Count; i++)
				{
					if (TryGetSpec(workKeys[i], out var work))
					{
						workLines.Add(work.DisplayName + " {{K|(" + KingdomYardRules.ShadeSummary(work) + ")}}");
					}
				}
				if (workLines.Count == 0)
				{
					Popup.Show("The settlement knows no yard trade to take up yet.");
					return;
				}
				int workPicked = Popup.PickOption(Title: "A trade for " + KingdomDesign.ReferenceFor(chosen, chosen.ShortDisplayName), Options: workLines, AllowEscape: true);
				if (workPicked < 0 || workPicked >= workKeys.Count)
				{
					continue;
				}
				if (!TryTakeUpTrade(System, chosen, workKeys[workPicked], out var takeFailure))
				{
					Popup.Show(takeFailure);
				}
				else
				{
					return;
				}
			}
		}

		/// <summary>
		/// The Charter's single entry point for "Your works, and what they become": grow an
		/// existing work into its successor (<c>KingdomUpgrade.ShowImprovements</c>), or have a
		/// house take up a yard trade. Folded under one menu line rather than given a hotkey of
		/// its own because the Charter's 26 hotkeys are already every letter it has &mdash; a
		/// founder who already knows this line finds the second thing one step under it rather
		/// than nowhere at all.
		/// </summary>
		public static void ShowWorksAndTrades(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			int picked = Popup.PickOption(Title: "Your works, and what they become", Options: new List<string> { "Grow an existing work into its successor", "Have a house take up a yard trade" }, AllowEscape: true);
			if (picked == 0)
			{
				KingdomUpgrade.ShowImprovements(System);
			}
			else if (picked == 1)
			{
				ShowYardTrades(System);
			}
		}

		// --- The roll of settlers ---------------------------------------------------------------

		/// <summary>
		/// One line per household that has taken up a trade, standing on <paramref name="Z"/>.
		/// Meant to be appended to <c>KingdomReports.Roll</c> for the zone the Charter is read
		/// in: the roll of settlers already says who came and when, and a household's trade
		/// belongs beside that, not in a second report a founder has to remember exists.
		/// <para>
		/// Deliberately scoped to one zone rather than every claimed one. Forcing every claimed
		/// zone to load just to answer a report would build ground nobody is standing on outside
		/// the attended pass this whole mod resolves on; a second city's roll is read standing in
		/// the second city, the same way its Charter menu already only ever acts on
		/// <c>ParentObject.CurrentZone</c>.
		/// </para>
		/// </summary>
		public static List<string> RollLines(Zone Z)
		{
			List<string> lines = new List<string>();
			if (Z == null)
			{
				return lines;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				string key = item.GetStringProperty(YardKeyProperty);
				if (string.IsNullOrEmpty(key) || !TryGetSpec(key, out var work))
				{
					continue;
				}
				string houseName = KingdomDesign.ReferenceFor(item, item.ShortDisplayName);
				lines.Add(houseName + " has taken up " + work.Trade + ".");
			}
			return lines;
		}
	}
}
