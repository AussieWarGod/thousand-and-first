using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Behavioural coverage row 12 "Multiple cities": where the second city is poured.
	/// Observation only - nothing here founds, claims, clears or moves anything; the travel case
	/// moves the founder onto the cell chosen here.
	/// <para>
	/// A candidate parasang (KingdomSecondCitySiteRules.Candidates) must build, answer to no
	/// foreign faction and read Allowed from KingdomFounding.JudgeSite. On it, a rite cell
	/// (KingdomSecondCitySiteRules.RiteOrder, seeded with city one's rite) must be empty, must hold
	/// no liquid inside its rung-1 heart rect (the FoundingHeartGroundAllows predicate, read
	/// through KingdomPlots.GroundGrid), and must pass production's own read-only founding-heart
	/// preflight, KingdomArchitectureRuntime.TryPrepareFoundingHeart: a pose binds the basin to
	/// the rite and every authored public ingress cell is walkable. That preflight is read with
	/// the current seat's selection context; the founding heart's tier has one fallback variant,
	/// so the new seat resolves the same layout. Production may still refuse the founding; that
	/// refusal is the evidence, and nothing here retries it.
	/// </para>
	/// </summary>
	internal static class KingdomSecondCityNativeSite
	{
		/// <summary>How many candidate parasangs may be built before the site search refuses.</summary>
		internal const int MaxProbes = 8;

		/// <summary>
		/// Resolves the site, its rite cell and the heart rect into the checks frame and returns
		/// the detail for the site evidence row; refuses naming every rejected candidate and the
		/// probe limit when nothing qualifies.
		/// </summary>
		internal static string Resolve(KingdomSystem System)
		{
			string home = KingdomSecondCityNativeChecks.HomeZoneId;
			Cell homeCell = KingdomSecondCityNativeChecks.HomeCell;
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(home);
			Require(candidates.Count > 0,
				"no surface parasang outside the bordering band exists for " + home);
			string key = KingdomPlotRules.HeartKeyForRung(KingdomSecondCitySiteRules.FoundingRung);
			KingdomRules.BuildEntry entry;
			Require(KingdomData.TryGetBuilding(key, out entry) && entry != null,
				"the founding heart " + key + " is missing from the catalogue");
			List<string> tried = new List<string>();
			int probes = 0;
			for (int i = 0; i < candidates.Count && probes < MaxProbes; i++)
			{
				string id = candidates[i];
				if (System.ClaimedZones.Contains(id)) { tried.Add(id + " (claimed)"); continue; }
				if (KingdomFounding.ZonesAdjacent(home, id))
				{ tried.Add(id + " (adjacent)"); continue; }
				probes++;
				Zone zone;
				try { zone = The.ZoneManager.GetZone(id); }
				catch (Exception) { tried.Add(id + " (unbuildable)"); continue; }
				if (zone == null) { tried.Add(id + " (null)"); continue; }
				if (KingdomRules.GroundIsForeignFaction(zone.GetZoneProperty("faction", null),
					KingdomSecondCityNativeChecks.RealmFactionName))
				{ tried.Add(id + " (foreign)"); continue; }
				KingdomSettlement.SecondFoundingVerdict verdict =
					KingdomFounding.JudgeSite(System, zone);
				if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed)
				{ tried.Add(id + " (" + verdict + ")"); continue; }
				Cell rite;
				KingdomPlotRules.PlotRect heart;
				string refusal;
				if (!TryRite(System, zone, key, entry.Category, homeCell.X, homeCell.Y, out rite,
					out heart, out refusal))
				{
					tried.Add(id + " (no seatable rite: " + refusal + ")");
					continue;
				}
				KingdomSecondCityNativeChecks.SiteZoneId = id;
				KingdomSecondCityNativeChecks.SiteZone = zone;
				KingdomSecondCityNativeChecks.SiteCell = rite;
				KingdomSecondCityNativeChecks.SiteHeart = heart;
				return "site=" + id + " rite=" + rite.X + "," + rite.Y + " heart=" + Describe(heart)
					+ " centred=" + (KingdomSecondCitySiteRules.IsCentred(heart, rite.X, rite.Y)
						? "true" : "false")
					+ " probes=" + probes + " rejected=" + tried.Count;
			}
			Require(false, "no eligible second-city site: probed " + probes + " of at most "
				+ MaxProbes + " parasangs (" + candidates.Count + " candidates in rings "
				+ KingdomSecondCitySiteRules.MinRing + ".." + KingdomSecondCitySiteRules.MaxRing
				+ "); tried " + KingdomScenarioRules.Bounded(string.Join(", ", tried.ToArray())));
			return null;
		}

		/// <summary>
		/// The first rite cell, in RiteOrder, that is empty, dry under its heart rect and passes
		/// production's founding-heart preflight. Refusal names how many cells each predicate
		/// turned away and the last preflight sentence, so a refused map explains itself.
		/// </summary>
		private static bool TryRite(KingdomSystem System, Zone Zone, string Key, string LotType,
			int PreferredX, int PreferredY, out Cell Rite, out KingdomPlotRules.PlotRect Heart,
			out string Refusal)
		{
			Rite = null;
			Heart = default(KingdomPlotRules.PlotRect);
			IList<int> order = KingdomSecondCitySiteRules.RiteOrder(PreferredX, PreferredY,
				Zone.Width, Zone.Height);
			KingdomPlots.GroundGrid grid = new KingdomPlots.GroundGrid(Zone);
			int occupied = 0;
			int wet = 0;
			int refused = 0;
			string last = null;
			for (int i = 0; i < order.Count; i++)
			{
				int x = order[i] % Zone.Width;
				int y = order[i] / Zone.Width;
				Cell cell = Zone.GetCell(x, y);
				KingdomPlotRules.PlotRect rect;
				if (cell == null || cell.Objects.Count != 0) { occupied++; continue; }
				if (!KingdomSecondCitySiteRules.TryRiteHeartRect(x, y, Zone.Width, Zone.Height,
					out rect)) continue;
				if (!Dry(grid, rect)) { wet++; continue; }
				KingdomArchitectureIntent intent;
				string failure;
				if (!KingdomArchitectureRuntime.TryPrepareFoundingHeart(System, Zone, rect, Key,
					LotType, x, y, out intent, out failure))
				{
					refused++;
					last = failure;
					continue;
				}
				Rite = cell;
				Heart = rect;
				Refusal = null;
				return true;
			}
			Refusal = order.Count + " cells, " + occupied + " occupied, " + wet + " wet, "
				+ refused + " refused by the heart preflight"
				+ (last == null ? "" : ", last: " + last);
			return false;
		}

		/// <summary>Production's founding-heart ground law: no liquid inside the rect.</summary>
		private static bool Dry(KingdomPlots.GroundGrid Grid, KingdomPlotRules.PlotRect Rect)
		{
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
					if (Grid.KindAt(x, y) == KingdomPlotRules.GroundKind.Liquid) return false;
			return true;
		}

		internal static string Describe(KingdomPlotRules.PlotRect Rect)
		{
			return Rect.X1 + "," + Rect.Y1 + "-" + Rect.X2 + "," + Rect.Y2;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomSecondCityNativeProvider.Require(Value, Failure);
		}
	}
}
