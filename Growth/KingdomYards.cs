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

	/// <summary>
	/// The engine-coupled half of yard trades (see <see cref="KingdomYardRules"/> for the
	/// geometry, the parsing, and the prose): the registry of authored trades, finding real free
	/// ground in a real house's yard, and raising or striking the one object that stands for a
	/// household's trade.
	/// <para>
	/// A yard work is never itself a plot. It is furniture the way a shop's own furnishings are:
	/// tagged <c>KingdomPlots.PlotPartProperty</c> so the rest of the plot bookkeeping treats it
	/// exactly like anything <c>KingdomPlots.Furnish</c> rolled, and tied to its house by
	/// <see cref="YardKeyProperty"/> rather than by a chain or a successor, because taking one up
	/// is a household's own decision and never a construction the settlement schedules.
	/// </para>
	/// </summary>
	public static class KingdomYards
	{
		// --- Registry ----------------------------------------------------------------------

		// Keyed the same way KingdomPlots keys its own plot specs (STANDARDS 6): a later file
		// re-using a Key owns that trade's whole spec, including retheming its blueprint or its
		// prose. Order is kept alongside the map so a founder's picker lists trades in the order
		// they were authored rather than in hash order.
		private static readonly Dictionary<string, KingdomYardRules.YardWorkSpec> Specs = new Dictionary<string, KingdomYardRules.YardWorkSpec>();

		private static readonly List<string> SpecOrder = new List<string>();

		/// <summary>Forgets every registered yard work. Called by the registry loader before it
		/// re-reads the XML streams.</summary>
		public static void ClearSpecs()
		{
			Specs.Clear();
			SpecOrder.Clear();
		}

		/// <summary>
		/// Registers one <c>&lt;yardwork&gt;</c> entry as the registry parses it. Call once per
		/// element that parsed successfully, with the raw attribute strings.
		/// </summary>
		public static void RegisterSpec(string Key, string DisplayName, string Blueprint, string Trade, string Shades, string Goods)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomYardRules.TryParseYardWorkAttributes(Key, DisplayName, Blueprint, Trade, Shades, Goods, out var spec, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomYardWorks: " + error);
				if (Specs.Remove(Key))
				{
					SpecOrder.Remove(Key);
				}
				return;
			}
			if (!Specs.ContainsKey(Key))
			{
				SpecOrder.Add(Key);
			}
			Specs[Key] = spec;
		}

		/// <summary>The yard work a key was registered with, if any.</summary>
		public static bool TryGetSpec(string Key, out KingdomYardRules.YardWorkSpec Spec)
		{
			Spec = null;
			return !string.IsNullOrEmpty(Key) && Specs.TryGetValue(Key, out Spec) && Spec != null;
		}

		/// <summary>Every registered yard work, in authored order. A copy, so a caller may not
		/// perturb the registry by holding it.</summary>
		public static List<string> AllSpecKeys()
		{
			return new List<string>(SpecOrder);
		}

		// --- Properties ----------------------------------------------------------------------

		/// <summary>On the finished house: the registered key of the trade it has taken up, or
		/// absent for a house that never has.</summary>
		public const string YardKeyProperty = "KingdomYardKey";

		/// <summary>On the object standing in the yard: marks it as a yard work rather than an
		/// ordinary furnishing, so it can be found again to be struck.</summary>
		public const string YardWorkProperty = "KingdomYardWork";

		// --- Reading a house ------------------------------------------------------------------

		/// <summary>
		/// Everything a finished plot building needs to say about itself before a yard trade can
		/// be asked of it: its catalogue entry, its plot spec, and the rect it was stamped with.
		/// </summary>
		/// <returns>False for anything that is not a finished plot building at all &mdash; a
		/// single-cell design, a scaffold still under way, or an object the settlement never
		/// built.</returns>
		public static bool TryReadHouse(GameObject Building, out KingdomRules.BuildEntry Entry, out KingdomPlotRules.PlotSpec Spec, out KingdomPlotRules.PlotRect Rect)
		{
			Entry = null;
			Spec = null;
			Rect = default(KingdomPlotRules.PlotRect);
			if (Building == null || Building.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
			{
				return false;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out Entry) || !KingdomPlots.TryGetSpec(key, out Spec))
			{
				return false;
			}
			return KingdomPlots.TryReadRect(Building, out Rect);
		}

		/// <summary>
		/// The first free cell of a house's yard, scanned in the same row-major order
		/// <c>KingdomPlots.Furnish</c> fills furniture in, so the cell a yard work lands in is
		/// never one furniture could still roll into later &mdash; there is no later roll, but the
		/// shared order keeps the two passes legible as the same idea.
		/// </summary>
		public static bool TryFreeYardCell(Zone Z, KingdomPlotRules.PlotRect Interior, out Cell Free)
		{
			Free = null;
			if (Z == null)
			{
				return false;
			}
			for (int y = Interior.Y1; y <= Interior.Y2; y++)
			{
				for (int x = Interior.X1; x <= Interior.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell != null && cell.IsEmpty() && cell.IsPassable())
					{
						Free = cell;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>The first free cell of a house's yard, band by band in the order
		/// <c>KingdomPlotRules.YardBands</c> returns them, so a trade always lands on the same side
		/// of the same house. A tier that fills its plot has no yard outside it, and
		/// <c>KingdomPlots.YardRects</c> hands back its interior instead -- which is the ground yard
		/// trades used before footprints existed, so nothing standing today moves.</summary>
		public static bool TryFreeYardCell(Zone Z, List<KingdomPlotRules.PlotRect> Bands, out Cell Free)
		{
			Free = null;
			if (Bands == null)
			{
				return false;
			}
			for (int i = 0; i < Bands.Count; i++)
			{
				if (TryFreeYardCell(Z, Bands[i], out Free))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Every finished house in a zone with room to take up a yard trade or already
		/// carrying one &mdash; the candidate set <see cref="ShowYardTrades"/> lists.</summary>
		public static List<GameObject> ListHousesWithYards(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (!TryReadHouse(item, out var entry, out var spec, out var rect) || !KingdomYardRules.IsEligibleDesign(spec.Size, spec.Open, entry.Category))
				{
					continue;
				}
				if (!string.IsNullOrEmpty(item.GetStringProperty(YardKeyProperty)))
				{
					found.Add(item);
					continue;
				}
				if (TryFreeYardCell(Z, KingdomPlots.YardRects(item), out _))
				{
					found.Add(item);
				}
			}
			return found;
		}

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

namespace XRL.World.Parts
{
	/// <summary>
	/// Attached to a house the moment it takes up a yard trade. Carries no state of its own
	/// &mdash; the trade lives on <see cref="KingdomYards.YardKeyProperty"/>, which survives a
	/// reload on its own, so this part only ever reads it back to say so on the object itself
	/// (see <c>r_KingdomImprovement</c>'s identical idiom in Growth/KingdomUpgrade.cs). Attaching
	/// it a second time after a reload is idempotent: <c>RequirePart</c> never duplicates it.
	/// </summary>
	[Serializable]
	public class r_KingdomYardTrade : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (base.WantEvent(ID, cascade))
			{
				return true;
			}
			return ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			string key = ParentObject?.GetStringProperty(KingdomYards.YardKeyProperty);
			if (!string.IsNullOrEmpty(key) && KingdomYards.TryGetSpec(key, out var work))
			{
				E.Postfix.Append("\n").Append(KingdomYardRules.DescriptionLine(work));
			}
			return base.HandleEvent(E);
		}
	}
}
