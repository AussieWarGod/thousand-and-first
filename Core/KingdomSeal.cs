using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Save-scoped coordinator for one profile-level kingdom lineage.
	/// <para>
	/// This system owns identity, capture timing, terminal attempts, explicit retirement,
	/// later promotion, and import receipts. <see cref="KingdomSealStore"/> owns durable files;
	/// <see cref="KingdomSealRules"/> owns their meaning. No world-generation placement lives
	/// here.
	/// </para>
	/// <para>
	/// Death has one owner. Outside Kingdom Mode this player system observes
	/// <see cref="AfterDieEvent"/> directly. Inside Kingdom Mode it does nothing at event time;
	/// <c>KingdomSuccession</c> must explicitly call <see cref="TryTerminalFromSuccession"/> only
	/// after ruling the line ended, or <see cref="TryStartSuccessorGeneration"/> after accession.
	/// </para>
	/// </summary>
	[Serializable]
	public sealed partial class KingdomSeal : IPlayerSystem
	{
		private const int SerializationMagic = 1413567315;
		private const int CurrentSerializationVersion = 1;
		private const string ProfileFolder = "ThousandAndFirst";
		private const int MaxScoresScanned = 10000;
		private const int MaxSaveDirectoriesScanned = 1024;
		private const int MaxSaveEntriesScanned = 256;

		private int SerializationVersion = CurrentSerializationVersion;
		private string LineageId = "";
		private string LegacyId = "";
		private string OriginGameId = "";
		private int Generation;
		private int Revision;
		private long LastPollTick;
		private string SealedLegacyId = "";
		private string LastAccessionToken = "";
		private string PendingAccessionToken = "";
		private bool SealDisabled;

		[NonSerialized]
		private KingdomSealStore Store;

		[NonSerialized]
		private bool Dirty;

		[NonSerialized]
		private string DirtyReason;

		[NonSerialized]
		private bool FlushInProgress;

		[NonSerialized]
		private bool ReconcileInProgress;

		[NonSerialized]
		private bool LoadFailed;

		[NonSerialized]
		private string LastFailureKey;

		public override bool WantFieldReflection => false;

		/// <summary>Stable dynasty id. Empty before a realm is founded.</summary>
		public string CurrentLineageId => AuthorityEnabled ? LineageId ?? "" : "";

		/// <summary>Identity of this generation's possible immutable result.</summary>
		public string CurrentLegacyId => AuthorityEnabled ? LegacyId ?? "" : "";

		/// <summary>Zero for the founder; advances once per successful accession.</summary>
		public int CurrentGeneration => AuthorityEnabled ? Generation : 0;

		/// <summary>Exact legacy sealed by retirement in this still-live save, or empty.</summary>
		public string RetiredLegacyId => AuthorityEnabled ? SealedLegacyId ?? "" : "";

		/// <summary>Namespaced profile root. Store adds Stages, Legacies, and Receipts below it.</summary>
		private static string ProfileRootPath()
		{
			return DataManager.SyncedPath(ProfileFolder);
		}

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(EndTurnEvent.ID);
		}

		public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterDieEvent.ID);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			try
			{
				KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.AutomaticWorkAllowed(kingdom))
				{
					return base.HandleEvent(E);
				}
				if (!SealEnabled())
				{
					return base.HandleEvent(E);
				}
				if (AuthorityEnabled)
				{
					TryReconcileProfile("game load");
					string failure;
					if (!TrySynchronizeLoadedWorld(out failure))
					{
						ReportFailure("loaded stage reconciliation", failure);
					}
				}
				else
				{
					ReportFailure("load", "the saved seal coordinator could not be read; seal writes are disabled for this save");
				}
			}
			catch (Exception ex)
			{
				ReportFailure("game load", ex.Message, ex);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			try
			{
				XRLGame game = The.Game;
				KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
				if (game == null || !KingdomMaster.AutomaticWorkAllowed(kingdom)
					|| !AuthorityEnabled || !SealEnabled()
					|| !KingdomSealEngineRules.PollDue(LastPollTick, game.TimeTicks, Calendar.TurnsPerDay))
				{
					return base.HandleEvent(E);
				}
				LastPollTick = SafeTick(game.TimeTicks);
				TryReconcileProfile("daily reconciliation");
				string failure;
				if (!TryFlushLiving("daily missed-dirty backstop", ProbeEvenIfClean: true, out failure))
				{
					ReportFailure("daily stage", failure);
				}
			}
			catch (Exception ex)
			{
				ReportFailure("daily stage", ex.Message, ex);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterDieEvent E)
		{
			try
			{
				XRLGame game = The.Game;
				GameObject dying = E?.Dying;
				KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
				bool kingdomMode = IsKingdomMode(game);
				if (game != null && KingdomMaster.AutomaticWorkAllowed(kingdom)
					&& AuthorityEnabled && SealEnabled() && dying != null
					&& ReferenceEquals(The.Player, dying)
					&& KingdomSealEngineRules.ObserveDeathDirectly(kingdomMode,
						kingdom != null && kingdom.Founded, IsGenerationSealed))
				{
					string failure;
					if (!TryWriteTerminal(DeathReason(E), DeathCategory(E), game.TimeTicks, out failure))
					{
						ReportFailure("terminal attempt", failure);
					}
				}
			}
			catch (Exception ex)
			{
				ReportFailure("terminal attempt", ex.Message, ex);
			}
			return base.HandleEvent(E);
		}

		/// <summary>Final synchronous external stage flush. <c>AfterSave</c> is intentionally not
		/// overridden: engine calls it before the primary writer has finished.</summary>
		public override void BeforeSave()
		{
			try
			{
				string failure;
				KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
				if (KingdomMaster.AutomaticWorkAllowed(kingdom)
					&& SealEnabled() && AuthorityEnabled
					&& !TryFlushLiving("BeforeSave", ProbeEvenIfClean: true, out failure))
				{
					ReportFailure("BeforeSave stage", failure);
				}
			}
			catch (Exception ex)
			{
				ReportFailure("BeforeSave stage", ex.Message, ex);
			}
		}

		public override void AfterLoad(XRLGame Game)
		{
			Store = null;
			Dirty = false;
			DirtyReason = null;
			FlushInProgress = false;
			ReconcileInProgress = false;
			LastFailureKey = null;
			if (!AuthorityEnabled)
			{
				NeutralizeDisabledState();
			}
		}

		public override void Write(SerializationWriter Writer)
		{
			SealDisabled = KingdomSealEngineRules.PersistSealDisabled(LoadFailed, SealDisabled);
			if (SealDisabled)
			{
				NeutralizeDisabledState();
			}
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomSeal),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(SerializationReader Reader)
		{
			bool disabledBeforeRead = !AuthorityEnabled;
			try
			{
				int magic = Reader.ReadInt32();
				int version = Reader.ReadInt32();
				if (magic != SerializationMagic || version != CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst seal coordinator save block.");
				}
				Reader.ReadNamedFields(this, typeof(KingdomSeal),
					BindingFlags.Instance | BindingFlags.NonPublic);
				if (SerializationVersion != CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst seal named-field version.");
				}
				if (disabledBeforeRead)
				{
					SealDisabled = true;
					NeutralizeDisabledState();
				}
				SerializationVersion = CurrentSerializationVersion;
				ValidateSavedState();
				LoadFailed = false;
			}
			catch
			{
				LoadFailed = true;
				SealDisabled = true;
				NeutralizeDisabledState();
				throw;
			}
		}

	}
}
