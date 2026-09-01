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

	public static partial class KingdomSocket
	{
		/// <summary>The Charter's "give a building a new look" action.</summary>
		public static void OpenRedress(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null)
			{
				return;
			}
			Zone zone = Founder.CurrentZone;
			Cell cell = Founder.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A building is re-dressed on the kingdom's own ground.");
				return;
			}
			List<GameObject> candidates = new List<GameObject>();
			Func<GameObject, bool> isBuilding = KingdomUpgrade.IsFunctionallyBuilt;
			CollectNearby(cell, candidates, isBuilding);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectNearby(adjacent, candidates, isBuilding);
			}
			if (candidates.Count == 0)
			{
				Popup.Show("Stand beside something " + KingdomPresentation.Rich(System.SeatName) + " stands behind to give it a new look.");
				return;
			}
			string[] options = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				options[i] = candidates[i].ShortDisplayName;
			}
			int picked = Popup.PickOption(Title: "Give a building a new look, at " + KingdomPresentation.Rich(System.SeatName), Options: options, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = candidates[picked];
			string key = target.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key))
			{
				key = target.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			}
			if (!KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry) || entry.Skins == null || entry.Skins.Count == 0)
			{
				Popup.Show("There is no look known for the " + target.ShortDisplayName + " besides its own.");
				return;
			}
			string[] skinOptions = new string[entry.Skins.Count];
			for (int i = 0; i < entry.Skins.Count; i++)
			{
				skinOptions[i] = KingdomDesignRules.DescribeSkinOption(entry.Skins[i], false);
			}
			int skinPicked = Popup.PickOption(Title: "Dress the " + target.ShortDisplayName + " as", Options: skinOptions, AllowEscape: true);
			if (skinPicked < 0)
			{
				return;
			}
			if (!Redress(System, zone, target, entry.Skins[skinPicked].Key, out string failure))
			{
				Popup.Show(failure);
			}
		}
	}
}
