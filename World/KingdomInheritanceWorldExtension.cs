using System;
using System.Collections.Generic;
using System.Globalization;
using Genkit;
using Qud.API;
using XRL;
using XRL.World;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	/// <summary>
	/// Claims one exact, still-mutable Joppa surface site after vanilla world construction.
	/// Selection is pure and stable; this hook consumes no runtime RNG and creates no object.
	/// </summary>
	[JoppaWorldBuilderExtension]
	public sealed class KingdomInheritanceWorldExtension : IJoppaWorldBuilderExtension
	{
		public override void OnAfterBuild(JoppaWorldBuilder Builder)
		{
			KingdomInheritanceState state = KingdomInheritanceState.Instance;
			MutabilityMap removedMap = null;
			int removedX = -1;
			int removedY = -1;
			string removedTerrain = "";
			bool staged = false;
			try
			{
				string legacyId;
				string oldGroundZoneId;
				string preferredTerrain;
				if (state == null || !state.TrySelectionInputs(out legacyId,
					out oldGroundZoneId, out preferredTerrain))
				{
					return;
				}
				if (Builder == null || Builder.mutableMap == null || Builder.worldInfo == null
					|| Builder.WorldZone == null
					|| Builder.WorldZone.ZoneID != KingdomInheritanceSiteRules.WorldId)
				{
					state.RefuseBootstrap("the active world builder was not the canonical Joppa world");
					return;
				}

				List<KingdomInheritanceSiteCandidate> candidates =
					new List<KingdomInheritanceSiteCandidate>(240 * 75);
				KingdomInheritanceWorldIndex index = new KingdomInheritanceWorldIndex(
					Builder.WorldZone, Builder.worldInfo);
				for (int y = 0; y < 75; y++)
				{
					for (int x = 0; x < 240; x++)
					{
						KingdomInheritanceSiteCandidate candidate;
						if (KingdomInheritanceWorldRuntime.TryCandidate(Builder.WorldZone,
							Builder.mutableMap, Builder.worldInfo, index, x, y, out candidate))
						{
							candidates.Add(candidate);
						}
					}
				}

				KingdomInheritanceSiteCandidate selected;
				KingdomInheritanceSiteFault fault;
				if (!KingdomInheritanceSiteRules.TrySelect(candidates, legacyId,
					oldGroundZoneId, preferredTerrain, out selected, out fault))
				{
					state.RefuseBootstrap("no compatible mutable surface site remained: "
						+ fault.ToString());
					return;
				}

				int targetX;
				int targetY;
				if (!KingdomInheritanceSiteRules.TrySurfaceCoordinates(selected.ZoneId,
					out targetX, out targetY)
					|| Builder.mutableMap.GetMutable(targetX, targetY) != 1)
				{
					state.RefuseBootstrap("the selected mutable site changed before reservation");
					return;
				}
				Builder.mutableMap.RemoveMutableLocation(Location2D.Get(targetX, targetY));
				removedMap = Builder.mutableMap;
				removedX = targetX;
				removedY = targetY;
				removedTerrain = selected.TerrainTag ?? "";
				if (Builder.mutableMap.GetMutable(targetX, targetY) != 0)
				{
					RestoreRemoved(removedMap, removedX, removedY, removedTerrain);
					removedMap = null;
					state.RefuseBootstrap("the exact selected site could not be removed from the mutable pool");
					return;
				}
				string failure;
				if (!state.StageSite(selected, targetX, targetY, Builder.mutableMap,
					Builder.worldInfo, out failure))
				{
					RestoreRemoved(removedMap, removedX, removedY, removedTerrain);
					removedMap = null;
					state.RefuseBootstrap("the selected site could not be staged: " + failure);
				}
				else
				{
					staged = true;
				}
			}
			catch (Exception ex)
			{
				if (!staged)
				{
					RestoreRemoved(removedMap, removedX, removedY, removedTerrain);
				}
				state?.RefuseBootstrap("the Joppa inheritance extension failed closed: " + ex.Message);
				MetricsManager.LogError("ThousandAndFirst inheritance world extension", ex);
			}
		}

		private static void RestoreRemoved(MutabilityMap Map, int X, int Y, string Terrain)
		{
			if (Map != null && X >= 0 && Y >= 0 && Map.GetMutable(X, Y) == 0)
			{
				Map.AddMutableLocation(Location2D.Get(X, Y), Terrain, 1);
			}
		}
	}
}
