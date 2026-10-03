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
	/// (KingdomSecondCitySiteRules.RiteOrder, seeded with city one's rite) must be empty, must
	/// give production's founding heart the unslid rung-1 rect city one's rite has, must hold no
	/// liquid inside that rect (the FoundingHeartGroundAllows predicate, read through
	/// KingdomPlots.GroundGrid), and every cell of the rect's ingress envelope must pass
	/// KingdomRoads.Walkable, the predicate TryVerifyPhysicalIngressRoutes applies. These are
	/// production's own predicates, read before anything is spent. Production may still refuse
	/// the founding; that refusal is the evidence, and nothing here retries it.
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
				int offered;
				if (!TryRite(zone, homeCell.X, homeCell.Y, out rite, out heart, out offered))
				{
					tried.Add(id + " (no dry walkable heart ground among " + offered + " rites)");
					continue;
				}
				KingdomSecondCityNativeChecks.SiteZoneId = id;
				KingdomSecondCityNativeChecks.SiteZone = zone;
				KingdomSecondCityNativeChecks.SiteCell = rite;
				KingdomSecondCityNativeChecks.SiteHeart = heart;
				return "site=" + id + " rite=" + rite.X + "," + rite.Y + " heart=" + Describe(heart)
					+ " envelope=" + Describe(KingdomSecondCitySiteRules.IngressEnvelope(heart))
					+ " probes=" + probes + " rejected=" + tried.Count;
			}
			Require(false, "no eligible second-city site: probed " + probes + " of at most "
				+ MaxProbes + " parasangs (" + candidates.Count + " candidates in rings "
				+ KingdomSecondCitySiteRules.MinRing + ".." + KingdomSecondCitySiteRules.MaxRing
				+ "); tried " + KingdomScenarioRules.Bounded(string.Join(", ", tried.ToArray())));
			return null;
		}

		/// <summary>
		/// The first rite cell, in RiteOrder, whose ground production's founding heart can take.
		/// Offered is how many unslid rite cells this map has at all, for the refusal row.
		/// </summary>
		private static bool TryRite(Zone Zone, int PreferredX, int PreferredY, out Cell Rite,
			out KingdomPlotRules.PlotRect Heart, out int Offered)
		{
			Rite = null;
			Heart = default(KingdomPlotRules.PlotRect);
			IList<int> order = KingdomSecondCitySiteRules.RiteOrder(PreferredX, PreferredY,
				Zone.Width, Zone.Height);
			Offered = order.Count;
			KingdomPlots.GroundGrid grid = new KingdomPlots.GroundGrid(Zone);
			// 0 unread, 1 walkable, -1 not: each cell is read once however many envelopes share it.
			sbyte[] walkable = new sbyte[Zone.Width * Zone.Height];
			for (int i = 0; i < order.Count; i++)
			{
				int x = order[i] % Zone.Width;
				int y = order[i] / Zone.Width;
				Cell cell = Zone.GetCell(x, y);
				KingdomPlotRules.PlotRect rect;
				if (cell == null || cell.Objects.Count != 0
					|| !KingdomSecondCitySiteRules.TryRiteHeartRect(x, y, Zone.Width, Zone.Height,
						out rect)
					|| !Dry(grid, rect)
					|| !Walkable(Zone, walkable, KingdomSecondCitySiteRules.IngressEnvelope(rect)))
					continue;
				Rite = cell;
				Heart = rect;
				return true;
			}
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

		private static bool Walkable(Zone Zone, sbyte[] Known, KingdomPlotRules.PlotRect Envelope)
		{
			for (int y = Envelope.Y1; y <= Envelope.Y2; y++)
				for (int x = Envelope.X1; x <= Envelope.X2; x++)
				{
					int key = y * Zone.Width + x;
					if (Known[key] == 0)
						Known[key] = KingdomRoads.Walkable(Zone.GetCell(x, y)) ? (sbyte)1 : (sbyte)-1;
					if (Known[key] < 0) return false;
				}
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
