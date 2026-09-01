using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		private static void RecordSemantic(IDictionary<int, RoadSemantic> Semantics,
			int Cell, string Role, int Width)
		{
			if (Semantics == null) return;
			KingdomRoadFrontage incoming = new KingdomRoadFrontage(Role, Width, 1);
			if (Semantics.TryGetValue(Cell, out var prior))
			{
				KingdomRoadFrontage merged = KingdomRoadClearanceRules.Merge(
					new KingdomRoadFrontage(prior.Role, prior.Width, 1), incoming);
				Semantics[Cell] = new RoadSemantic(merged.Role, merged.PreferredWidth);
			}
			else
			{
				KingdomRoadFrontage normalized = KingdomRoadClearanceRules.Merge(
					new KingdomRoadFrontage(KingdomRoadPaletteRules.LocalRole, 1, 1), incoming);
				Semantics.Add(Cell, new RoadSemantic(normalized.Role,
					normalized.PreferredWidth));
			}
		}

		private static void StampRoadSemantic(GameObject Floor, int X, int Y, int ZoneWidth,
			IDictionary<int, RoadSemantic> Semantics)
		{
			if (!GameObject.Validate(Floor) || Semantics == null || ZoneWidth <= 0
				|| !Semantics.TryGetValue(KingdomRoadRules.Pack(X, Y, ZoneWidth),
					out var semantic)) return;
			KingdomRoadFrontage merged = KingdomRoadClearanceRules.Merge(
				new KingdomRoadFrontage(RoadRole(Floor), RoadWidth(Floor), 1),
				new KingdomRoadFrontage(semantic.Role, semantic.Width, 1));
			Floor.SetStringProperty(PathRoleProperty, merged.Role);
			Floor.SetIntProperty(PathWidthProperty, merged.PreferredWidth);
		}

		private static void CopyRoadSemantic(GameObject From, GameObject To)
		{
			if (!GameObject.Validate(From) || !GameObject.Validate(To)) return;
			KingdomRoadFrontage merged = KingdomRoadClearanceRules.Merge(
				new KingdomRoadFrontage(RoadRole(To), RoadWidth(To), 1),
				new KingdomRoadFrontage(RoadRole(From), RoadWidth(From), 1));
			To.SetStringProperty(PathRoleProperty, merged.Role);
			To.SetIntProperty(PathWidthProperty, merged.PreferredWidth);
		}

		private static string RoadRole(GameObject Floor)
		{
			if (GameObject.Validate(Floor)
				&& KingdomRoadPaletteRules.TryRole(Floor.GetStringProperty(PathRoleProperty),
					out string role)) return role;
			return KingdomRoadPaletteRules.LocalRole;
		}

		private static int RoadWidth(GameObject Floor)
		{
			int width = GameObject.Validate(Floor) ? Floor.GetIntProperty(PathWidthProperty) : 0;
			return width == 2 ? 2 : 1;
		}

		private static bool TryRoadSurface(KingdomSystem System, Zone Z, GameObject Floor,
			out KingdomRoadSurface Surface)
		{
			Surface = default(KingdomRoadSurface);
			if (System == null || Z == null || !GameObject.Validate(Floor)) return false;
			string terrain = KingdomRoadPaletteRules.TerrainKey(System.Style,
				System.FoundingRegionName, KingdomPlotRules.IsUnderground(Z.Z));
			return KingdomRoadPaletteRules.TryResolveCurrent(terrain, RoadRole(Floor),
				KingdomZoning.Tech(System), out Surface);
		}

		private static bool SameSurface(KingdomRoadSurface A, KingdomRoadSurface B)
		{
			return A.Blueprint == B.Blueprint && A.Material == B.Material;
		}
	}
}
