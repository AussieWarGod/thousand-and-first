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
	public sealed partial class KingdomSuccession : IPlayerSystem
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

	}
}
