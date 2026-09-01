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
		private const int CurrentSerializationVersion = 4;
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
		private string PendingAccessionRepairSettlementId;
		[Obsolete("Legacy save migration only; use PendingAccessionRepairSettlementId.")]
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

		// This named field belongs to older save blocks. Only migration and exact legacy-state
		// validation may inspect it; current repair authority is the immutable settlement id.
		private bool ReadLegacyAccessionRepairSeated()
		{
#pragma warning disable 618
			return PendingAccessionRepairSeated;
#pragma warning restore 618
		}

		private void ClearLegacyAccessionRepairSeated()
		{
#pragma warning disable 618
			PendingAccessionRepairSeated = false;
#pragma warning restore 618
		}

		[NonSerialized]
		private bool LoadFailed;

		[NonSerialized]
		private bool DeathChroniclePublished;

		[NonSerialized]
		private bool AccessionOwnershipCommitted;

		private static bool DeathSelectionInProgress;

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
			DeathSelectionInProgress = true;
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
				DeathSelectionInProgress = false;
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
				PrepareConfigurationRecovery(system);
				TryResumePendingRite("game load");
				TryCompletePendingAccessionRepair("game load");
				TryCompletePendingSealAccession("game load");
				FinishConfigurationRecovery(system, "game load");
			}
			return base.HandleEvent(E);
		}

		public override void BeforeSave()
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (KingdomMaster.AutomaticWorkAllowed(system)
				&& KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled))
			{
				PrepareConfigurationRecovery(system);
				TryCompletePendingAccessionRepair("BeforeSave");
				TryCompletePendingSealAccession("BeforeSave");
				FinishConfigurationRecovery(system, "BeforeSave");
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

	}
}
