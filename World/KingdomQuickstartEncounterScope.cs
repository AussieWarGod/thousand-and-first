using System;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>A pre-founded camp cannot begin as a random legendary faction party's camp.
	/// The exact initial GetZone call reserves its ambient encounter slot, before actors exist.
	/// No loaded body is removed, relocated, pacified or made invulnerable.</summary>
	internal sealed class KingdomQuickstartEncounterScope : IDisposable
	{
		[ThreadStatic] private static KingdomQuickstartEncounterScope Active;
		private readonly XRLGame Game;
		private readonly ZoneManager Manager;
		private readonly KingdomQuickstartProfile Profile;
		private bool Disposed;

		private KingdomQuickstartEncounterScope(XRLGame Game, KingdomQuickstartProfile Profile)
		{
			this.Game = Game; Manager = Game.ZoneManager; this.Profile = Profile;
			Active = this;
		}

		internal static IDisposable Begin(XRLGame Game, KingdomQuickstartProfile Profile)
		{
			if (Active != null || Game == null || Profile == null || Game.ZoneManager == null
				|| !ReferenceEquals(Game, The.Game) || !KingdomQuickstartRules.IsMode(Game.gameMode)
				|| !KingdomQuickstartRules.WorldReservationMatches(Game.GetStringGameState(
					KingdomQuickstartRules.WorldReservationState, null), Profile))
				throw new InvalidOperationException("Initial camp encounter scope lacks exact reservation authority.");
			return new KingdomQuickstartEncounterScope(Game, Profile);
		}

		internal static bool Reserves(Zone Zone)
		{
			var scope = Active;
			return scope != null && !scope.Disposed && Zone != null
				&& ReferenceEquals(The.Game, scope.Game)
				&& ReferenceEquals(scope.Game.ZoneManager, scope.Manager)
				&& ReferenceEquals(ZoneManager.ZoneGenerationContext, Zone)
				&& Zone.ZoneID == scope.Profile.ZoneId;
		}

		public void Dispose()
		{
			if (Disposed) return;
			if (!ReferenceEquals(Active, this))
				throw new InvalidOperationException("Initial camp encounter scope lost its owner.");
			Disposed = true;
			Active = null;
		}
	}

	[HarmonyPatch(typeof(FactionEncounters), nameof(FactionEncounters.BuildZone))]
	internal static class KingdomQuickstartAmbientEncounterPatch
	{
		[HarmonyPrefix]
		internal static bool Before(Zone Z, ref bool __result)
		{
			if (!KingdomQuickstartEncounterScope.Reserves(Z)) return true;
			KingdomLog.Log("quickstart camp: reserved ambient faction encounter slot in " + Z.ZoneID);
			__result = true;
			return false;
		}
	}
}
