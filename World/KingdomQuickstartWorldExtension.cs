using System;
using Genkit;
using XRL;
using XRL.World;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	/// <summary>
	/// Reserves the selected wilderness parasang before vanilla spends mutable cells on villages,
	/// lairs, or encounters. It is inert outside the explicit Kingdom Quickstart game mode.
	/// </summary>
	[JoppaWorldBuilderExtension]
	public sealed class KingdomQuickstartWorldExtension : IJoppaWorldBuilderExtension
	{
		public override void OnAfterMutableInit(JoppaWorldBuilder Builder)
		{
			XRLGame game = The.Game;
			KingdomQuickstartProfile profile = null;
			bool removed = false;
			try
			{
				if (game == null || !KingdomQuickstartRules.IsMode(game.gameMode)) return;
				if (!game.GetBooleanGameState("r_TAF_KingdomMode")
					|| Builder == null || Builder.mutableMap == null
					|| Builder.WorldZone == null || Builder.WorldZone.ZoneID != "JoppaWorld"
					|| !KingdomQuickstartRules.TryProfile(
						game.GetStringGameState(KingdomQuickstartRules.ProfileState, null),
						out profile))
					throw new InvalidOperationException("quickstart profile or canonical Joppa map was absent");

				Location2D block = Location2D.Get(profile.WorldX, profile.WorldY);
				string expected = KingdomQuickstartRules.WorldReservation(profile);
				string existing = game.GetStringGameState(
					KingdomQuickstartRules.WorldReservationState, null);
				if (!string.IsNullOrEmpty(existing))
				{
					if (!string.Equals(existing, expected, StringComparison.Ordinal)
						|| !BlockIs(Builder.mutableMap, profile, 0))
						throw new InvalidOperationException("quickstart reservation proof disagreed with the mutable map");
					return;
				}
				if (!Builder.mutableMap.GetWorldBlockMutable(block)
					|| !BlockIs(Builder.mutableMap, profile, 1))
					throw new InvalidOperationException("selected quickstart parasang was already reserved");

				Builder.mutableMap.SetWorldBlockMutable(block, 0);
				removed = true;
				if (!BlockIs(Builder.mutableMap, profile, 0))
					throw new InvalidOperationException("selected quickstart parasang did not leave the mutable pool");
				game.SetStringGameState(KingdomQuickstartRules.WorldReservationState, expected);
				if (!KingdomQuickstartRules.WorldReservationMatches(game.GetStringGameState(
					KingdomQuickstartRules.WorldReservationState, null), profile))
					throw new InvalidOperationException("quickstart reservation proof did not publish exactly");
			}
			catch (Exception ex)
			{
				if (removed && profile != null && Builder?.mutableMap != null)
					RestoreBlock(Builder.mutableMap, profile);
				if (game != null)
					game.RemoveStringGameState(KingdomQuickstartRules.WorldReservationState);
				MetricsManager.LogError("ThousandAndFirst quickstart world reservation", ex);
			}
		}

		private static bool BlockIs(MutabilityMap Map, KingdomQuickstartProfile Profile,
			int Expected)
		{
			if (Map == null || Profile == null) return false;
			for (int localY = 0; localY < 3; localY++)
				for (int localX = 0; localX < 3; localX++)
					if (Map.GetMutable(Profile.WorldX * 3 + localX,
						Profile.WorldY * 3 + localY) != Expected) return false;
			return true;
		}

		private static void RestoreBlock(MutabilityMap Map, KingdomQuickstartProfile Profile)
		{
			for (int localY = 0; localY < 3; localY++)
				for (int localX = 0; localX < 3; localX++)
				{
					Location2D cell = Location2D.Get(Profile.WorldX * 3 + localX,
						Profile.WorldY * 3 + localY);
					if (Map.GetMutable(cell) == 0)
						Map.AddMutableLocation(cell, Profile.TerrainFamily, 1);
				}
		}
	}
}
