using System;
using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.CharacterBuilds.Qud.UI;
using XRL.UI;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>Windowless embark seam for the explicit Kingdom Quickstart mode.</summary>
	public sealed class KingdomQuickstartEmbarkModule : AbstractEmbarkBuilderModule
	{
		private sealed class CampAttempt
		{
			internal XRLGame Game;
			internal EmbarkInfo Info;
			internal ZoneManager Manager;
			internal GamePlayer Player;
			internal GlobalLocation Where;
			internal KingdomQuickstartProfile Profile;
			internal string GameId;
			internal Zone ActiveZone, PreparedZone;
			internal GameObject Founder;
			internal bool Consumed, Prepared, BootstrapEntered;
			internal string Failure;
		}

		[NonSerialized] private CampAttempt Camp;
		[NonSerialized] private bool PreparingCamp;

		public override bool IncludeInBuildCodes()
		{
			return false;
		}

		public override string DataErrors()
		{
			return null;
		}

		public override bool shouldBeEnabled()
		{
			QudGamemodeModule modes = builder == null
				? null : builder.GetModule<QudGamemodeModule>();
			return modes != null && KingdomQuickstartRules.IsMode(modes.GetMode());
		}

		public override object handleUIEvent(string id, object element)
		{
			if (string.Equals(id,
				QudChooseStartingLocationModuleWindow.EventNames.EID_GET_STARTING_LOCATION_SET,
				StringComparison.Ordinal)) return KingdomQuickstartRules.LocationSet;
			return base.handleUIEvent(id, element);
		}

		public override object handleBootEvent(string id, XRLGame game, EmbarkInfo info,
			object element = null)
		{
			if (string.Equals(id, QudGameBootModule.BOOTEVENT_BOOTSTARTINGLOCATION,
				StringComparison.Ordinal)) BindCampBuilder(game, info, element as GlobalLocation);
			else if (string.Equals(id, QudGameBootModule.BOOTEVENT_AFTERBOOTPLAYEROBJECT,
				StringComparison.Ordinal)) PrepareCamp(game, info, element as GameObject);
			else if (string.Equals(id, QudGameBootModule.BOOTEVENT_GAMESTARTING,
				StringComparison.Ordinal))
			{
				if (game == null || !ReferenceEquals(game, The.Game)
					|| !KingdomQuickstartRules.IsMode(game.gameMode))
					return base.handleBootEvent(id, game, info, element);
				game?.RequireSystem<KingdomQuickstartLifecycle>();
				string failure;
				if (!EnterBootstrap(game, info, out failure)
					|| !KingdomQuickstartBootstrap.Run(game, out failure))
				{
					MetricsManager.LogError("ThousandAndFirst quickstart bootstrap: " + failure);
					Popup.Show("Kingdom Quickstart stopped before granting any further stock. "
						+ failure + "\n\nThis world remains playable, but the quickstart receipt will "
						+ "not invent replacement goods. Start another Kingdom Quickstart world or "
						+ "found normally if you want a clean opening.");
				}
			}
			return base.handleBootEvent(id, game, info, element);
		}

		private void BindCampBuilder(XRLGame Game, EmbarkInfo Info, GlobalLocation Where)
		{
			if (Game == null || !ReferenceEquals(Game, The.Game)) return;
			if (PreparingCamp)
			{
				if (ReferenceEquals(Camp?.Game, Game) && ReferenceEquals(Camp.Info, Info))
					Camp.Failure = "A nested camp preparation was refused.";
				return;
			}
			if (ReferenceEquals(Camp?.Game, Game)) return;
			try
			{
				if (Info == null || !KingdomQuickstartRules.IsMode(Game.gameMode)
					|| !Game.GetBooleanGameState("r_TAF_KingdomMode")
					|| !KingdomQuickstartRules.TryProfile(Game.GetStringGameState(
						KingdomQuickstartRules.ProfileState, null), out KingdomQuickstartProfile profile)) return;
				CampAttempt attempt = new CampAttempt { Game = Game, Info = Info,
					Manager = Game.ZoneManager, Player = Game.Player, Where = Where,
					Profile = profile, GameId = Game.GameID, ActiveZone = Game.ZoneManager?.ActiveZone };
				Camp = attempt;
				if (!ExactReservation(attempt) || !EmptyOpening(Game) || attempt.Player == null
					|| attempt.Player.Body != null)
				{
					attempt.Consumed = true;
					attempt.Failure = "The selected camp lacks exact unused new-game authority.";
				}
			}
			catch (Exception error)
			{
				if (ReferenceEquals(Camp?.Game, Game))
				{
					Camp.Consumed = true;
					Camp.Failure = "The camp could not reserve its new-game preparation.";
				}
				MetricsManager.LogError("ThousandAndFirst quickstart camp reservation", error);
			}
		}

		private void PrepareCamp(XRLGame Game, EmbarkInfo Info, GameObject Founder)
		{
			CampAttempt attempt = Camp;
			if (attempt == null || !ReferenceEquals(attempt.Game, Game)
				|| !ReferenceEquals(attempt.Info, Info)) return;
			if (PreparingCamp)
			{
				attempt.Failure = "A nested camp preparation was refused.";
				return;
			}
			if (attempt.Consumed) return;
			attempt.Consumed = true;
			attempt.Founder = Founder;
			PreparingCamp = true;
			string failure = "The exact new-game founder was not unplaced.";
			try
			{
				if (!ExactUnplaced(attempt))
					throw new InvalidOperationException(failure);
				// GetZone returns after builders, biomes and zone events; no queued camp survives.
				// Player creation/mutators precede this seam; later embark handlers and the
				// engine's InitialSeeds reset still follow, so old RNG output is not promised.
				failure = "The completed camp zone could not be resolved.";
				Zone zone = attempt.Manager.GetZone(attempt.Profile.ZoneId);
				failure = "The completed camp ground lost its exact owner.";
				if (!ExactUnplaced(attempt) || !ExactZone(attempt, zone))
					throw new InvalidOperationException(failure);
				failure = "The bounded camp could not be safely prepared.";
				if (!new KingdomQuickstartCampBuilder().BuildZone(zone))
					throw new InvalidOperationException(failure);
				failure = "Camp preparation changed its exact owner.";
				if (!ExactUnplaced(attempt) || !ExactZone(attempt, zone))
					throw new InvalidOperationException(failure);
				// Clearing a wall does not refresh Qud's cached start-placement map. Add the
				// actual wall-free founder component; preserve prior bits and all physical terrain.
				zone.BuildReachableMap(KingdomQuickstartRules.StartCellX,
					KingdomQuickstartRules.StartCellY, bClearFirst: false);
				failure = "The prepared founder cell did not retain reachable, passable ground.";
				if (!zone.IsReachable(KingdomQuickstartRules.StartCellX, KingdomQuickstartRules.StartCellY)
					|| !KingdomQuickstartCampBuilder.Ready(zone)
					|| !ExactUnplaced(attempt) || !ExactZone(attempt, zone))
					throw new InvalidOperationException(failure);
				attempt.PreparedZone = zone;
				attempt.Prepared = true;
			}
			catch (Exception error)
			{
				if (string.IsNullOrEmpty(attempt.Failure))
					attempt.Failure = failure + " (" + error.GetType().Name + ")";
				MetricsManager.LogError("ThousandAndFirst quickstart camp preparation", error);
			}
			finally { PreparingCamp = false; }
		}

		private bool EnterBootstrap(XRLGame Game, EmbarkInfo Info, out string Failure)
		{
			Failure = "The camp was not prepared once before founder placement.";
			CampAttempt attempt = Camp;
			if (attempt == null || !ReferenceEquals(attempt.Game, Game)
				|| !ReferenceEquals(attempt.Info, Info)) return false;
			if (!string.IsNullOrEmpty(attempt.Failure)) Failure = attempt.Failure;
			if (attempt.BootstrapEntered) return false;
			attempt.BootstrapEntered = true;
			try
			{
				return !PreparingCamp && attempt.Prepared && string.IsNullOrEmpty(attempt.Failure)
					&& ExactReservation(attempt) && EmptyOpening(Game)
					&& ReferenceEquals(Game.Player, attempt.Player)
					&& GameObject.Validate(attempt.Founder) && ReferenceEquals(The.Player, attempt.Founder)
					&& ReferenceEquals(attempt.Manager.ActiveZone, attempt.PreparedZone)
					&& ExactZone(attempt, attempt.PreparedZone);
			}
			catch (Exception error)
			{
				Failure = "The prepared camp could not prove its new-game authority.";
				MetricsManager.LogError("ThousandAndFirst quickstart camp admission", error);
				return false;
			}
		}

		private bool ExactUnplaced(CampAttempt Attempt)
		{
			GameObject body = Attempt.Founder;
			return ReferenceEquals(Camp, Attempt) && !Attempt.BootstrapEntered
				&& string.IsNullOrEmpty(Attempt.Failure) && ExactReservation(Attempt)
				&& EmptyOpening(Attempt.Game) && ReferenceEquals(Attempt.Game.Player, Attempt.Player)
				&& Attempt.Player != null && Attempt.Player.Body == null && The.Player == null
				&& ReferenceEquals(Attempt.Manager.ActiveZone, Attempt.ActiveZone)
				&& GameObject.Validate(body) && body.CurrentCell == null
				&& body.InInventory == null && body.Equipped == null;
		}

		private static bool ExactZone(CampAttempt Attempt, Zone Zone)
		{
			return Zone != null && Zone.Built && Zone.Width > 45 && Zone.Height > 18
				&& (long)Zone.Width * Zone.Height <= 4096 && Zone.ZoneID == Attempt.Profile.ZoneId
				&& Attempt.Manager.CachedZones != null
				&& Attempt.Manager.CachedZones.TryGetValue(Attempt.Profile.ZoneId, out Zone cached)
				&& ReferenceEquals(cached, Zone);
		}

		private static bool ExactReservation(CampAttempt Attempt)
		{
			XRLGame game = Attempt.Game;
			return game != null && ReferenceEquals(game, The.Game) && game.GameID == Attempt.GameId
				&& Attempt.Manager != null && ReferenceEquals(game.ZoneManager, Attempt.Manager)
				&& ReferenceEquals(Attempt.Manager.Game, game) && KingdomQuickstartRules.IsMode(game.gameMode)
				&& game.GetBooleanGameState("r_TAF_KingdomMode")
				&& ExactString(game, KingdomQuickstartRules.ProfileState, Attempt.Profile.Key)
				&& ExactString(game, KingdomQuickstartRules.WorldReservationState,
					KingdomQuickstartRules.WorldReservation(Attempt.Profile))
				&& Attempt.Where != null && Attempt.Where.ZoneID == Attempt.Profile.ZoneId
				&& Attempt.Where.CellX == KingdomQuickstartRules.StartCellX
				&& Attempt.Where.CellY == KingdomQuickstartRules.StartCellY;
		}

		private static bool ExactString(XRLGame Game, string Key, string Value)
		{
			return Game.HasStringGameState(Key) && Game.GetStringGameState(Key, null) == Value
				&& !Game.HasIntGameState(Key) && !Game.HasInt64GameState(Key)
				&& !Game.HasBooleanGameState(Key) && !Game.HasObjectGameState(Key);
		}

		private static bool EmptyOpening(XRLGame Game)
		{
			return Game.GetSystem<KingdomSystem>()?.Founded != true
				&& !HasState(Game, KingdomQuickstartRules.ReceiptState)
				&& !HasState(Game, KingdomQuickstartRules.QuarantineState);
		}

		private static bool HasState(XRLGame Game, string Key)
		{
			return Game.HasStringGameState(Key) || Game.HasIntGameState(Key)
				|| Game.HasInt64GameState(Key) || Game.HasBooleanGameState(Key)
				|| Game.HasObjectGameState(Key);
		}
	}
}
