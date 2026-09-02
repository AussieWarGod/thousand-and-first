using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			// A positional reader can fail before Read() is entered. Detect that constructor-default
			// sentinel before touching the blank recovery object returned by Qud's block skipper.
			if (RefuseIncompleteLoad()) return;
			// The research registry and everything it caches about the world are process statics,
			// so a second game in the same session would otherwise read the first one's quest
			// verdicts and believe its journal notes were already filed.
			KingdomResearch.Reload();
			NormalizeState(AllowLegacyIdentityMigration: false);
			MigrateDirectionalStandingStateAfterLoad();
			ValidateDirectionalFactionRegistryAfterLoad();
			// Local-view reconstruction is not new simulation work and remains safe while the
			// master option is off. Migrated schema-1 state also gets exact isolation cleanup.
			KingdomFounderHistory.ReconcileBestEffort(this);
			// AfterGameLoadedEvent owns option observation. Until then, configured master-off load is
			// decode/validation only: do not continue an external/profile or physical transition.
			if (!KingdomMaster.AutomaticWorkAllowed(this))
			{
				return;
			}
			if (ExiledRealmArchive != null &&
				(ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Prepared ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.TradeClosed ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.MirrorsPublished ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleFrozen ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleCleared ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Resetting))
			{
				string refusal;
				ContinueExileTransition(out refusal);
			}
		}

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterReputationChangeEvent.ID);
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(ZoneActivatedEvent.ID);
			// Deactivation immediately zeros attended hosted-floor credit. A deactivated zone can
			// remain live for up to forty turns, so only SuspendingEvent may replace that zero with
			// the true final reading after the grace window.
			Registrar.Register(ZoneDeactivatedEvent.ID);
			// This fires from SuspendZone BEFORE Suspended is set, while objects are still in RAM.
			Registrar.Register(SuspendingEvent.ID);
			// The pump, and the ONE per-turn cost this design adds anywhere (§0.0(e)). Game-level
			// EndTurnEvent.Send(game) is a single dispatch immediately before ProcessSingleTurn
			// (D/XRL/Core/ActionManager.cs:1644-1650), not the 2,000-cell broadcast a live zone
			// pays. It does not fire during world-map travel, which is exactly why §2.1 bans it as
			// the city's CLOCK -- but a founder on the world map is standing in no city zone and is
			// owed no reification, so the same blind spot is harmless in a pump.
			Registrar.Register(EndTurnEvent.ID);
			// The second reify hook (§3.5), and the one instant the stale-transient sweep may run
			// (§3.8 t3): any zone coming off disk, before intake and before anything looks at it.
			Registrar.Register(ZoneThawedEvent.ID);
			// Research quest locks are event-driven and cached, never polled. This fires AFTER all
			// quest state is consistent, which is why it and not QuestStepFinishedEvent is the hook.
			Registrar.Register(QuestFinishedEvent.ID);
		}

		/// <summary>Player-scoped events follow the active body. <see cref="IPlayerSystem"/>
		/// unregisters this system from the old body and registers it on the new one after
		/// domination, metempsychosis, or Kingdom succession.</summary>
		public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar)
		{
			// Vanilla exposes no ritual-completion event. Its player-dispatched start event carries
			// Initial, the exact first-sharing fact a rite source needs.
			Registrar.Register(WaterRitualStartEvent.ID);
		}

	}
}
