using System;
using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.CharacterBuilds.Qud.UI;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Windowless embark seam for the explicit Kingdom Quickstart mode.</summary>
	public sealed class KingdomQuickstartEmbarkModule : AbstractEmbarkBuilderModule
	{
		private const string CampBuilder = "KingdomQuickstartCampBuilder";

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
				StringComparison.Ordinal)) BindCampBuilder(game, element as GlobalLocation);
			else if (string.Equals(id, QudGameBootModule.BOOTEVENT_GAMESTARTING,
				StringComparison.Ordinal))
			{
				game?.RequireSystem<KingdomQuickstartLifecycle>();
				string failure;
				if (!KingdomQuickstartBootstrap.Run(game, out failure))
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

		private static void BindCampBuilder(XRLGame Game, GlobalLocation Where)
		{
			KingdomQuickstartProfile profile;
			if (Game == null || !KingdomQuickstartRules.IsMode(Game.gameMode)
				|| !Game.GetBooleanGameState("r_TAF_KingdomMode")
				|| !KingdomQuickstartRules.TryProfile(Game.GetStringGameState(
					KingdomQuickstartRules.ProfileState, null), out profile)
				|| !KingdomQuickstartRules.WorldReservationMatches(Game.GetStringGameState(
					KingdomQuickstartRules.WorldReservationState, null), profile)
				|| Where == null || !string.Equals(Where.ZoneID, profile.ZoneId,
					StringComparison.Ordinal)
				|| Where.CellX != KingdomQuickstartRules.StartCellX
				|| Where.CellY != KingdomQuickstartRules.StartCellY
				|| The.ZoneManager == null) return;
			The.ZoneManager.AddZonePostBuilder(profile.ZoneId, CampBuilder);
		}
	}
}
