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
		// --- Taking up and letting go ---------------------------------------------------------

		/// <summary>
		/// Places one registered yard work into a house's yard and marks the house as having
		/// taken up that trade. Refuses by name rather than by silence (STANDARDS 7b) whenever
		/// the ground, the house, or the key is not what it needs to be.
		/// </summary>
		public static bool TryTakeUpTrade(KingdomSystem System, GameObject Building, string YardKey, out string Failure)
		{
			Failure = null;
			if (System == null || Building == null)
			{
				Failure = "There is nothing here to work.";
				return false;
			}
			string houseName = KingdomDesign.ReferenceFor(Building, Building.ShortDisplayName);
			if (!TryReadHouse(Building, out var entry, out var spec, out var rect) || !KingdomYardRules.IsEligibleDesign(spec.Size, spec.Open, entry.Category))
			{
				Failure = KingdomYardRules.RefuseNotEligible(houseName);
				return false;
			}
			if (!string.IsNullOrEmpty(Building.GetStringProperty(YardKeyProperty)))
			{
				string existingKey = Building.GetStringProperty(YardKeyProperty);
				string existingName = TryGetSpec(existingKey, out var existingSpec) ? existingSpec.Trade : existingKey;
				Failure = KingdomYardRules.RefuseAlreadyWorking(houseName, existingName);
				return false;
			}
			if (!TryGetSpec(YardKey, out var work))
			{
				Failure = KingdomYardRules.RefuseUnknownWork(YardKey);
				return false;
			}
			Zone zone = Building.CurrentZone;
			if (zone == null || !TryFreeYardCell(zone, KingdomPlots.YardRects(Building), out var cell))
			{
				Failure = KingdomYardRules.RefuseNoRoom(houseName);
				return false;
			}
			GameObject placed = GameObject.Create(work.Blueprint);
			if (placed == null)
			{
				Failure = "The " + work.DisplayName + " could not be raised. (Its blueprint, \"" + work.Blueprint + "\", does not exist.)";
				return false;
			}
			placed.SetIntProperty(KingdomPlots.PlotPartProperty, 1);
			placed.SetIntProperty(YardWorkProperty, 1);
			placed.SetStringProperty(YardKeyProperty, work.Key);
			string plotId = Building.GetStringProperty(KingdomPlots.PlotIdProperty);
			if (!string.IsNullOrEmpty(plotId))
			{
				placed.SetStringProperty(KingdomPlots.PlotIdProperty, plotId);
			}
			cell.AddObject(placed);
			if (placed.CurrentCell != cell)
			{
				placed.Obliterate(null, Silent: true);
				Failure = "The " + work.DisplayName + " could not be set in the yard.";
				return false;
			}
			Building.SetStringProperty(YardKeyProperty, work.Key);
			KingdomGovernanceScope.Commit("take up yard trade");
			Building.RequirePart<r_KingdomYardTrade>();
			KingdomLog.Log("yards: " + houseName + " took up " + work.Key + " at " + cell.X + "," + cell.Y);
			KingdomChronicle.Record(System, KingdomYardRules.TakeUpLine(houseName, work));
			MessageQueue.AddPlayerMessage("{{G|" + houseName.Capitalize() + " takes up " + work.Trade + ".}}");
			return true;
		}

		/// <summary>
		/// Takes the yard work of a house down. Free, and returns nothing to the stores &mdash;
		/// the rule is stated in <see cref="KingdomYardRules.ReleaseLine"/> and enforced here by
		/// simply never crediting anything.
		/// </summary>
		public static bool TryReleaseTrade(KingdomSystem System, GameObject Building, out string Failure)
		{
			Failure = null;
			if (System == null || Building == null)
			{
				Failure = "There is nothing here to release.";
				return false;
			}
			string houseName = KingdomDesign.ReferenceFor(Building, Building.ShortDisplayName);
			string key = Building.GetStringProperty(YardKeyProperty);
			if (string.IsNullOrEmpty(key) || !TryGetSpec(key, out var work))
			{
				Failure = houseName + " has taken up no yard trade.";
				return false;
			}
			Zone zone = Building.CurrentZone;
			if (zone != null)
			{
				string plotId = Building.GetStringProperty(KingdomPlots.PlotIdProperty);
				foreach (GameObject item in zone.GetObjects())
				{
					if (item.GetIntProperty(YardWorkProperty) != 1 || item.GetStringProperty(YardKeyProperty) != key)
					{
						continue;
					}
					if (!string.IsNullOrEmpty(plotId) && item.GetStringProperty(KingdomPlots.PlotIdProperty) != plotId)
					{
						continue;
					}
					if (!item.Destroy(null, Silent: true))
					{
						Failure = "The yard work would not come down. Nothing was released.";
						return false;
					}
					break;
				}
			}
			Building.SetStringProperty(YardKeyProperty, null);
			KingdomGovernanceScope.Commit("release yard trade");
			KingdomLog.Log("yards: " + houseName + " let go of " + key);
			KingdomChronicle.Record(System, KingdomYardRules.ReleaseLine(houseName, work));
			MessageQueue.AddPlayerMessage("{{K|" + houseName.Capitalize() + " lets go of " + work.Trade + ". Nothing is recovered.}}");
			return true;
		}
	}
}
