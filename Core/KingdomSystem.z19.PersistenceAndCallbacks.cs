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
		/// <summary>Founder-facing reason an expulsion did not proceed.</summary>
		private string ExileRefusal(ExileVerdict Verdict)
		{
			switch (Verdict)
			{
			case ExileVerdict.NothingFounded:
				return "You hold no realm. Nobody can put you out of ground that was never yours.";
			case ExileVerdict.AlreadyCastOut:
				return "{{C|" + KingdomPresentation.Rich(ExiledDisplayName ?? "The realm") + "}} has already put you out. It cannot do it twice.";
			case ExileVerdict.RegardHolds:
				return "{{C|" + KingdomPresentation.Rich(KingdomDisplayName ?? "The realm") + "}} holds you " + KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(FounderRegard())) + ". Nobody there is calling for the gate to be shut behind you.";
			default:
				return "";
			}
		}

		/// <summary>
		/// Reads the realm's regard for the founder after it changed, and lets the realm answer:
		/// a murmur, a warning read aloud, or the gate. Keyed entirely on the deed that moved the
		/// reputation, never on how long the founder has been gone.
		/// </summary>
		/// <param name="ReputationType">The engine's own reason for the change, or null.</param>
		private void OnRealmRegardChanged(string ReputationType)
		{
			RealmRegard current = KingdomExileRules.ClassifyRegard(FounderRegard());
			RealmRegard spoken = (RealmRegard)RegardSpoken;
			RegardStep step = KingdomExileRules.JudgeRegardStep(current, spoken, Exiled);
			if (step == RegardStep.Expulsion)
			{
				Exile(KingdomExileRules.DeedClause(ReputationType), Forced: false, out var _);
				return;
			}
			RegardSpoken = (int)KingdomExileRules.RememberedRegard(current, spoken);
			if (step == RegardStep.Nothing)
			{
				return;
			}
			// Nonmodal on purpose: this is the city talking about you, not the city stopping you.
			XRL.Messages.MessageQueue.AddPlayerMessage(KingdomExileRules.RegardSpeech(step,
				KingdomPresentation.Rich(SeatName)));
			KingdomChronicle.Record(this, KingdomExileRules.RegardChronicle(step,
				KingdomPresentation.Rich(SeatName)));
		}

		/// <summary>
		/// What the old realm's ground has to say to a founder standing on it after being put out:
		/// the question, if it will hear it; why it will not, if it will not; and the closed door,
		/// once, to a founder who has since poured somewhere else.
		/// </summary>
		/// <param name="Z">The activated zone. Null is tolerated.</param>
		private void OnZoneActivatedWhileExiled(Zone Z)
		{
			if (!Exiled || Z == null || !ExiledRealmHolds(Z.ZoneID))
			{
				return;
			}
			if (Founded)
			{
				if (!DoorClosedTold)
				{
					DoorClosedTold = true;
					XRL.Messages.MessageQueue.AddPlayerMessage(KingdomExileRules.DoorClosedLine(
						KingdomPresentation.Rich(ExiledDisplayName),
						KingdomPresentation.Rich(KingdomDisplayName)));
				}
				return;
			}
			int regard = ExiledRealmRegard();
			KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
			if (succession != null && succession.ChosenSeatBlocksReturn(this,
				out string chosenSeatRefusal))
			{
				if (regard <= ReturnAskedRegard) return;
				ReturnAskedRegard = regard;
				XRL.Messages.MessageQueue.AddPlayerMessage(chosenSeatRefusal);
				return;
			}
			// Nothing is said again until the founder has actually changed the realm's mind about
			// them. A founder who walks away from the question is never asked it twice for free,
			// and a founder who ignores the whole feature is never spoken to at all.
			if (regard <= ReturnAskedRegard)
			{
				return;
			}
			ReturnAskedRegard = regard;
			ReturnVerdict verdict = KingdomExileRules.JudgeReturn(Exiled, Founded, ExiledRealmKeptGround, true, regard);
			if (verdict != ReturnVerdict.Allowed)
			{
				XRL.Messages.MessageQueue.AddPlayerMessage(KingdomExileRules.ReturnRefusal(verdict,
					KingdomPresentation.Rich(ExiledDisplayName),
					KingdomPresentation.Rich(KingdomDisplayName)));
				return;
			}
			if (Popup.ShowYesNo("You are standing in {{C|" + KingdomPresentation.Rich(ExiledDisplayName) + "}}, which put you out.\n\nAsk to be taken back?") != DialogResult.Yes)
			{
				XRL.Messages.MessageQueue.AddPlayerMessage("You say nothing, and nobody asks you to.");
				return;
			}
			if (!TryReturn(Z, out var refusal))
			{
				Popup.Show(refusal);
			}
		}

		/// <summary>
		/// The founder's reputation with a named faction, tolerating a name no faction answers to.
		/// <c>Factions.Get</c> throws on an unknown name, which inside event dispatch would cost
		/// the whole step; <c>GetIfExists</c> and the null-tolerant reputation overload degrade to
		/// 0 instead.
		/// </summary>
		private static int RegardWith(string FactionName)
		{
			if (string.IsNullOrEmpty(FactionName))
			{
				return 0;
			}
			return The.Game.PlayerReputation.Get(Factions.GetIfExists(FactionName));
		}

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
