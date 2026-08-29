using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomReopenedExoticActivation
	{
		static partial void AppendAssentingMoot(KingdomSystem System, List<string> Roster)
		{
			if (AssentingMootEligible(System, Roster) && !Roster.Contains(AssentingMootKey))
				Roster.Add(AssentingMootKey);
		}

		internal static bool AssentingMootEligible(KingdomSystem System,
			IList<string> Roster)
		{
			bool node = Roster != null && KingdomZoningRules.Knows(Roster, "node:assent");
			bool rite = Roster != null && KingdomZoningRules.Knows(Roster, "rite:Chavvah");
			bool surface;
			bool adjacent = HasMoonStairAdjacency(System, out surface);
			return KingdomAssentingMootRules.ActivationEligible(
				System != null && System.Founded, node, rite, surface, adjacent,
				r_KingdomAssentingMoot.RuntimeOwnerVersion
					== KingdomAssentingMootRules.CurrentReceiptVersion);
		}

		private static bool HasMoonStairAdjacency(KingdomSystem System, out bool Surface)
		{
			Surface = false;
			if (System?.ClaimedZones == null || The.Game?.ZoneManager == null) return false;
			for (int i = 0; i < System.ClaimedZones.Count; i++)
			{
				string world;
				int px;
				int py;
				int zx;
				int zy;
				int z;
				try
				{
					if (!ZoneID.Parse(System.ClaimedZones[i], out world, out px, out py,
						out zx, out zy, out z) || z != 10) continue;
				}
				catch (Exception) { continue; }
				Surface = true;
				if (MoonStairAt(world, px - 1, py) || MoonStairAt(world, px + 1, py)
					|| MoonStairAt(world, px, py - 1) || MoonStairAt(world, px, py + 1))
					return true;
			}
			return false;
		}

		private static bool MoonStairAt(string World, int X, int Y)
		{
			if (string.IsNullOrEmpty(World) || X < 0 || Y < 0) return false;
			try
			{
				GameObject terrain = ZoneManager.GetTerrainObjectForZone(X, Y, World);
				return terrain?.GetBlueprint()?.DescendsFrom("TerrainMoonStair") == true;
			}
			catch (Exception) { return false; }
		}
	}
}
