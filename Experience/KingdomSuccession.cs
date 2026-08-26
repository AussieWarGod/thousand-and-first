using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// Kingdom Mode's one death-time crossover. It is an <see cref="IPlayerSystem"/> so the
	/// <see cref="AfterDieEvent"/> registration follows whichever real body is the player, and it
	/// stages the priced interregnum and physical mourning procession synchronously before
	/// <c>GameObject.Die</c> asks <c>IsPlayer</c> again. Qud exposes no cancellable later seam: this
	/// is a checkpointed physical ceremony inside one death callback, not simulated async turns.
	/// </summary>
	[Serializable]
	public sealed class KingdomSuccession : IPlayerSystem
	{
		private const int SerializationMagic = 1414746963;
		private const int CurrentSerializationVersion = 2;
		private const int MaxSealAccessionTokenChars = KingdomSuccessionRules.MaxDeathTokenChars;
		private const int MaxPendingRiteChronicleChars = 2048;
		private const int MaxPendingRepairCreedsChars = 1024;

		private int SerializationVersion = CurrentSerializationVersion;
		private int SuccessionOrdinal;
		private string PendingDeathToken;
		private string CompletedDeathToken;
		private InterregnumPhase PendingPhase;
		private long PendingDueTick;
		private NewsRoad PendingRoad;
		private int PendingDays;
		private string PendingSealAccessionToken;
		private string PendingSealRiteChronicle;
		private bool PendingSealAccessionReady;
		private int PendingAccessionRepairResidentId;
		private string PendingAccessionRepairFounderName;
		private string PendingAccessionRepairHeirName;
		private bool PendingAccessionRepairSeated;
		private long PendingAccessionRepairArrivedTick;
		private string PendingAccessionRepairKeptCreeds;
		private MourningRiteStage PendingRiteStage;
		private string PendingFounderName;
		private string PendingFounderObjectId;
		private string PendingFounderCause;
		private int PendingHeirResidentId;
		private string PendingHeirObjectId;
		private string PendingHeirName;
		private string PendingHeirZoneId;
		private string PendingRiteZoneId;
		private string PendingRiteCityName;
		private string PendingRiteFixtureObjectId;
		private string PendingRiteFixtureName;
		private int PendingShrineX;
		private int PendingShrineY;
		private string PendingRiteAttendeeManifest;
		private string PendingShrineObjectId;
		private string CompletedShrineToken;
		private string CompletedShrineObjectId;
		private string CompletedShrineZoneId;
		private bool LegacyPhysicalRiteUnavailable;
		private bool SuccessionDisabled;

		[NonSerialized]
		private bool LoadFailed;

		[NonSerialized]
		private bool DeathChroniclePublished;

		[NonSerialized]
		private bool AccessionOwnershipCommitted;

		/// <summary>Native-test seam: a harness may snapshot/save at an exact durable checkpoint.
		/// It must not mutate runtime authority; production leaves it null.</summary>
		[NonSerialized]
		internal static Action<MourningRiteStage> InjectedCheckpoint = null;

		public override bool WantFieldReflection => false;

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterGameLoadedEvent.ID);
		}

		public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterDieEvent.ID);
		}

		public override bool HandleEvent(AfterDieEvent E)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(system))
			{
				return base.HandleEvent(E);
			}
			DeathChroniclePublished = false;
			AccessionOwnershipCommitted = false;
			try
			{
				HandleFounderDeath(E);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession failed closed", ex);
				KingdomLog.Log("succession: failed closed (" + ex.GetType().Name + ": " + ex.Message + ")");
				bool founderStillControls = ReferenceEquals(The.Player, E?.Dying);
				if (!AccessionOwnershipCommitted && founderStillControls)
				{
					TryTerminalAfterOuterFailure(E);
					TryTellFailure("The charter could not be carried through the mourning rite.\n\nThe line ends here.");
				}
				else
				{
					TryCompletePendingSealAccession("post-accession exception");
					TryTellFailure("Control left the founder, but a later accession step failed. The line was not ended; any complete profile handoff stays queued for a safe retry.");
				}
			}
			finally
			{
				DeathChroniclePublished = false;
				AccessionOwnershipCommitted = false;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			if (!KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled))
			{
				KingdomLog.Log("succession: saved state could not be read; succession is disabled for this save");
				TryTellFailure("The kingdom's succession record could not be read. No death will be redirected by it in this save.");
			}
			else
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.AutomaticWorkAllowed(system))
					return base.HandleEvent(E);
				TryResumePendingRite("game load");
				TryCompletePendingAccessionRepair("game load");
				TryCompletePendingSealAccession("game load");
			}
			return base.HandleEvent(E);
		}

		public override void BeforeSave()
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (KingdomMaster.AutomaticWorkAllowed(system)
				&& KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled))
			{
				TryCompletePendingAccessionRepair("BeforeSave");
				TryCompletePendingSealAccession("BeforeSave");
			}
		}

		public override void Write(SerializationWriter Writer)
		{
			SuccessionDisabled = SuccessionDisabled || LoadFailed;
			if (SuccessionDisabled)
			{
				ClearDisabledSavedState();
			}
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomSuccession),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(SerializationReader Reader)
		{
			try
			{
				int magic = Reader.ReadInt32();
				int version = Reader.ReadInt32();
				if (magic != SerializationMagic || version < 1
					|| version > CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst succession save block.");
				}
				Reader.ReadNamedFields(this, typeof(KingdomSuccession),
					BindingFlags.Instance | BindingFlags.NonPublic);
				if (SerializationVersion != version)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst succession named-field version.");
				}
				MigrateSavedState(version);
				SerializationVersion = CurrentSerializationVersion;
				ValidateSavedState();
			}
			catch
			{
				LoadFailed = true;
				SuccessionDisabled = true;
				throw;
			}
		}

		/// <summary>Reveals only notes stamped by one exact founder death, then adds one exact,
		/// deduplicated giver-location note for each still-open quest. Quest state remains entirely
		/// game-scoped and untouched; the corpse contributes memory and navigation only.</summary>
		internal bool TryRestoreFounderKnowledge(string DeathToken, string FounderName,
			out int Revealed, out int QuestMarks)
		{
			Revealed = 0;
			QuestMarks = 0;
			if (string.IsNullOrEmpty(DeathToken))
			{
				return false;
			}
			string attribute = KingdomSuccessionRules.FounderAttribute(DeathToken);
			foreach (IBaseJournalEntry entry in JournalAPI.GetAllNotes())
			{
				if (entry == null || entry.Revealed || entry.Attributes == null
					|| !entry.Attributes.Contains(attribute))
				{
					continue;
				}
				entry.Reveal("the remains of " + (string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName), Silent: true);
				if (entry.Revealed)
				{
					Revealed++;
				}
			}
			QuestMarks = RestoreQuestOriginMarks(DeathToken, FounderName);
			return true;
		}

		private static int RestoreQuestOriginMarks(string DeathToken, string FounderName)
		{
			XRLGame game = The.Game;
			if (game == null || game.Quests == null) return 0;
			int marked = 0;
			foreach (KeyValuePair<string, Quest> pair in game.Quests)
			{
				Quest quest = pair.Value;
				if (quest == null || quest.Finished
					|| string.IsNullOrEmpty(quest.QuestGiverLocationZoneID)) continue;
				string questId = string.IsNullOrEmpty(quest.ID) ? pair.Key : quest.ID;
				if (string.IsNullOrEmpty(questId)) questId = quest.Name;
				string secretId = KingdomSuccessionRules.QuestOriginSecretId(
					DeathToken, questId);
				if (string.IsNullOrEmpty(secretId))
				{
					KingdomLog.Log("succession: an open quest origin exceeded its identity bound");
					continue;
				}
				try
				{
					JournalMapNote note = JournalAPI.GetMapNote(secretId);
					if (note == null)
					{
						JournalAPI.AddMapNote(quest.QuestGiverLocationZoneID,
							KingdomSuccessionRules.QuestMarkNote(quest.Name,
								quest.QuestGiverName), "general",
							new string[]
							{
								KingdomSuccessionRules.FounderAttribute(DeathToken),
								KingdomSuccessionRules.QuestOriginAttribute
							}, secretId, revealed: true, sold: false, time: -1L, silent: true);
						note = JournalAPI.GetMapNote(secretId);
					}
					if (note == null || !string.Equals(note.ZoneID,
						quest.QuestGiverLocationZoneID, StringComparison.Ordinal)
						|| note.Attributes == null
						|| !note.Attributes.Contains(KingdomSuccessionRules.QuestOriginAttribute))
					{
						KingdomLog.Log("succession: a quest-origin secret identity conflicted; the existing note was left untouched");
						continue;
					}
					if (!note.Revealed)
					{
						note.Reveal("the remains of " + (string.IsNullOrEmpty(FounderName)
							? "the founder" : FounderName), Silent: true);
					}
					if (note.Revealed) marked++;
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: quest-origin map note failed", ex);
					KingdomLog.Log("succession: one quest-origin map note failed ("
						+ ex.GetType().Name + ")");
				}
			}
			return marked;
		}

		private void HandleFounderDeath(AfterDieEvent E)
		{
			XRLGame game = The.Game;
			GameObject founder = E?.Dying;
			KingdomSystem system = game?.GetSystem<KingdomSystem>();
			if (game == null || founder == null
				|| !KingdomMaster.AutomaticWorkAllowed(system)
				|| !KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled)
				|| !ReferenceEquals(The.Player, founder))
			{
				return;
			}
			bool mode = KingdomSuccessionRules.ModeOn(game.gameMode,
				game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey));
			if (!mode)
			{
				return;
			}
			if (system == null || !system.Founded)
			{
				return;
			}

			string founderName = founder.BaseDisplayNameStripped;
			string founderCause = DeathCause(E);
			if (string.IsNullOrEmpty(founderName)
				|| founderName.Length > KingdomSealRecord.MaxNameChars
				|| string.IsNullOrEmpty(founderCause)
				|| founderCause.Length > MaxPendingRiteChronicleChars)
			{
				KingdomLog.Log("succession: exact founder name/cause exceeded its persistence bound");
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if (PendingAccessionRepairResidentId != 0)
			{
				TryCompletePendingAccessionRepair("before another death");
				if (PendingAccessionRepairResidentId != 0)
				{
					KingdomLog.Log("succession: unresolved accession repair could not precede another death");
					PublishFounderDeath(system, founderName, E);
					EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
					return;
				}
			}
			if (!string.IsNullOrEmpty(PendingSealAccessionToken))
			{
				TryCompletePendingSealAccession("before another death");
				if (!string.IsNullOrEmpty(PendingSealAccessionToken))
				{
					PublishFounderDeath(system, founderName, E);
					EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
					return;
				}
			}
			string founderId = founder.ID;
			long deathTick = game.TimeTicks < 0L ? 0L : game.TimeTicks;
			string token = KingdomSuccessionRules.FounderDeathToken(
				SuccessionOrdinal + 1, deathTick, founderId);
			if (string.IsNullOrEmpty(token) || token.Length > MaxSealAccessionTokenChars)
			{
				KingdomLog.Log("succession: exact founder-death token exceeded its persistence bound");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			SuccessionAttemptVerdict attempt = KingdomSuccessionRules.JudgeAttempt(
				token, PendingDeathToken, CompletedDeathToken);
			if (attempt != SuccessionAttemptVerdict.Begin)
			{
				KingdomLog.Log("succession: death ignored by idempotence gate " + attempt + " token=" + token);
				return;
			}

			List<HeirRuntime> heirs;
			if (!TryReadHeirs(system, out heirs))
			{
				KingdomLog.Log("succession: the complete resident law could not be read; no partial roll was used");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomHeir[] candidates = new KingdomHeir[heirs.Count];
			for (int i = 0; i < heirs.Count; i++)
			{
				candidates[i] = heirs[i].Rule;
			}
			int chosenIndex;
			if (!KingdomSuccessionRules.TryChooseHeir(candidates, SuccessionLaw.Seniority, null, out chosenIndex))
			{
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.NoHeir, E);
				return;
			}

			HeirRuntime chosen = heirs[chosenIndex];
			if ((chosen.Rule.KeptCreeds ?? "").Length > MaxPendingRepairCreedsChars)
			{
				KingdomLog.Log("succession: chosen heir's creed history exceeded the repair bound");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			GameObject heirBody;
			string heirZoneId;
			if (!KingdomResidents.TryResolveBoundBody(system, chosen.Rule.ResidentId, LoadZone: true,
				out heirBody, out heirZoneId))
			{
				KingdomLog.Log("succession: law chose resident " + chosen.Rule.ResidentId + " ("
					+ chosen.Rule.Name + "), but that exact bound body was unreachable; no substitute was tried");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if (!GameObject.Validate(heirBody) || !heirBody.IsAlive || heirBody.CurrentCell == null
				|| ReferenceEquals(heirBody, founder))
			{
				KingdomLog.Log("succession: exact heir failed final body/cell validation; no substitute was tried");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomCityBook heirBook;
			int heirResidentId;
			if (!KingdomResidents.TryLocate(system, heirBody, out heirBook, out heirResidentId)
				|| heirResidentId != chosen.Rule.ResidentId)
			{
				KingdomLog.Log("succession: exact heir lost its resident row after body resolution");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			string citizenshipFailure;
			if (!KingdomCitizenship.CanRemove(system, heirBody, out citizenshipFailure))
			{
				KingdomLog.Log("succession: exact heir has no reversible citizenship boundary ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			bool heirWasSeated = ReferenceEquals(heirBook, system.City);
			string riteCityName = heirWasSeated ? system.SeatName
				: (system.Away?.SettlementName ?? system.SeatName);
			KingdomSuccessionRite.Plan ritePlan;
			string riteFailure;
			if (!KingdomSuccessionRite.TryFreeze(system, heirBook, heirBody, riteCityName,
				out ritePlan, out riteFailure))
			{
				KingdomLog.Log("succession: physical rite preflight refused (" + riteFailure + ")");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if ((ritePlan.CityName ?? "").Length > KingdomSealRecord.MaxNameChars
				|| (ritePlan.FixtureName ?? "").Length > KingdomSealRecord.MaxNameChars)
			{
				KingdomLog.Log("succession: exact rite locus names exceeded their persistence bound");
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}

			int newsDays;
			NewsRoad newsRoad;
			JudgeActualNews(system, founder.CurrentZone, out newsDays, out newsRoad);
			long dueTick = KingdomSuccessionRules.NewsDueTick(deathTick, newsDays);
			bool heldOffice = chosen.Rule.HoldsOffice;
			string heirCreed = heirBody.GetStringProperty(KingdomCreed.CreedProperty);

			PendingDeathToken = token;
			PendingPhase = InterregnumPhase.WordOnTheRoad;
			PendingDueTick = dueTick;
			PendingRoad = newsRoad;
			PendingDays = newsDays;
			LegacyPhysicalRiteUnavailable = false;
			PendingFounderName = founderName;
			PendingFounderObjectId = founder.IDIfAssigned;
			PendingFounderCause = founderCause;
			PendingHeirResidentId = chosen.Rule.ResidentId;
			PendingHeirObjectId = heirBody.IDIfAssigned;
			PendingHeirName = chosen.Rule.Name;
			PendingHeirZoneId = heirZoneId;
			PendingRiteZoneId = ritePlan.ZoneId;
			PendingRiteCityName = ritePlan.CityName;
			PendingRiteFixtureObjectId = ritePlan.FixtureObjectId;
			PendingRiteFixtureName = ritePlan.FixtureName;
			PendingShrineX = ritePlan.ShrineX;
			PendingShrineY = ritePlan.ShrineY;
			PendingRiteAttendeeManifest = ritePlan.Manifest;
			PendingShrineObjectId = "";
			Checkpoint(MourningRiteStage.Frozen);
			r_KingdomFounderRemains remains = new r_KingdomFounderRemains(token, founderName);
			founder.AddPart(remains);

			long advance = KingdomSuccessionRules.WorldTicksUntilDue(game.TimeTicks, dueTick);
			if (advance > 0L)
			{
				game.TimeTicks = dueTick;
			}
			PendingPhase = InterregnumPhase.RiteDue;
			Checkpoint(MourningRiteStage.WordArrived);

			GameObject walkedHeir;
			if (!KingdomSuccessionRite.TryHoldProcession(system, token, PendingRiteZoneId,
				PendingRiteFixtureObjectId, PendingRiteAttendeeManifest,
				out walkedHeir, out riteFailure)
				|| !ReferenceEquals(walkedHeir, heirBody)
				|| walkedHeir.GetIntProperty(KingdomResidents.ResidentIdProperty)
					!= PendingHeirResidentId
				|| !string.Equals(walkedHeir.IDIfAssigned, PendingHeirObjectId,
					StringComparison.Ordinal))
			{
				KingdomLog.Log("succession: physical procession refused (" + riteFailure + ")");
				AbortPendingBeforeTransfer(founder, remains);
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			Checkpoint(MourningRiteStage.ProcessionComplete);

			string shrineHistory = KingdomSuccessionRules.FounderEpitaph(
				KingdomPresentation.Rich(founderName),
				KingdomPresentation.Rich(PendingRiteCityName),
				KingdomPresentation.Rich(system.FoundingRegionName),
				KingdomPresentation.Rich(PendingFounderCause))
				+ " The named residents present walked to "
				+ KingdomPresentation.Rich(PendingRiteFixtureName)
				+ " and held the mourning rite here.";
			GameObject founderShrine;
			if (!KingdomSuccessionRite.TryEnsureFounderShrine(token, founderName, deathTick,
				PendingFounderCause, shrineHistory, PendingRiteCityName, PendingRiteZoneId,
				PendingRiteFixtureObjectId, PendingShrineX, PendingShrineY,
				PendingShrineObjectId, out founderShrine, out riteFailure))
			{
				KingdomLog.Log("succession: founder shrine refused (" + riteFailure + ")");
				AbortPendingBeforeTransfer(founder, remains);
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			PendingShrineObjectId = founderShrine.IDIfAssigned;
			CompletedShrineToken = token;
			CompletedShrineObjectId = PendingShrineObjectId;
			CompletedShrineZoneId = PendingRiteZoneId;
			Checkpoint(MourningRiteStage.ShrinePlaced);

			// After SetBody returns and the explicit global player-system sweep succeeds, no mod
			// action dispatches or yields before the prebuilt resident snapshots are published.
			KingdomResidentRow formerRow = default(KingdomResidentRow);
			// Unknown is repair-required. RefusedClean is trustworthy only when TryAccede
			// returns it after re-reading both exact original carriers, or when the
			// publication boundary was never entered at all.
			KingdomAccessionOutcome accession = KingdomAccessionOutcome.RepairRequired;
			bool accessionSeated = heirWasSeated;
			bool founderRestored = false;
			bool heirContinuationRegistrationsExact = false;
			// Procession and shrine callbacks can advance world state after early preflight.
			// Re-prove exact reversible citizenship immediately before irreversible body transfer.
			if (!KingdomCitizenship.CanRemove(system, heirBody, out citizenshipFailure))
			{
				KingdomLog.Log("succession: exact heir citizenship changed before body transfer ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				AbortPendingBeforeTransfer(founder, remains);
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomPlayerBodyTransfer forward = SetPlayerBodyAndRebindAll(game, founder,
				heirBody, "accession");
			if (forward.TargetControls)
			{
				Checkpoint(MourningRiteStage.BodyCrossed);
			}
			if (forward.MayPublishAccession)
			{
				heirContinuationRegistrationsExact = true;
				try
				{
					accession = KingdomAccessionOutcome.RepairRequired;
					accession = KingdomResidents.TryAccede(system, heirBody,
						out formerRow, out accessionSeated);
					if (accession == KingdomAccessionOutcome.RefusedClean)
					{
						accession = KingdomAccessionOutcome.RepairRequired;
						accession = KingdomResidents.TryAccede(system, heirBody,
							out formerRow, out accessionSeated);
					}
					if (accession == KingdomAccessionOutcome.RepairRequired
						&& formerRow.ResidentId == chosen.Rule.ResidentId)
					{
						accession = KingdomAccessionOutcome.RepairRequired;
						accession = KingdomResidents.TryRepairAccession(system, heirBody,
							chosen.Rule.ResidentId, accessionSeated, formerRow.Name,
							formerRow.ArrivedTick, formerRow.KeptCreeds, out formerRow);
					}
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: accession publish retry failed", ex);
				}
			}
			else
			{
				// Any thrown, short-circuited, misdirected, or incompletely rebound forward
				// transfer is not an accession. No resident carrier has been touched yet.
				KingdomLog.Log("succession: forward body transfer was not a clean globally rebound heir transfer; restoring founder control");
				KingdomPlayerBodyTransfer rollback = SetPlayerBodyAndRebindAll(game, heirBody,
					founder, "accession rollback");
				founderRestored = rollback.TargetControls;
				heirContinuationRegistrationsExact = rollback.OriginalControls
					&& rollback.RegistrationsExact;
				accession = founderRestored ? KingdomAccessionOutcome.RefusedClean
					: KingdomAccessionOutcome.RepairRequired;
			}
			if (accession != KingdomAccessionOutcome.Committed)
			{
				if (accession == KingdomAccessionOutcome.RepairRequired)
				{
					if (!KingdomSuccessionRules.MayQueueAccessionRepair(
						ReferenceEquals(The.Player, heirBody),
						heirContinuationRegistrationsExact))
					{
						FailCatastrophicBodyTransfer(system, founder, founderName, remains, E,
							"the failed body transfer ended on neither a globally rebound founder nor the exact heir");
						return;
					}
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(chosen.Rule, founderName, accessionSeated);
					TryPrepareRepairableHeir(heirBody);
					KingdomLog.Log("succession: accession carriers need repair; control remains with the heir");
					TryTellFailure("Control passed from the founder, but the resident accession carriers did not converge. The line remains open and the exact repair is queued.");
					return;
				}
				KingdomLog.Log("succession: CRITICAL accession publish failed immediately after SetBody; rolling control back to the dying founder");
				if (!founderRestored)
				{
					KingdomPlayerBodyTransfer rollback = SetPlayerBodyAndRebindAll(game,
						heirBody, founder, "accession rollback");
					founderRestored = rollback.TargetControls;
					heirContinuationRegistrationsExact = rollback.OriginalControls
						&& rollback.RegistrationsExact;
				}
				if (!KingdomSuccessionRules.MayTerminalAfterAccessionFailure(
					accession == KingdomAccessionOutcome.RefusedClean, founderRestored))
				{
					if (!KingdomSuccessionRules.MayQueueAccessionRepair(
						ReferenceEquals(The.Player, heirBody),
						heirContinuationRegistrationsExact))
					{
						FailCatastrophicBodyTransfer(system, founder, founderName, remains, E,
							"the clean resident refusal could not restore founder control or prove the exact heir");
						return;
					}
					// SetBody may change control and then throw. Never terminalize a lineage after
					// control left the dying founder; resident-law repair remains a separate task.
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(chosen.Rule, founderName, accessionSeated);
					TryPrepareRepairableHeir(heirBody);
					KingdomLog.Log("succession: CRITICAL founder control could not be restored; line remains open for accession repair");
					TryTellFailure("Control passed from the founder, but the resident accession record could not be published or rolled back. The line remains open and requires repair.");
					return;
				}
				AbortPendingBeforeTransfer(founder, remains);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}

			CompleteAccession(game, system, heirBody, founderName, formerRow, token,
				newsRoad, newsDays, heldOffice, heirCreed, heirZoneId, "accession");
		}

		private void FailCatastrophicBodyTransfer(KingdomSystem System, GameObject Founder,
			string FounderName, r_KingdomFounderRemains Remains, AfterDieEvent Death,
			string Reason)
		{
			// A third or unproved controller is not the chosen heir. Never aim the persisted
			// resident roll-forward token at that body. End and disable this succession record.
			AccessionOwnershipCommitted = true;
			SuccessionDisabled = true;
			PendingDeathToken = "";
			PendingPhase = InterregnumPhase.None;
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
			ClearPendingRiteIdentity();
			try
			{
				Founder?.RemovePart(Remains);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic succession remains cleanup failed", ex);
			}
			PublishFounderDeath(System, FounderName, Death);
			try
			{
				KingdomChronicle.Record(System,
					KingdomSuccessionRules.DynastyEndChronicle(KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(FounderName)));
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic dynasty-end chronicle failed", ex);
			}
			try
			{
				string failure;
				if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out failure))
				{
					KingdomLog.Log("succession: catastrophic terminal seal attempt failed closed ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: catastrophic terminal seal attempt threw", ex);
			}
			KingdomLog.Log("succession: CATASTROPHIC body-transfer refusal; succession disabled ("
				+ Reason + ")");
			TryTellFailure("The body transfer ended in an unproved controller state. The dynasty has ended, succession is disabled for this save, and no resident identity was applied to the uncontrolled body.");
		}

		private void AbortPendingBeforeTransfer(GameObject Founder,
			r_KingdomFounderRemains Remains)
		{
			try
			{
				Founder?.RemovePart(Remains);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: founder-remains rollback failed", ex);
			}
			PendingDeathToken = "";
			PendingPhase = InterregnumPhase.None;
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			ClearPendingRiteIdentity();
		}

		private void ClearPendingRiteIdentity()
		{
			PendingRiteStage = MourningRiteStage.None;
			PendingFounderName = "";
			PendingFounderObjectId = "";
			PendingFounderCause = "";
			PendingHeirResidentId = 0;
			PendingHeirObjectId = "";
			PendingHeirName = "";
			PendingHeirZoneId = "";
			PendingRiteZoneId = "";
			PendingRiteCityName = "";
			PendingRiteFixtureObjectId = "";
			PendingRiteFixtureName = "";
			PendingShrineX = 0;
			PendingShrineY = 0;
			PendingRiteAttendeeManifest = "";
			PendingShrineObjectId = "";
		}

		private void Checkpoint(MourningRiteStage Stage)
		{
			if (!KingdomSuccessionRules.MayAdvanceRite(PendingRiteStage, Stage))
			{
				throw new InvalidOperationException("The mourning rite attempted to skip a physical checkpoint.");
			}
			PendingRiteStage = Stage;
			InjectedCheckpoint?.Invoke(Stage);
		}

		/// <summary>Cold-load recovery exists for debugger/injected saves only. Native Qud cannot
		/// save between AfterDie and Die's immediate IsPlayer recheck, but a checkpoint that does
		/// exist must either re-prove exact physical evidence or remain fail-closed.</summary>
		private void TryResumePendingRite(string Context)
		{
			if (string.IsNullOrEmpty(PendingDeathToken)
				|| PendingRiteStage == MourningRiteStage.None
				|| PendingAccessionRepairResidentId != 0)
			{
				return;
			}
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				bool alreadyCrossed = PendingRiteStage == MourningRiteStage.BodyCrossed;
				GameObject founder = alreadyCrossed ? null : The.Player;
				if (game == null || system == null || (!alreadyCrossed && (founder == null
					|| !string.Equals(founder.IDIfAssigned, PendingFounderObjectId,
						StringComparison.Ordinal))))
				{
					QuarantinePendingRite(Context, "the exact controlled founder is absent");
					return;
				}
				GameObject heir = alreadyCrossed ? The.Player : null;
				string boundZone;
				bool heirExact = alreadyCrossed
					? GameObject.Validate(heir) && heir.IsPlayer()
						&& heir.GetIntProperty(KingdomResidents.ResidentIdProperty) == PendingHeirResidentId
						&& string.Equals(heir.IDIfAssigned, PendingHeirObjectId, StringComparison.Ordinal)
						&& string.Equals(heir.CurrentZone?.ZoneID, PendingHeirZoneId, StringComparison.Ordinal)
					: KingdomResidents.TryResolveBoundBody(system, PendingHeirResidentId, true,
						out heir, out boundZone)
						&& string.Equals(heir.IDIfAssigned, PendingHeirObjectId, StringComparison.Ordinal)
						&& string.Equals(boundZone, PendingHeirZoneId, StringComparison.Ordinal);
				if (!heirExact)
				{
					QuarantinePendingRite(Context, "the exact frozen heir is absent");
					return;
				}

				if (PendingRiteStage == MourningRiteStage.Frozen)
				{
					if (KingdomSuccessionRules.WorldTicksUntilDue(game.TimeTicks, PendingDueTick) > 0L)
					{
						game.TimeTicks = PendingDueTick;
					}
					PendingPhase = InterregnumPhase.RiteDue;
					Checkpoint(MourningRiteStage.WordArrived);
				}
				if (PendingRiteStage == MourningRiteStage.WordArrived)
				{
					GameObject walked;
					string failure;
					if (!KingdomSuccessionRite.ProcessionEvidence(system, PendingDeathToken,
						PendingRiteZoneId, PendingRiteFixtureObjectId,
						PendingRiteAttendeeManifest, out walked)
						&& !KingdomSuccessionRite.TryHoldProcession(system, PendingDeathToken,
							PendingRiteZoneId, PendingRiteFixtureObjectId,
							PendingRiteAttendeeManifest, out walked, out failure))
					{
						QuarantinePendingRite(Context, failure);
						return;
					}
					if (!ReferenceEquals(walked, heir))
					{
						QuarantinePendingRite(Context, "procession evidence names another body");
						return;
					}
					Checkpoint(MourningRiteStage.ProcessionComplete);
				}
				if (PendingRiteStage == MourningRiteStage.ProcessionComplete)
				{
					GameObject proved;
					if (!KingdomSuccessionRite.ProcessionEvidence(system, PendingDeathToken,
						PendingRiteZoneId, PendingRiteFixtureObjectId,
						PendingRiteAttendeeManifest, out proved)
						|| !ReferenceEquals(proved, heir))
					{
						QuarantinePendingRite(Context, "completed procession evidence is absent");
						return;
					}
					int ordinal;
					long deathTick;
					KingdomSuccessionRules.TryReadDeathToken(PendingDeathToken,
						out ordinal, out deathTick);
					string history = KingdomSuccessionRules.FounderEpitaph(
						KingdomPresentation.Rich(PendingFounderName),
						KingdomPresentation.Rich(PendingRiteCityName),
						KingdomPresentation.Rich(system.FoundingRegionName),
						KingdomPresentation.Rich(PendingFounderCause))
						+ " The named residents walked to "
						+ KingdomPresentation.Rich(PendingRiteFixtureName)
						+ " and held the mourning rite here.";
					GameObject shrine;
					string failure;
					if (!KingdomSuccessionRite.TryEnsureFounderShrine(PendingDeathToken,
						PendingFounderName, deathTick, PendingFounderCause, history,
						PendingRiteCityName, PendingRiteZoneId, PendingRiteFixtureObjectId,
						PendingShrineX, PendingShrineY, PendingShrineObjectId,
						out shrine, out failure))
					{
						QuarantinePendingRite(Context, failure);
						return;
					}
					PendingShrineObjectId = shrine.IDIfAssigned;
					CompletedShrineToken = PendingDeathToken;
					CompletedShrineObjectId = PendingShrineObjectId;
					CompletedShrineZoneId = PendingRiteZoneId;
					Checkpoint(MourningRiteStage.ShrinePlaced);
				}

				KingdomCityBook book;
				int residentId;
				KingdomCityState city;
				KingdomResidentRow row;
				int rowIndex;
				KingdomCityFault cityFault;
				if ((PendingRiteStage != MourningRiteStage.ShrinePlaced
						&& PendingRiteStage != MourningRiteStage.BodyCrossed)
					|| !KingdomResidents.TryLocate(system, heir, out book, out residentId)
					|| residentId != PendingHeirResidentId || !book.TryRead(out city, out cityFault)
					|| !city.TryResidentIndex(residentId, out rowIndex)
					|| !city.TryResident(rowIndex, out row))
				{
					QuarantinePendingRite(Context, "the frozen resident row cannot cross the rite boundary");
					return;
				}
				int officeId = ReferenceEquals(book, system.City)
					? system.OfficeHolderResidentId : system.Away?.OfficeHolderResidentId ?? 0;
				string legacyOffice = ReferenceEquals(book, system.City)
					? system.OfficeHolderName : system.Away?.OfficeHolderName;
				bool heldOffice = officeId > 0 ? officeId == row.ResidentId
					: string.Equals(legacyOffice, row.Name, StringComparison.Ordinal);
				string heirCreed = heir.GetStringProperty(KingdomCreed.CreedProperty);
				if (!alreadyCrossed)
				{
					string citizenshipFailure;
					if (!KingdomCitizenship.CanRemove(system, heir, out citizenshipFailure))
					{
						QuarantinePendingRite(Context,
							"citizenship preflight failed: " + citizenshipFailure);
						return;
					}
					KingdomPlayerBodyTransfer transfer = SetPlayerBodyAndRebindAll(game, founder,
						heir, "cold-load accession");
					if (!transfer.MayPublishAccession)
					{
						SetPlayerBodyAndRebindAll(game, heir, founder, "cold-load rollback");
						QuarantinePendingRite(Context, "body transfer was not exact");
						return;
					}
					Checkpoint(MourningRiteStage.BodyCrossed);
				}
				KingdomResidentRow former;
				bool seated;
				KingdomAccessionOutcome outcome = KingdomResidents.TryAccede(system, heir,
					out former, out seated);
				if (outcome != KingdomAccessionOutcome.Committed)
				{
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(new KingdomHeir(row.Name, row.ArrivedTick, null,
						row.KeptCreeds, true, heldOffice, row.BoundZoneId, row.ResidentId),
						PendingFounderName, ReferenceEquals(book, system.City));
					TryPrepareRepairableHeir(heir);
					return;
				}
				CompleteAccession(game, system, heir, PendingFounderName, former,
					PendingDeathToken, PendingRoad, PendingDays, heldOffice, heirCreed,
					PendingHeirZoneId, "cold-load accession " + Context);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: cold-load rite recovery failed", ex);
				QuarantinePendingRite(Context, ex.GetType().Name);
			}
		}

		private void QuarantinePendingRite(string Context, string Failure)
		{
			SuccessionDisabled = true;
			KingdomLog.Log("succession: pending rite quarantined during " + Context + " ("
				+ (string.IsNullOrEmpty(Failure) ? "unproved physical evidence" : Failure) + ")");
			TryTellFailure("The saved mourning rite cannot prove its exact heir, residents, fixture, and shrine. Succession is disabled for this save; nothing was substituted or minted.");
		}

		private KingdomPlayerBodyTransfer SetPlayerBodyAndRebindAll(XRLGame Game,
			GameObject Original, GameObject Target, string Context)
		{
			KingdomPlayerBodyTransfer result = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				Original, Target,
				body => Game.Player.SetBody(body),
				() => The.Player,
				Game.Systems,
				(system, body) =>
				{
					IPlayerSystem playerSystem = system as IPlayerSystem;
					if (playerSystem != null)
					{
						playerSystem.RegisterPlayer(body,
							EventUnregistrar.Get(body, playerSystem));
					}
				},
				(system, body) =>
				{
					IPlayerSystem playerSystem = system as IPlayerSystem;
					if (playerSystem != null)
					{
						playerSystem.RegisterPlayer(body,
							EventRegistrar.Get(body, playerSystem));
					}
				});
			if (result.Failure != null)
			{
				MetricsManager.LogError("ThousandAndFirst: " + Context
					+ " body transfer or global player-system rebind failed", result.Failure);
			}
			if (!result.RegistrationsExact)
			{
				KingdomLog.Log("succession: " + Context
					+ " could not prove " + result.RegistrationFailures
					+ " player-system registration operation(s)");
			}
			return result;
		}

		private void QueueAccessionRepair(KingdomHeir Heir, string FounderName, bool Seated)
		{
			PendingPhase = InterregnumPhase.RiteDue;
			PendingAccessionRepairResidentId = Heir.ResidentId;
			PendingAccessionRepairFounderName = BoundPendingName(FounderName);
			PendingAccessionRepairHeirName = BoundPendingName(Heir.Name);
			PendingAccessionRepairSeated = Seated;
			PendingAccessionRepairArrivedTick = Heir.ArrivedTick;
			PendingAccessionRepairKeptCreeds = BoundPendingCreeds(Heir.KeptCreeds);
		}

		private void TryCompletePendingAccessionRepair(string Context)
		{
			if (PendingAccessionRepairResidentId == 0)
			{
				return;
			}
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				GameObject heir = The.Player;
				if (game == null || system == null || !system.Founded
					|| !GameObject.Validate(heir) || !heir.IsAlive)
				{
					KingdomLog.Log("succession: pending accession repair cannot prove its exact controlled resident during "
						+ Context);
					return;
				}
				KingdomResidentRow formerRow = default(KingdomResidentRow);
				KingdomAccessionOutcome outcome = KingdomResidents.TryRepairAccession(system,
					heir, PendingAccessionRepairResidentId, PendingAccessionRepairSeated,
					PendingAccessionRepairHeirName, PendingAccessionRepairArrivedTick,
					PendingAccessionRepairKeptCreeds, out formerRow);
				if (outcome != KingdomAccessionOutcome.Committed)
				{
					KingdomLog.Log("succession: pending accession repair remains after " + Context);
					TryPrepareRepairableHeir(heir);
					return;
				}
				string founderName = string.IsNullOrEmpty(PendingAccessionRepairFounderName)
					? "the founder" : PendingAccessionRepairFounderName;
				string token = PendingDeathToken ?? "";
				bool heldOffice = system.OfficeHolderResidentId > 0
					? system.OfficeHolderResidentId == formerRow.ResidentId
					: string.Equals(system.OfficeHolderName, formerRow.Name,
						StringComparison.Ordinal);
				string heirCreed = heir.GetStringProperty(KingdomCreed.CreedProperty);
				CompleteAccession(game, system, heir, founderName, formerRow, token,
					PendingRoad, PendingDays, heldOffice, heirCreed,
					heir.CurrentZone?.ZoneID ?? "", "accession repair " + Context);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: pending accession repair failed closed", ex);
				KingdomLog.Log("succession: pending accession repair remains after " + Context
					+ " (" + ex.GetType().Name + ")");
			}
		}

		private void CompleteAccession(XRLGame Game, KingdomSystem System, GameObject Heir,
			string FounderName, KingdomResidentRow FormerRow, string Token, NewsRoad Road,
			int Days, bool HeldOffice, string HeirCreed, string HeirZoneId, string Context)
		{
			// Player body and resident law have committed. No later presentation, knowledge,
			// chronicle, or profile failure may declare the line ended.
			AccessionOwnershipCommitted = true;
			PendingPhase = InterregnumPhase.Reigning;
			CompletedDeathToken = Token;
			SuccessionOrdinal++;
			PendingSealAccessionToken = Token;
			PendingSealAccessionReady = false;
			string shownHeir = KingdomPresentation.Rich(FormerRow.Name);
			PendingSealRiteChronicle = BoundPendingRite("the charter passed from "
				+ KingdomPresentation.Rich(FounderName) + " to " + shownHeir + " at "
				+ KingdomPresentation.Rich(PendingRiteCityName
					?? System.SeatName ?? "the settlement") + ".");
			try
			{
				PendingSealRiteChronicle = BoundPendingRite(LegacyPhysicalRiteUnavailable
					? KingdomSuccessionRules.RiteChronicle(KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(FounderName),
						shownHeir, Road, Days)
					: KingdomSuccessionRules.SuccessionChronicle(KingdomPresentation.Rich(PendingRiteCityName),
						KingdomPresentation.Rich(FounderName),
						KingdomPresentation.Rich(PendingFounderCause), shownHeir, Road, Days,
						KingdomPresentation.Rich(PendingRiteFixtureName)));
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: accession rite telling fell back", ex);
			}
			PendingDeathToken = null;
			PendingDueTick = 0L;
			PendingDays = 0;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
			PendingRiteStage = MourningRiteStage.Complete;
			ClearPendingRiteIdentity();

			TryFinishAccessionBodyCleanup(Heir);
			TryPrepareRepairableHeir(Heir);

			int regard = 0;
			try
			{
				bool creedMatches = !string.IsNullOrEmpty(System.DeclaredCreed)
					&& string.Equals(HeirCreed, System.DeclaredCreed,
						StringComparison.OrdinalIgnoreCase);
				bool creedLeft = KingdomCreedRules.KeptHolds(FormerRow.KeptCreeds,
					System.DeclaredCreed);
				regard = KingdomSuccessionRules.AccessionRegard(FormerRow.ArrivedTick,
					Game.TimeTicks, creedMatches, creedLeft, HeldOffice);
				if (!TryResetPersonalKnowledge(System, Token, regard))
				{
					KingdomLog.Log("succession: honesty reset rolled back after accession; successor remains seated with prior knowledge intact");
					TryTellFailure("The charter changed hands, but the successor's personal records could not be opened safely. Nothing in them was changed.");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: post-accession honesty step failed", ex);
				KingdomLog.Log("succession: post-accession honesty step failed without reversing accession");
			}

			TryCompletePendingSealAccession(Context);
			TryInheritOpenQuests(Game, System, Token, FounderName);
			try
			{
				TryTell(KingdomSuccessionRules.RiteAttendedPopup(
					KingdomPresentation.Rich(System.SeatName),
					KingdomPresentation.Rich(FounderName), shownHeir, Road, Days));
				KingdomLog.Log("succession: " + FounderName + " -> " + FormerRow.Name + " token="
					+ Token + " heirZone=" + HeirZoneId + " road=" + Road + " days=" + Days
					+ " regard=" + regard);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: accession telling failed after commit", ex);
			}
		}

		private static void TryInheritOpenQuests(XRLGame Game, KingdomSystem System,
			string DeathToken, string FounderName)
		{
			if (Game == null || Game.Quests == null || System == null) return;
			foreach (KeyValuePair<string, Quest> pair in Game.Quests)
			{
				Quest quest = pair.Value;
				if (quest == null || quest.Finished) continue;
				string questId = string.IsNullOrEmpty(quest.ID) ? pair.Key : quest.ID;
				if (string.IsNullOrEmpty(questId)) questId = quest.Name;
				try
				{
					string eventId = KingdomSuccessionRules.InheritedQuestEventId(
						DeathToken, questId);
					if (string.IsNullOrEmpty(eventId) || !KingdomChronicle.RecordOnce(System,
						eventId, KingdomSuccessionRules.InheritedQuestChronicle(
							FounderName, quest.Name)))
					{
						KingdomLog.Log("succession: one inherited undertaking could not settle its Chronicle receipt");
					}
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: inherited quest Chronicle failed", ex);
					KingdomLog.Log("succession: one inherited undertaking lacked its Chronicle line ("
						+ ex.GetType().Name + ")");
				}
				try
				{
					if (KingdomSuccessionRules.PersonalQuest(questId, quest.Name)
						&& !quest.HasProperty(KingdomSuccessionRules.InheritedQuestMarker))
					{
						quest.Name = KingdomSuccessionRules.InheritedQuestName(quest.Name);
						quest.SetProperty(KingdomSuccessionRules.InheritedQuestMarker, "1");
					}
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: inherited quest label failed", ex);
					KingdomLog.Log("succession: one personal undertaking remained unlabelled ("
						+ ex.GetType().Name + ")");
				}
			}
		}

		private static void TryPrepareRepairableHeir(GameObject Heir)
		{
			try
			{
				PrepareSuccessor(Heir);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor body preparation failed", ex);
				KingdomLog.Log("succession: successor body preparation remains pending ("
					+ ex.GetType().Name + ")");
			}
			try
			{
				Heir.RequirePart<KingdomCharterPart>().EnsureAbility();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor charter ability failed", ex);
				KingdomLog.Log("succession: successor charter ability remains pending ("
					+ ex.GetType().Name + ")");
			}
		}

		private static void TryFinishAccessionBodyCleanup(GameObject Heir)
		{
			try
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				string citizenshipFailure;
				if (!KingdomCitizenship.TryRemove(system, Heir,
					KingdomCitizenshipRemovalReason.Accession, out citizenshipFailure))
				{
					KingdomLog.Log("succession: exact citizenship cleanup remains pending ("
						+ (citizenshipFailure ?? "unknown failure") + ")");
					return;
				}
				KingdomStations.Post(Heir, 0, KingdomWorkKind.Other);
				Heir.RemoveIntProperty(KingdomResidents.ResidentIdProperty);
				Heir.RemoveIntProperty("KingdomBorn");
				Heir.RemoveStringProperty("KingdomName");
				Heir.RemoveStringProperty(KingdomLodging.HomePlotIdProperty);
				Heir.RemovePart<r_KingdomCitizenLegacy>();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor resident cleanup failed", ex);
			}
		}

		private static string BoundPendingName(string Name)
		{
			string value = string.IsNullOrEmpty(Name) ? "the founder" : Name;
			return value.Length <= KingdomSealRecord.MaxNameChars
				? value : value.Substring(0, KingdomSealRecord.MaxNameChars);
		}

		private static string BoundPendingCreeds(string KeptCreeds)
		{
			string value = KeptCreeds ?? "";
			return value.Length <= MaxPendingRepairCreedsChars
				? value : value.Substring(0, MaxPendingRepairCreedsChars);
		}

		private static bool TryReadHeirs(KingdomSystem System, out List<HeirRuntime> Result)
		{
			Result = new List<HeirRuntime>();
			if (!ReadHeirs(System.City, System.OfficeHolderResidentId,
				System.OfficeHolderName, Result))
			{
				return false;
			}
			if (System.Away != null)
			{
				if (!ReadHeirs(System.Away.City, System.Away.OfficeHolderResidentId,
					System.Away.OfficeHolderName, Result))
				{
					return false;
				}
			}
			return true;
		}

		private static bool ReadHeirs(KingdomCityBook Book, int OfficeHolderResidentId,
			string LegacyOfficeHolder, List<HeirRuntime> Result)
		{
			KingdomCityState state;
			KingdomCityFault fault = default(KingdomCityFault);
			if (Book == null || !Book.TryRead(out state, out fault))
			{
				KingdomLog.Log("succession: a city book could not be read while choosing the heir (" + fault + ")");
				return false;
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Resident)
				{
					continue;
				}
				KingdomHeir rule = new KingdomHeir(row.Name, row.ArrivedTick, null, row.KeptCreeds,
					onTheRoll: true,
					holdsOffice: OfficeHolderResidentId > 0
						? row.ResidentId == OfficeHolderResidentId
						: !string.IsNullOrEmpty(LegacyOfficeHolder)
							&& string.Equals(row.Name, LegacyOfficeHolder,
								StringComparison.Ordinal),
					row.BoundZoneId, row.ResidentId);
				Result.Add(new HeirRuntime(rule));
			}
			return true;
		}

		private static void JudgeActualNews(KingdomSystem System, Zone DeathZone, out int Days, out NewsRoad Road)
		{
			string deathZoneId = DeathZone?.ZoneID;
			string seatZoneId = (System.ClaimedZones != null && System.ClaimedZones.Count > 0)
				? System.ClaimedZones[0] : null;
			string deathWorld = null;
			string seatWorld = null;
			int dwx = 0;
			int dwy = 0;
			int dzx = 0;
			int dzy = 0;
			int dz = 0;
			int swx = 0;
			int swy = 0;
			int szx = 0;
			int szy = 0;
			int sz = 0;
			bool deathParsed = TryParseZone(deathZoneId,
				out deathWorld, out dwx, out dwy, out dzx, out dzy, out dz);
			bool seatParsed = TryParseZone(seatZoneId,
				out seatWorld, out swx, out swy, out szx, out szy, out sz);
			bool onOwnedGround = !string.IsNullOrEmpty(deathZoneId)
				&& ((System.ClaimedZones != null && System.ClaimedZones.Contains(deathZoneId))
					|| (System.Away?.ClaimedZones != null
						&& System.Away.ClaimedZones.Contains(deathZoneId)));
			bool sameWorld = onOwnedGround || (deathParsed && seatParsed
				&& string.Equals(deathWorld, seatWorld, StringComparison.Ordinal));
			int dx = 0;
			int dy = 0;
			int depth = 0;
			if (sameWorld && !onOwnedGround)
			{
				dx = SaturatedDifference((long)dwx * 3L + dzx, (long)swx * 3L + szx);
				dy = SaturatedDifference((long)dwy * 3L + dzy, (long)swy * 3L + szy);
				depth = SaturatedDifference(dz, sz);
			}
			bool arch = ArchAnswersSeat(System, DeathZone);
			KingdomSuccessionRules.JudgeNews(arch, sameWorld, dx, dy, depth, out Days, out Road);
		}

		private static int SaturatedDifference(long A, long B)
		{
			long difference = A >= B ? A - B : B - A;
			return difference >= int.MaxValue ? int.MaxValue : (int)difference;
		}

		private static bool TryParseZone(string ZoneId, out string World, out int WorldX,
			out int WorldY, out int ZoneX, out int ZoneY, out int ZoneZ)
		{
			World = null;
			WorldX = 0;
			WorldY = 0;
			ZoneX = 0;
			ZoneY = 0;
			ZoneZ = 0;
			if (string.IsNullOrEmpty(ZoneId))
			{
				return false;
			}
			try
			{
				return ZoneID.Parse(ZoneId, out World, out WorldX, out WorldY,
					out ZoneX, out ZoneY, out ZoneZ);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("succession: zone id could not price news (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static bool ArchAnswersSeat(KingdomSystem System, Zone Zone)
		{
			try
			{
				if (Zone == null || The.Game == null || !KingdomPower.Enabled)
				{
					return false;
				}
				KingdomGateRow[] rows;
				int dropped;
				KingdomMirrorGateRules.TryParseRegister(
					The.Game.GetStringGameState(KingdomMirrorGateRules.RegisterStateKey, ""), out rows, out dropped);
				foreach (GameObject obj in Zone.GetObjects())
				{
					r_KingdomMirrorGate gate = obj?.GetPart<r_KingdomMirrorGate>();
					if (gate == null || gate.Dark)
					{
						continue;
					}
					KingdomMirrorGate.Anchor(gate);
					int here = KingdomMirrorGateRules.IndexOfKey(rows, gate.LocationKey);
					if (here < 0)
					{
						continue;
					}
					int there = KingdomMirrorGateRules.IndexOfKey(rows, rows[here].Partner);
					if (there >= 0 && string.Equals(rows[there].City, System.SeatName,
						StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
				return false;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("succession: arch news fact unavailable (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static void RecordFounderDeath(KingdomSystem System, string FounderName, AfterDieEvent E)
		{
			string cause = KingdomPresentation.Rich(DeathCause(E));
			KingdomChronicle.RecordDisputed(System,
				KingdomSuccessionRules.FallenChronicle(KingdomPresentation.Rich(FounderName), KingdomPresentation.Rich(System.SeatName), cause),
				KingdomSuccessionRules.FallenRumour(KingdomPresentation.Rich(FounderName), KingdomPresentation.Rich(System.SeatName)));
		}

		private static string DeathCause(AfterDieEvent E)
		{
			string cause = !string.IsNullOrEmpty(E?.ThirdPersonReason) ? E.ThirdPersonReason
				: (!string.IsNullOrEmpty(E?.Reason) ? E.Reason
					: "died, and no one living can say how");
			return ConsoleLib.Console.ColorUtility.StripFormatting(cause);
		}

		private void PublishFounderDeath(KingdomSystem System, string FounderName, AfterDieEvent E)
		{
			if (DeathChroniclePublished || System == null || E == null)
			{
				return;
			}
			DeathChroniclePublished = true;
			KingdomSystem.Guard("succession founder death", delegate
			{
				RecordFounderDeath(System, FounderName, E);
			});
		}

		private void EndDynasty(KingdomSystem System, string FounderName, SuccessionVerdict Verdict,
			AfterDieEvent Death)
		{
			PublishFounderDeath(System, FounderName, Death);
			KingdomChronicle.Record(System,
				KingdomSuccessionRules.DynastyEndChronicle(KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(FounderName)));
			string sealFailure;
			if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out sealFailure))
			{
				KingdomLog.Log("succession: terminal seal attempt failed closed ("
					+ (string.IsNullOrEmpty(sealFailure) ? "unknown failure" : sealFailure) + ")");
			}
			Popup.Show(KingdomSuccessionRules.DynastyEndPopup(KingdomPresentation.Rich(System.SeatName), Verdict));
			KingdomLog.Log("succession: terminal verdict " + Verdict + "; player body unchanged");
		}

		private void TryTerminalAfterOuterFailure(AfterDieEvent Death)
		{
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				GameObject founder = Death?.Dying;
				if (game == null || system == null || !system.Founded || founder == null
					|| !ReferenceEquals(The.Player, founder)
					|| !KingdomSuccessionRules.ModeOn(game.gameMode,
						game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey)))
				{
					return;
				}
				PublishFounderDeath(system, founder.BaseDisplayNameStripped, Death);
				string failure;
				if (!KingdomSeal.TryTerminalFromSuccession(Death, LineEnded: true, out failure))
				{
					KingdomLog.Log("succession: outer-failure terminal seal attempt failed closed ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: outer succession terminal attempt failed", ex);
			}
		}

		private static void PrepareSuccessor(GameObject Heir)
		{
			The.Game.PlayerName = Heir.Render.DisplayName;
			Heir.SetIntProperty("Renamed", 1);
			if (Heir.Brain != null)
			{
				// GamePlayer.SetBody owns the control transition and clears active AI goals itself.
				// Add only vanilla's player membership to the base set. Native memberships, every
				// temporary layer/reason/flag, leader, feeling, conversation and ownership survive.
				AllegianceSet baseSet = Heir.Brain.GetBaseAllegiance();
				if (baseSet == null)
					throw new InvalidOperationException("successor Brain has no base allegiance");
				baseSet["Player"] = 100;
			}
		}

		private static bool TryResetPersonalKnowledge(KingdomSystem System, string Token, int RealmRegard)
		{
			List<JournalSnapshot> journal = new List<JournalSnapshot>();
			foreach (IBaseJournalEntry entry in JournalAPI.GetAllNotes())
			{
				if (entry != null)
				{
					journal.Add(new JournalSnapshot(entry));
				}
			}
			List<TinkerData> recipes = (TinkerData.KnownRecipes == null)
				? new List<TinkerData>() : new List<TinkerData>(TinkerData.KnownRecipes);
			Reputation oldReputation = The.Game.PlayerReputation;
			string founderRites = The.Game.GetStringGameState(KingdomResearch.FounderRiteState, "");
			try
			{
				string attribute = KingdomSuccessionRules.FounderAttribute(Token);
				foreach (JournalSnapshot snapshot in journal)
				{
					IBaseJournalEntry entry = snapshot.Entry;
					if (!entry.Revealed || !KingdomSuccessionRules.Forgets(KindOf(entry), entry.Forgettable()))
					{
						continue;
					}
					if (entry.Attributes == null)
					{
						entry.Attributes = new List<string>();
					}
					if (!entry.Attributes.Contains(attribute))
					{
						entry.Attributes.Add(attribute);
					}
					entry.Forget(fast: true);
				}
				RevealRealmGround(System);
				TinkerData.KnownRecipes?.Clear();
				The.Game.SetStringGameState(KingdomResearch.FounderRiteState, "");
				if (!string.IsNullOrEmpty(The.Game.GetStringGameState(
					KingdomResearch.FounderRiteState, "")))
				{
					throw new InvalidOperationException("founder rite ledger did not clear");
				}
				Reputation next = new Reputation();
				next.Init();
				Faction realm = Factions.GetIfExists(System.KingdomFactionName);
				if (realm != null)
				{
					next.Set(realm, RealmRegard);
				}
				The.Game.PlayerReputation = next;
				next.InitFeeling();
				return true;
			}
			catch (Exception ex)
			{
				for (int i = 0; i < journal.Count; i++)
				{
					try
					{
						journal[i].Restore();
					}
					catch (Exception restoreEx)
					{
						MetricsManager.LogError("ThousandAndFirst: journal rollback entry failed", restoreEx);
					}
				}
				try
				{
					if (TinkerData.KnownRecipes != null)
					{
						TinkerData.KnownRecipes.Clear();
						TinkerData.KnownRecipes.AddRange(recipes);
					}
				}
				catch (Exception recipeEx)
				{
					MetricsManager.LogError("ThousandAndFirst: recipe rollback failed", recipeEx);
				}
				try
				{
					The.Game.SetStringGameState(KingdomResearch.FounderRiteState, founderRites);
					if (!string.Equals(The.Game.GetStringGameState(
						KingdomResearch.FounderRiteState, ""), founderRites,
						StringComparison.Ordinal))
					{
						throw new InvalidOperationException("founder rite ledger rollback did not stick");
					}
				}
				catch (Exception riteEx)
				{
					MetricsManager.LogError("ThousandAndFirst: founder rite rollback failed", riteEx);
				}
				try
				{
					The.Game.PlayerReputation = oldReputation;
					oldReputation?.InitFeeling();
				}
				catch (Exception reputationEx)
				{
					MetricsManager.LogError("ThousandAndFirst: reputation rollback failed", reputationEx);
				}
				MetricsManager.LogError("ThousandAndFirst: succession honesty reset rolled back", ex);
				return false;
			}
		}

		private static void RevealRealmGround(KingdomSystem System)
		{
			HashSet<string> ground = new HashSet<string>(StringComparer.Ordinal);
			if (System.ClaimedZones != null)
			{
				ground.UnionWith(System.ClaimedZones);
			}
			if (System.Away != null && System.Away.ClaimedZones != null)
			{
				ground.UnionWith(System.Away.ClaimedZones);
			}
			foreach (JournalMapNote note in JournalAPI.MapNotes)
			{
				if (note != null && ground.Contains(note.ZoneID) && !note.Revealed)
				{
					note.Reveal("the kingdom's chart", Silent: true);
				}
			}
		}

		private static JournalKind KindOf(IBaseJournalEntry Entry)
		{
			if (Entry is JournalAccomplishment) return JournalKind.Accomplishment;
			if (Entry is JournalMapNote) return JournalKind.MapNote;
			if (Entry is JournalGeneralNote) return JournalKind.GeneralNote;
			if (Entry is JournalVillageNote) return JournalKind.VillageNote;
			if (Entry is JournalRecipeNote) return JournalKind.RecipeNote;
			if (Entry is JournalSultanNote) return JournalKind.SultanNote;
			return JournalKind.Observation;
		}

		private void TryCompletePendingSealAccession(string Context)
		{
			if (string.IsNullOrEmpty(PendingSealAccessionToken))
			{
				return;
			}
			if (!PendingSealAccessionReady && !TryPublishPendingAccessionRite(Context))
			{
				return;
			}
			string token = PendingSealAccessionToken;
			string failure;
			if (KingdomSeal.TryStartSuccessorGeneration(token, out failure))
			{
				PendingSealAccessionToken = "";
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = false;
				return;
			}
			KingdomLog.Log("succession: pending profile accession remains after " + Context + " ("
				+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
		}

		private bool TryPublishPendingAccessionRite(string Context)
		{
			if (PendingSealAccessionReady)
			{
				return true;
			}
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded || string.IsNullOrEmpty(PendingSealRiteChronicle))
			{
				KingdomLog.Log("succession: pending accession rite cannot publish during " + Context);
				return false;
			}
			try
			{
				string eventId = KingdomSuccessionRules.AccessionRiteEventId(
					PendingSealAccessionToken);
				if (string.IsNullOrEmpty(eventId) || !KingdomChronicle.RecordOnce(system, eventId,
					PendingSealRiteChronicle))
				{
					KingdomLog.Log("succession: pending accession Chronicle receipt remains after "
						+ Context);
					return false;
				}
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = true;
				return true;
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: pending accession rite failed", ex);
				KingdomLog.Log("succession: pending accession rite remains after " + Context
					+ " (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static string BoundPendingRite(string Text)
		{
			string value = string.IsNullOrEmpty(Text)
				? "the charter passed to the successor after the founder's death." : Text;
			return value.Length <= MaxPendingRiteChronicleChars
				? value : value.Substring(0, MaxPendingRiteChronicleChars);
		}

		private void MigrateSavedState(int Version)
		{
			if (Version >= 2) return;
			LegacyPhysicalRiteUnavailable = true;
			ClearPendingRiteIdentity();
			CompletedShrineToken = "";
			CompletedShrineObjectId = "";
			CompletedShrineZoneId = "";
			if (!string.IsNullOrEmpty(PendingDeathToken)
				&& PendingAccessionRepairResidentId == 0)
			{
				// Version 1 could describe a clock jump but had no frozen body/locus/fixture
				// proof. It cannot be upgraded by inventing those facts. Quarantine only this
				// system so the enclosing save remains loadable.
				SuccessionDisabled = true;
				ClearDisabledSavedState();
			}
		}

		private void ValidateSavedState()
		{
			if (SuccessionDisabled)
			{
				ClearDisabledSavedState();
				return;
			}
			PendingDeathToken = PendingDeathToken ?? "";
			CompletedDeathToken = CompletedDeathToken ?? "";
			PendingSealAccessionToken = PendingSealAccessionToken ?? "";
			PendingFounderName = PendingFounderName ?? "";
			PendingFounderObjectId = PendingFounderObjectId ?? "";
			PendingFounderCause = PendingFounderCause ?? "";
			PendingHeirObjectId = PendingHeirObjectId ?? "";
			PendingHeirName = PendingHeirName ?? "";
			PendingHeirZoneId = PendingHeirZoneId ?? "";
			PendingRiteZoneId = PendingRiteZoneId ?? "";
			PendingRiteCityName = PendingRiteCityName ?? "";
			PendingRiteFixtureObjectId = PendingRiteFixtureObjectId ?? "";
			PendingRiteFixtureName = PendingRiteFixtureName ?? "";
			PendingRiteAttendeeManifest = PendingRiteAttendeeManifest ?? "";
			PendingShrineObjectId = PendingShrineObjectId ?? "";
			CompletedShrineToken = CompletedShrineToken ?? "";
			CompletedShrineObjectId = CompletedShrineObjectId ?? "";
			CompletedShrineZoneId = CompletedShrineZoneId ?? "";
			string stateFailure;
			if (!KingdomSuccessionRules.TryValidateSavedState(SuccessionOrdinal,
				PendingDeathToken, CompletedDeathToken, PendingPhase, PendingDueTick,
				PendingRoad, PendingDays, PendingAccessionRepairResidentId != 0,
				PendingSealAccessionToken, out stateFailure))
			{
				throw new InvalidOperationException("The saved succession state is invalid: "
					+ stateFailure + ".");
			}
			if (PendingSealAccessionToken != null
				&& PendingSealAccessionToken.Length > MaxSealAccessionTokenChars)
			{
				throw new InvalidOperationException("The saved profile-accession token is out of bounds.");
			}
			PendingSealRiteChronicle = PendingSealRiteChronicle ?? "";
			PendingAccessionRepairFounderName = PendingAccessionRepairFounderName ?? "";
			PendingAccessionRepairHeirName = PendingAccessionRepairHeirName ?? "";
			PendingAccessionRepairKeptCreeds = PendingAccessionRepairKeptCreeds ?? "";
			if (PendingAccessionRepairResidentId < 0
				|| PendingAccessionRepairFounderName.Length > KingdomSealRecord.MaxNameChars
				|| PendingAccessionRepairHeirName.Length > KingdomSealRecord.MaxNameChars
				|| PendingAccessionRepairKeptCreeds.Length > MaxPendingRepairCreedsChars
				|| PendingAccessionRepairArrivedTick < 0L
				|| (PendingAccessionRepairResidentId != 0
					&& (string.IsNullOrEmpty(PendingDeathToken)
						|| PendingDeathToken.Length > MaxSealAccessionTokenChars
						|| string.IsNullOrEmpty(PendingAccessionRepairHeirName))))
			{
				throw new InvalidOperationException("The saved accession repair identity is invalid.");
			}
			if (PendingAccessionRepairResidentId == 0)
			{
				PendingAccessionRepairFounderName = "";
				PendingAccessionRepairHeirName = "";
				PendingAccessionRepairSeated = false;
				PendingAccessionRepairArrivedTick = 0L;
				PendingAccessionRepairKeptCreeds = "";
			}
			if (PendingSealRiteChronicle.Length > MaxPendingRiteChronicleChars)
			{
				throw new InvalidOperationException("The saved accession rite chronicle is out of bounds.");
			}
			if (PendingSealAccessionToken.Length == 0)
			{
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = false;
			}
			else if (!PendingSealAccessionReady && PendingSealRiteChronicle.Length == 0)
			{
				// Compatibility with the first save shape: it only queued the token after the
				// rite chronicle had already been published.
				PendingSealAccessionReady = true;
			}
			else if (PendingSealAccessionReady)
			{
				PendingSealRiteChronicle = "";
			}

			bool hasPending = !string.IsNullOrEmpty(PendingDeathToken);
			if (!Enum.IsDefined(typeof(MourningRiteStage), PendingRiteStage))
			{
				throw new InvalidOperationException("The saved mourning-rite stage is invalid.");
			}
			if (!hasPending)
			{
				if (PendingRiteStage != MourningRiteStage.None)
				{
					throw new InvalidOperationException("An idle succession carries a mourning-rite stage.");
				}
				ClearPendingRiteIdentity();
			}
			else if (!LegacyPhysicalRiteUnavailable)
			{
				KingdomRiteAttendee[] attendees;
				if (PendingRiteStage < MourningRiteStage.Frozen
					|| PendingRiteStage > MourningRiteStage.BodyCrossed
					|| PendingHeirResidentId <= 0 || string.IsNullOrEmpty(PendingFounderName)
					|| string.IsNullOrEmpty(PendingFounderObjectId)
					|| string.IsNullOrEmpty(PendingFounderCause)
					|| string.IsNullOrEmpty(PendingHeirObjectId)
					|| string.IsNullOrEmpty(PendingHeirName)
					|| string.IsNullOrEmpty(PendingHeirZoneId)
					|| string.IsNullOrEmpty(PendingRiteZoneId)
					|| string.IsNullOrEmpty(PendingRiteCityName)
					|| string.IsNullOrEmpty(PendingRiteFixtureObjectId)
					|| string.IsNullOrEmpty(PendingRiteFixtureName)
					|| PendingFounderName.Length > KingdomSealRecord.MaxNameChars
					|| PendingHeirName.Length > KingdomSealRecord.MaxNameChars
					|| PendingRiteCityName.Length > KingdomSealRecord.MaxNameChars
					|| PendingRiteFixtureName.Length > KingdomSealRecord.MaxNameChars
					|| PendingFounderCause.Length > MaxPendingRiteChronicleChars
					|| PendingFounderObjectId.Length > 512
					|| PendingHeirObjectId.Length > 512
					|| PendingRiteFixtureObjectId.Length > 512
					|| PendingShrineObjectId.Length > 512
					|| PendingHeirZoneId.Length > 1024 || PendingRiteZoneId.Length > 1024
					|| PendingShrineX < 0 || PendingShrineX > 4096
					|| PendingShrineY < 0 || PendingShrineY > 4096
					|| !KingdomSuccessionRules.TryDecodeRiteManifest(
						PendingRiteAttendeeManifest, out attendees)
					|| attendees.Length == 0
					|| attendees[0].ResidentId != PendingHeirResidentId
					|| !string.Equals(attendees[0].ObjectId, PendingHeirObjectId,
						StringComparison.Ordinal)
					|| !string.Equals(attendees[0].ZoneId, PendingRiteZoneId,
						StringComparison.Ordinal)
					|| (PendingRiteStage >= MourningRiteStage.ShrinePlaced
						&& string.IsNullOrEmpty(PendingShrineObjectId))
					|| (PendingAccessionRepairResidentId != 0
						&& PendingRiteStage != MourningRiteStage.BodyCrossed))
				{
					throw new InvalidOperationException("The saved physical mourning-rite identity is invalid.");
				}
			}

			bool anyShrineReceipt = CompletedShrineToken.Length > 0
				|| CompletedShrineObjectId.Length > 0 || CompletedShrineZoneId.Length > 0;
			bool wholeShrineReceipt = CompletedShrineToken.Length > 0
				&& CompletedShrineObjectId.Length > 0 && CompletedShrineZoneId.Length > 0;
			if (anyShrineReceipt && !wholeShrineReceipt)
			{
				throw new InvalidOperationException("The in-run founder-shrine receipt is torn.");
			}
			int shrineOrdinal;
			long shrineTick;
			if (CompletedShrineToken.Length > 0
				&& (!KingdomSuccessionRules.TryReadDeathToken(CompletedShrineToken,
					out shrineOrdinal, out shrineTick)
					|| CompletedShrineObjectId.Length > 512
					|| CompletedShrineZoneId.Length > 1024))
			{
				throw new InvalidOperationException("The in-run founder-shrine receipt is invalid.");
			}
		}

		private void ClearDisabledSavedState()
		{
			int completedOrdinal;
			long completedTick;
			if (!KingdomSuccessionRules.TryReadDeathToken(CompletedDeathToken,
				out completedOrdinal, out completedTick))
			{
				CompletedDeathToken = "";
				SuccessionOrdinal = 0;
				PendingPhase = InterregnumPhase.None;
			}
			else
			{
				SuccessionOrdinal = completedOrdinal;
				PendingPhase = InterregnumPhase.Reigning;
			}
			PendingDeathToken = "";
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			PendingSealAccessionToken = "";
			PendingSealRiteChronicle = "";
			PendingSealAccessionReady = false;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
			ClearPendingRiteIdentity();
			int shrineOrdinal;
			long shrineTick;
			if (!KingdomSuccessionRules.TryReadDeathToken(CompletedShrineToken,
				out shrineOrdinal, out shrineTick)
				|| string.IsNullOrEmpty(CompletedShrineObjectId)
				|| string.IsNullOrEmpty(CompletedShrineZoneId))
			{
				CompletedShrineToken = "";
				CompletedShrineObjectId = "";
				CompletedShrineZoneId = "";
			}
		}

		private static void TryTellFailure(string Text)
		{
			try
			{
				Popup.Show(Text);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession failure telling failed", ex);
			}
		}

		private static void TryTell(string Text)
		{
			try
			{
				Popup.Show(Text);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession telling failed", ex);
			}
		}

		private sealed class HeirRuntime
		{
			internal readonly KingdomHeir Rule;

			internal HeirRuntime(KingdomHeir Rule)
			{
				this.Rule = Rule;
			}
		}

		private sealed class JournalSnapshot
		{
			internal readonly IBaseJournalEntry Entry;
			private readonly bool Revealed;
			private readonly string LearnedFrom;
			private readonly List<string> Attributes;

			internal JournalSnapshot(IBaseJournalEntry Entry)
			{
				this.Entry = Entry;
				Revealed = Entry.Revealed;
				LearnedFrom = Entry.LearnedFrom;
				Attributes = Entry.Attributes == null ? null : new List<string>(Entry.Attributes);
			}

			internal void Restore()
			{
				Entry.Revealed = Revealed;
				Entry.LearnedFrom = LearnedFrom;
				Entry.Attributes = Attributes == null ? null : new List<string>(Attributes);
				Entry.Updated();
			}
		}
	}
}
