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

		public override bool WantFieldReflection => false;

		public override void Write(SerializationWriter Writer)
		{
			SerializationVersion = CurrentSerializationVersion;
			// Named-field serializer writes compatibility field as stored data, not a property.
			// Refresh immediately before every save, including a save cut through an open receipt.
			SynchronizeLegacyManifestProjection();
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomSystem));
		}

		/// <summary>
		/// Reads kingdom state, tolerating every layout this mod has ever written.
		/// <para>
		/// Two regimes meet here. Saves written before named fields arrived were emitted by the
		/// engine's positional reflection, so the engine has already filled every field by the
		/// time we are called &mdash; including <see cref="SerializationVersion"/>, which is how we
		/// recognise them. Nothing remains in the block to read, so we return.
		/// </para>
		/// <para>
		/// Named-field saves are self-describing: a reader may meet a field it does not know, and
		/// may miss one it expects, without either being an error. Any named-field version from
		/// the first through ours is therefore readable. Older positional versions and saves from
		/// a <i>newer</i> build are genuinely beyond this path.
		/// </para>
		/// <para>
		/// Throwing is the only way to reach the engine's block-skip recovery, so an unreadable
		/// save must throw &mdash; but it flags <see cref="LoadFailed"/> first, because the engine
		/// swallows the exception and hands back a blank system. Without the flag the founder's
		/// settlement would simply be gone, unremarked. See <see cref="ReportLoadFailure"/>.
		/// </para>
		/// </summary>
		public override void Read(SerializationReader Reader)
		{
			try
			{
				if (SerializationVersion == LegacyReflectedSerializationVersion)
				{
					SerializationVersion = CurrentSerializationVersion;
					NormalizeState(AllowLegacyIdentityMigration: true);
					return;
				}
				int magic = Reader.ReadInt32();
				if (magic != SerializationMagic)
				{
					throw new InvalidOperationException("Invalid ThousandAndFirst kingdom save marker.");
				}
				int version = Reader.ReadInt32();
				if (version < FirstNamedSerializationVersion || version > CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst kingdom save version " + version + "; this build reads named versions " + FirstNamedSerializationVersion + " through " + CurrentSerializationVersion + ".");
				}
				Reader.ReadNamedFields(this, typeof(KingdomSystem));
				SerializationVersion = CurrentSerializationVersion;
				NormalizeState(AllowLegacyIdentityMigration: false);
			}
			catch
			{
				LoadFailed = true;
				throw;
			}
		}

		/// <summary>
		/// Tells the founder, once, that the records could not be read. The engine catches
		/// deserialization failures and carries on with a blank system, so without this the loss
		/// would be visible only in the metrics log &mdash; the player would find the settlement
		/// unfounded and no reason given.
		/// </summary>
		private void ReportLoadFailure()
		{
			LoadFailed = false;
			MetricsManager.LogError("ThousandAndFirst: kingdom state could not be read; the settlement has been reset.");
			Popup.Show("The founding records cannot be read. Whatever kingdom you held is not recorded in this save, and the founding must begin again.\n\nYour game is otherwise unharmed.");
		}

		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			// The research registry and everything it caches about the world are process statics,
			// so a second game in the same session would otherwise read the first one's quest
			// verdicts and believe its journal notes were already filed.
			KingdomResearch.Reload();
			NormalizeState(AllowLegacyIdentityMigration: false);
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
			// The true last read (LIVING-CITY-ARCHITECTURE §3.4). ZoneDeactivatedEvent is only a
			// hint: a deactivated zone goes on simulating for up to forty more turns, so a reading
			// taken there would be wrong by whatever happened in the grace window. This fires from
			// SuspendZone BEFORE Suspended is set, for any zone, while its objects are still in RAM.
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
