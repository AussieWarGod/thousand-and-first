using System;
using ConsoleLib.Console;
using HarmonyLib;
using XRL;
using XRL.Core;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Read native visibility during the actual zone draw and after its restoration.</summary>
	internal static class KingdomQuickstartSightWitness
	{
		private static Zone Zone;
		private static KingdomClaimedGroundLight Part;
		private static bool[] Honest;
		private static int Projections, Draws, Restores, Hidden;
		private static string Fault;
		private static bool Done;

		internal static void Start(Zone Current, KingdomClaimedGroundLight Attached)
		{
			Require(Zone == null, "sight observer already started");
			Zone = Current; Part = Attached;
			var harmony = new Harmony("com.thousandandfirst.harness.quickstart-sight");
			harmony.Patch(AccessTools.Method(typeof(KingdomClaimedGroundLight), "ProjectCitySight"),
				prefix: new HarmonyMethod(typeof(KingdomQuickstartSightWitness), nameof(BeforeProjection)));
			harmony.Patch(AccessTools.Method(typeof(Zone), "Render", new[] { typeof(ScreenBuffer) }),
				postfix: new HarmonyMethod(typeof(KingdomQuickstartSightWitness), nameof(AfterZoneDraw)));
			harmony.Patch(AccessTools.Method(typeof(XRLCore), "RenderBaseToBuffer", new[] { typeof(ScreenBuffer) }),
				postfix: new HarmonyMethod(typeof(KingdomQuickstartSightWitness), nameof(AfterFrame)));
		}

		private static void BeforeProjection(KingdomClaimedGroundLight __instance)
		{
			if (Done || !ReferenceEquals(__instance, Part)) return;
			try
			{
				Require(Honest == null, "previous frame left an outstanding witness");
				Honest = (bool[])Zone.VisibilityMap.Clone();
				int hidden = 0;
				foreach (bool visible in Honest) if (!visible) hidden++;
				Hidden = Math.Max(Hidden, hidden);
				Projections++;
			}
			catch (Exception error) { Fault = error.Message; }
		}

		private static void AfterZoneDraw(Zone __instance)
		{
			if (Done || !ReferenceEquals(__instance, Zone) || Honest == null) return;
			try
			{
				for (int y = 0; y < Zone.Height; y++)
					for (int x = 0; x < Zone.Width; x++)
						Require(Zone.GetCell(x, y).IsVisible() && Zone.GetCell(x, y).IsLit(),
							"cell hidden or dark during native zone draw: " + x + "," + y);
				Draws++;
			}
			catch (Exception error) { Fault = error.Message; }
		}

		private static void AfterFrame()
		{
			if (Done || Honest == null) return;
			try
			{
				Require(ReferenceEquals(The.ActiveZone, Zone), "frame changed zones");
				bool[] live = Zone.VisibilityMap;
				Require(live.Length == Honest.Length, "visibility map changed size");
				for (int i = 0; i < live.Length; i++)
					Require(live[i] == Honest[i], "drawing leaked city sight into gameplay at cell " + i);
				Restores++;
			}
			catch (Exception error) { Fault = error.Message; }
			finally { Honest = null; }
		}

		internal static string Check(Zone Current, KingdomClaimedGroundLight Attached)
		{
			Require(!Done && ReferenceEquals(Current, Zone) && ReferenceEquals(Attached, Part),
				"sight observer owner changed");
			Require(Fault == null, Fault);
			Require(!KingdomScenarioFrames.Pending && KingdomScenarioFrames.Observations >= 3,
				"three real engine frames not observed");
			Require(Projections >= 3 && Draws == Projections && Restores == Draws && Honest == null,
				"draw/projection/restoration counts disagree: " + Projections + "/" + Draws + "/" + Restores);
			Require(Hidden > 0, "no naturally occluded cells: through-wall sight not exercised");
			Done = true;
			return "whole-zone-drawn=true; cells=" + Zone.Width * Zone.Height + "; frames=" + Draws
				+ "; naturally-hidden-cells=" + Hidden + "; gameplay-sight-restored=true; synthetic-visibility=false";
		}

		private static void Require(bool Value, string Failure) => KingdomQuickstartSightNativeProvider.Require(Value, Failure);
	}
}
