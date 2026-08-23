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
	public sealed class KingdomSeal : IPlayerSystem
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
				if (game == null || !AuthorityEnabled || !SealEnabled()
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
				if (game != null && AuthorityEnabled && SealEnabled() && dying != null
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
				if (SealEnabled() && AuthorityEnabled
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

		/// <summary>Marks a semantic action dirty. Call <see cref="TryStageSemanticSnapshot"/> at
		/// the coherent end of that action.</summary>
		public static void MarkSemanticDirty(string Reason)
		{
			try
			{
				if (!SealEnabled())
				{
					return;
				}
				KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
				if (seal != null)
				{
					string authorityFailure;
					if (!seal.TryRequireAuthority(out authorityFailure))
					{
						seal.ReportFailure("mark dirty", authorityFailure);
						return;
					}
					seal.MarkDirty(Reason);
				}
			}
			catch (Exception ex)
			{
				LogStaticFailure("mark dirty", ex);
			}
		}

		/// <summary>Stages the next coherent living snapshot after a semantic action. Safe to call
		/// when no fact changed; canonical comparison suppresses a redundant revision.</summary>
		public static bool TryStageSemanticSnapshot(string Reason, out string Failure)
		{
			Failure = "";
			try
			{
				if (!SealEnabled())
				{
					return true;
				}
				KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				seal.MarkDirty(Reason);
				return seal.TryFlushLiving(Reason, ProbeEvenIfClean: true, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("semantic stage", ex);
				return false;
			}
		}

		/// <summary>Founding's immediate flush. Loader/founding integration calls this after the
		/// founding action is wholly published.</summary>
		public static bool TryFoundingCompleted(out string Failure)
		{
			Failure = "";
			try
			{
				if (!SealEnabled())
				{
					return true;
				}
				KingdomSeal seal = The.Game?.RequireSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator could not be loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				seal.MarkDirty("founding");
				bool result = seal.TryFlushLiving("founding", ProbeEvenIfClean: true, out Failure);
				if (!result)
				{
					seal.ReportFailure("founding stage", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("founding stage", ex);
				return false;
			}
		}

		/// <summary>
		/// Kingdom-mode terminal route. Succession calls this only after exact-heir resolution has
		/// ruled the line ended. Calling it for a successful accession is refused.
		/// </summary>
		public static bool TryTerminalFromSuccession(AfterDieEvent Death, bool LineEnded,
			out string Failure)
		{
			Failure = "";
			try
			{
				if (!SealEnabled())
				{
					return true;
				}
				XRLGame game = The.Game;
				KingdomSeal seal = game?.GetSystem<KingdomSeal>();
				KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
				if (seal == null || game == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (!KingdomSealEngineRules.AcceptSuccessionTerminal(IsKingdomMode(game),
					kingdom != null && kingdom.Founded, seal.IsGenerationSealed, LineEnded))
				{
					Failure = "succession has not ruled an unsealed Kingdom-mode line ended";
					return false;
				}
				bool result = seal.TryWriteTerminal(DeathReason(Death), DeathCategory(Death),
					game.TimeTicks, out Failure);
				if (!result)
				{
					seal.ReportFailure("succession terminal attempt", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("succession terminal attempt", ex);
				return false;
			}
		}

		/// <summary>
		/// Successful-accession route. Must be called after body/row/ledger/chronicle publication.
		/// Token is succession's exact founder-death token and makes a retry idempotent.
		/// </summary>
		public static bool TryStartSuccessorGeneration(string AccessionToken, out string Failure)
		{
			Failure = "";
			try
			{
				if (!SealEnabled())
				{
					return true;
				}
				KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				bool result = seal.TryAdvanceGeneration(AccessionToken, out Failure);
				if (!result)
				{
					seal.ReportFailure("successor generation", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("successor generation", ex);
				return false;
			}
		}

		/// <summary>Immediately stages and promotes explicit retirement. The save remains alive;
		/// <see cref="RetiredLegacyId"/> records the exact immutable generation in it.</summary>
		public static bool TryRetireGeneration(out string Failure)
		{
			Failure = "";
			try
			{
				if (!SealEnabled())
				{
					Failure = "realm sealing is disabled in the options";
					return false;
				}
				KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				bool result = seal.TryRetire(out Failure);
				if (!result)
				{
					seal.ReportFailure("retirement", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("retirement", ex);
				return false;
			}
		}

		/// <summary>
		/// Import coordinator entrypoint. Returns true with null outputs when policy has nothing to
		/// offer. An existing exact-target reservation is returned on retry.
		/// </summary>
		internal bool TryReserveImport(KingdomImportPolicy Policy, out KingdomSealRecord Legacy,
			out KingdomSealReceipt Receipt, out KingdomSealReservationLease Lease, out string Failure)
		{
			Legacy = null;
			Receipt = null;
			Lease = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (!LegacyImportEnabled())
				{
					return true;
				}
				if (The.Game == null || !KingdomSealReceipt.ValidId(The.Game.GameID))
				{
					Failure = "the target game has no valid identity";
					return false;
				}
				TryReconcileProfile("import selection");
				KingdomSealStore store = GetStore();
				int refused;
				List<KingdomSealRecord> legacies = store.ReadLegacies(out refused);
				if (refused > 0)
				{
					Failure = "one or more legacy files could not be validated; latest selection is ambiguous";
					return false;
				}
				List<KingdomSealReceipt> receipts = store.ReadReceipts(out refused);
				if (refused > 0)
				{
					Failure = "one or more receipt files could not be validated; import selection is ambiguous";
					return false;
				}
				KingdomSealReceipt targetReceipt = null;
				HashSet<string> unavailable = new HashSet<string>();
				for (int i = 0; i < receipts.Count; i++)
				{
					KingdomSealReceipt item = receipts[i];
					unavailable.Add(item.LegacyId);
					if (item.TargetGameId == The.Game.GameID
						&& (item.State == KingdomSealReceiptState.Reserved
							|| item.State == KingdomSealReceiptState.Committed))
					{
						if (targetReceipt != null && targetReceipt.LegacyId != item.LegacyId)
						{
							Failure = "the target game has more than one import receipt";
							return false;
						}
						targetReceipt = item;
					}
				}
				if (targetReceipt != null)
				{
					for (int i = 0; i < legacies.Count; i++)
					{
						if (legacies[i].LegacyId == targetReceipt.LegacyId
							&& legacies[i].LineageId == targetReceipt.LineageId)
						{
							if (targetReceipt.State == KingdomSealReceiptState.Reserved
								&& !store.TryAcquireReservationLease(targetReceipt,
									out Lease, out Failure))
							{
								return false;
							}
							Legacy = legacies[i];
							Receipt = targetReceipt;
							return true;
						}
					}
					Failure = "the target receipt names no validated immutable legacy";
					return false;
				}

				KingdomSealRecord selected = KingdomSealRules.Select(legacies, unavailable, Policy);
				if (selected == null)
				{
					return true;
				}
				KingdomSealReceipt claimed;
				if (!store.TryClaimReservation(selected, The.Game.GameID,
					SafeTick(The.Game.TimeTicks), out claimed, out Lease, out Failure))
				{
					return false;
				}
				Legacy = selected;
				Receipt = claimed;
				return true;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Reads the monotone on-disk state of this target's exact persisted tuple.</summary>
		internal bool TryInspectImport(KingdomSealReceipt Expected,
			out KingdomSealReceipt Current, out string Failure)
		{
			Current = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Expected == null || The.Game == null
					|| Expected.TargetGameId != The.Game.GameID)
				{
					Failure = "only this target game's expected receipt can be inspected";
					return false;
				}
				return GetStore().TryInspectReceipt(Expected, out Current, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Reacquires the live OS claim for this target's exact persisted reservation.</summary>
		internal bool TryResumeImport(KingdomSealReceipt Reserved,
			out KingdomSealReservationLease Lease, out string Failure)
		{
			Lease = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Reserved == null || Reserved.State != KingdomSealReceiptState.Reserved
					|| The.Game == null || Reserved.TargetGameId != The.Game.GameID)
				{
					Failure = "only this target game's exact reserved receipt can resume its live claim";
					return false;
				}
				return GetStore().TryAcquireReservationLease(Reserved, out Lease, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Commits only the exact reservation whose target still holds its OS claim.</summary>
		internal bool TryCommitImport(KingdomSealReceipt Reserved,
			KingdomSealReservationLease Lease, out KingdomSealReceipt Committed,
			out string Failure)
		{
			Committed = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Reserved == null || Reserved.State != KingdomSealReceiptState.Reserved
					|| The.Game == null || Reserved.TargetGameId != The.Game.GameID)
				{
					Failure = "only this target game's exact reserved receipt can be committed";
					return false;
				}
				return GetStore().TryCommitReservation(Reserved, Lease,
					SafeTick(The.Game.TimeTicks), out Committed, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Marks an explicit player decline final and spent. Silence/crash never calls it.</summary>
		internal bool TryDeclineImport(KingdomSealReceipt Reserved, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			if (Reserved == null || Reserved.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reservation can be declined";
				return false;
			}
			KingdomSealReceipt declined = CopyReceipt(Reserved, KingdomSealReceiptState.Declined,
				SafeTick(The.Game?.TimeTicks ?? Reserved.WrittenTick));
			try
			{
				return GetStore().TryWriteReceipt(declined, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Releases a worldgen site refusal. Final receipts are never releasable.</summary>
		internal bool TryReleaseImport(KingdomSealReceipt Reserved, out string Failure)
		{
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				return GetStore().TryReleaseReservation(Reserved, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Releases an exact site refusal while the target still holds its OS live claim.</summary>
		internal bool TryReleaseImport(KingdomSealReceipt Reserved,
			KingdomSealReservationLease Lease, out string Failure)
		{
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				return GetStore().TryReleaseReservation(Reserved, Lease, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Loader/import hook for bounded later-boot reconciliation.</summary>
		internal void ReconcileProfile()
		{
			string authorityFailure;
			if (!TryRequireAuthority(out authorityFailure))
			{
				ReportFailure("loader reconciliation", authorityFailure);
				return;
			}
			if (SealEnabled())
			{
				TryReconcileProfile("loader reconciliation");
				string failure;
				if (!TrySynchronizeLoadedWorld(out failure))
				{
					ReportFailure("loader stage reconciliation", failure);
				}
			}
		}

		private bool AuthorityEnabled => KingdomSealEngineRules.SealAuthorityEnabled(
			LoadFailed, SealDisabled);

		private bool TryRequireAuthority(out string Failure)
		{
			Failure = "";
			if (AuthorityEnabled)
			{
				return true;
			}
			Failure = "the saved seal coordinator is disabled for this save";
			return false;
		}

		private void NeutralizeDisabledState()
		{
			SealDisabled = true;
			LineageId = "";
			LegacyId = "";
			OriginGameId = "";
			Generation = 0;
			Revision = 0;
			LastPollTick = 0L;
			SealedLegacyId = "";
			LastAccessionToken = "";
			PendingAccessionToken = "";
			Store = null;
			Dirty = false;
			DirtyReason = null;
			FlushInProgress = false;
			ReconcileInProgress = false;
			LastFailureKey = null;
		}

		private bool IsGenerationSealed => AuthorityEnabled && !string.IsNullOrEmpty(LegacyId)
			&& string.Equals(SealedLegacyId, LegacyId, StringComparison.Ordinal);

		private void MarkDirty(string Reason)
		{
			if (!AuthorityEnabled || IsGenerationSealed)
			{
				return;
			}
			Dirty = true;
			DirtyReason = KingdomSealRules.SanitizeText(Reason, 160);
		}

		private bool TryFlushLiving(string Reason, bool ProbeEvenIfClean, out string Failure)
		{
			Failure = "";
			if (FlushInProgress)
			{
				Failure = "a seal flush is already in progress";
				return false;
			}
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			XRLGame game = The.Game;
			KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
			if (game == null || kingdom == null || !kingdom.Founded)
			{
				return true;
			}
			if (!Dirty && !ProbeEvenIfClean && string.IsNullOrEmpty(PendingAccessionToken))
			{
				return true;
			}
			FlushInProgress = true;
			try
			{
				if (!EnsureIdentity(kingdom, out Failure))
				{
					return false;
				}
				if (!string.IsNullOrEmpty(PendingAccessionToken)
					&& !TryCompletePendingGeneration(kingdom, out Failure))
				{
					return false;
				}
				if (IsGenerationSealed)
				{
					Dirty = false;
					DirtyReason = null;
					return true;
				}

				KingdomSealStore store = GetStore();
				KingdomSealRecord existing = store.ReadStage(OriginGameId);
				if (existing != null && existing.Status == KingdomSealStatus.Retired
					&& SameCurrentIdentity(existing))
				{
					return TryCompleteRetirement(existing, out Failure);
				}
				int baseRevision = Revision;
				if (existing != null && SameCurrentIdentity(existing) && existing.Revision > baseRevision)
				{
					baseRevision = existing.Revision;
				}
				if (existing != null && !SameCurrentIdentity(existing))
				{
					Failure = "the origin journal names a different current generation";
					return false;
				}

				KingdomSealRecord probe;
				if (!TryCapture(kingdom, LegacyId, Generation, baseRevision,
					SafeTick(game.TimeTicks), out probe, out Failure))
				{
					return false;
				}
				if (existing != null && existing.Status == KingdomSealStatus.Living
					&& KingdomSealEngineRules.SameLivingSnapshot(existing, probe))
				{
					Revision = existing.Revision;
					Dirty = false;
					DirtyReason = null;
					LastFailureKey = null;
					return true;
				}
				int nextRevision;
				if (!KingdomSealEngineRules.TryNextRevision(baseRevision, out nextRevision))
				{
					Failure = "the seal revision is exhausted";
					return false;
				}
				KingdomSealRecord next;
				if (!TryCapture(kingdom, LegacyId, Generation, nextRevision,
					SafeTick(game.TimeTicks), out next, out Failure))
				{
					return false;
				}
				if (!store.TryStage(next, out Failure))
				{
					return false;
				}
				Revision = nextRevision;
				LastPollTick = SafeTick(game.TimeTicks);
				Dirty = false;
				DirtyReason = null;
				LastFailureKey = null;
				return true;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			finally
			{
				FlushInProgress = false;
			}
		}

		private bool TryWriteTerminal(string CauseText, string CauseKind, long CauseTurn,
			out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
			if (kingdom == null || !kingdom.Founded)
			{
				return true;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			if (IsGenerationSealed)
			{
				return true;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord existing = store.ReadStage(OriginGameId);
			if (existing != null && !SameCurrentIdentity(existing))
			{
				Failure = "the origin journal names a different current generation";
				return false;
			}
			int baseRevision = existing == null ? Revision : Math.Max(Revision, existing.Revision);
			KingdomSealRecord living;
			if (!TryCapture(kingdom, LegacyId, Generation, baseRevision,
				SafeTick(The.Game.TimeTicks), out living, out Failure))
			{
				return false;
			}
			KingdomSealRecord terminal;
			try
			{
				terminal = KingdomSealRules.WithTerminalCause(living, CauseText, CauseKind,
					SafeTick(CauseTurn));
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			if (!store.TryStage(terminal, out Failure))
			{
				return false;
			}
			Revision = terminal.Revision;
			Dirty = false;
			DirtyReason = null;
			return true;
		}

		private bool TryRetire(out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
			if (kingdom == null || !kingdom.Founded)
			{
				Failure = "no founded realm can be retired";
				return false;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord existing = store.ReadStage(OriginGameId);
			if (IsGenerationSealed)
			{
				return HasExactLegacy(store, LegacyId, out Failure);
			}
			if (existing != null && existing.Status == KingdomSealStatus.Retired
				&& SameCurrentIdentity(existing))
			{
				return TryCompleteRetirement(existing, out Failure);
			}
			if (existing != null && !SameCurrentIdentity(existing))
			{
				Failure = "the origin journal names a different current generation";
				return false;
			}
			int baseRevision = existing == null ? Revision : Math.Max(Revision, existing.Revision);
			KingdomSealRecord living;
			if (!TryCapture(kingdom, LegacyId, Generation, baseRevision,
				SafeTick(The.Game.TimeTicks), out living, out Failure))
			{
				return false;
			}
			KingdomSealRecord retired;
			try
			{
				retired = KingdomSealRules.WithRetirement(living);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			if (!store.TryStage(retired, out Failure))
			{
				return false;
			}
			return TryCompleteRetirement(retired, out Failure);
		}

		private bool TryCompleteRetirement(KingdomSealRecord Retired, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			if (Retired == null || Retired.Status != KingdomSealStatus.Retired
				|| Retired.OriginGameId != The.Game?.GameID)
			{
				Failure = "the retirement stage does not belong to this game";
				return false;
			}
			KingdomSealRecord promoted;
			try
			{
				promoted = KingdomSealRules.PromoteRetirement(Retired);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			if (!GetStore().TryWriteLegacy(promoted, out Failure))
			{
				return false;
			}
			AdoptIdentity(Retired);
			SealedLegacyId = Retired.LegacyId;
			Dirty = false;
			DirtyReason = null;
			LastFailureKey = null;
			return true;
		}

		private bool TryAdvanceGeneration(string AccessionToken, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			int tokenOrdinal;
			long tokenTick;
			if (!KingdomSuccessionRules.TryReadDeathToken(AccessionToken,
				out tokenOrdinal, out tokenTick))
			{
				Failure = "succession supplied no canonical accession identity";
				return false;
			}
			if (string.Equals(LastAccessionToken, AccessionToken, StringComparison.Ordinal))
			{
				if (tokenOrdinal == Generation) return true;
				Failure = "the completed accession token names the wrong generation";
				return false;
			}
			if (!string.IsNullOrEmpty(PendingAccessionToken))
			{
				if (!string.Equals(PendingAccessionToken, AccessionToken, StringComparison.Ordinal))
				{
					Failure = "a different accession is still waiting for its stage handoff";
					return false;
				}
				if (tokenOrdinal != Generation)
				{
					Failure = "the pending accession token names the wrong generation";
					return false;
				}
				return TryCompletePendingGeneration(The.Game?.GetSystem<KingdomSystem>(), out Failure);
			}
			if (Generation == int.MaxValue || tokenOrdinal != Generation + 1)
			{
				Failure = "the accession token does not name the exact next generation";
				return false;
			}
			XRLGame game = The.Game;
			KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
			if (game == null || kingdom == null || !kingdom.Founded || !IsKingdomMode(game))
			{
				Failure = "a successor generation requires a founded Kingdom-mode realm";
				return false;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord previous = store.ReadStage(OriginGameId);
			if (previous == null)
			{
				Dirty = true;
				if (!TryFlushLiving("accession preflight", ProbeEvenIfClean: true, out Failure))
				{
					return false;
				}
				previous = store.ReadStage(OriginGameId);
			}
			if (previous == null || !SameCurrentIdentity(previous)
				|| (previous.Status != KingdomSealStatus.Living
					&& previous.Status != KingdomSealStatus.Retired))
			{
				Failure = "the current generation has no exact living or retired stage to hand off";
				return false;
			}
			int nextGeneration;
			int nextRevision;
			if (!KingdomSealEngineRules.TryNextGeneration(previous.Generation, out nextGeneration)
				|| !KingdomSealEngineRules.TryNextRevision(previous.Revision, out nextRevision))
			{
				Failure = "the lineage has exhausted its generation or revision bound";
				return false;
			}
			string nextLegacy = MintId();
			KingdomSealRecord successor;
			if (!TryCapture(kingdom, nextLegacy, nextGeneration, nextRevision,
				SafeTick(game.TimeTicks), out successor, out Failure))
			{
				return false;
			}
			if (!KingdomSealEngineRules.MayAdvanceGeneration(previous, successor))
			{
				Failure = "the successor snapshot is not the exact adjacent living generation";
				return false;
			}

			// Publish intended save state before the external handoff. If disk I/O fails or tears,
			// BeforeSave/load retries the exact identity tuple; Store keeps the durable successor slot
			// canonical and finishes its other slot from that copy.
			LegacyId = successor.LegacyId;
			Generation = successor.Generation;
			Revision = successor.Revision;
			SealedLegacyId = "";
			PendingAccessionToken = AccessionToken;
			Dirty = true;
			if (!store.TryAdvanceGeneration(previous, successor, out Failure))
			{
				return false;
			}
			LastAccessionToken = AccessionToken;
			PendingAccessionToken = "";
			Dirty = false;
			DirtyReason = null;
			return true;
		}

		private bool TryCompletePendingGeneration(KingdomSystem Kingdom, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			if (string.IsNullOrEmpty(PendingAccessionToken))
			{
				return true;
			}
			if (!KingdomSealEngineRules.AccessionTokenIsOrdinal(PendingAccessionToken,
				Generation))
			{
				Failure = "the pending accession token is not canonical for this generation";
				return false;
			}
			if (Kingdom == null || !Kingdom.Founded)
			{
				Failure = "the pending accession has no founded realm to capture";
				return false;
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord existing = store.ReadStage(OriginGameId);
			if (existing != null && SameCurrentIdentity(existing)
				&& existing.Status == KingdomSealStatus.Living)
			{
				if (!store.TryCompleteGenerationAdvance(existing, out Failure))
				{
					return false;
				}
				Revision = existing.Revision;
				LastAccessionToken = PendingAccessionToken;
				PendingAccessionToken = "";
				Dirty = true;
				return true;
			}
			if (existing == null || existing.LineageId != LineageId
				|| existing.OriginGameId != OriginGameId
				|| existing.Generation + 1 != Generation
				|| (existing.Status != KingdomSealStatus.Living
					&& existing.Status != KingdomSealStatus.Retired))
			{
				Failure = "the pending accession cannot find its exact prior generation";
				return false;
			}
			KingdomSealRecord successor;
			if (!TryCapture(Kingdom, LegacyId, Generation, Revision,
				SafeTick(The.Game.TimeTicks), out successor, out Failure))
			{
				return false;
			}
			if (!store.TryAdvanceGeneration(existing, successor, out Failure))
			{
				return false;
			}
			LastAccessionToken = PendingAccessionToken;
			PendingAccessionToken = "";
			Dirty = true;
			return true;
		}

		private bool TrySynchronizeLoadedWorld(out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			XRLGame game = The.Game;
			KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
			if (game == null || kingdom == null || !kingdom.Founded)
			{
				return true;
			}
			if (!EnsureIdentity(kingdom, out Failure))
			{
				return false;
			}
			if (!string.IsNullOrEmpty(PendingAccessionToken))
			{
				return TryCompletePendingGeneration(kingdom, out Failure);
			}
			KingdomSealStore store = GetStore();
			KingdomSealRecord stage = store.ReadStage(OriginGameId);
			if (IsGenerationSealed)
			{
				if (stage != null && stage.Status == KingdomSealStatus.Retired
					&& SameCurrentIdentity(stage))
				{
					return TryCompleteRetirement(stage, out Failure);
				}
				string retirementPrimaryFailure;
				if (ExactPrimaryState(OriginGameId, out retirementPrimaryFailure)
					!= KingdomSealPrimaryState.Present)
				{
					Failure = "the saved retirement differs from its journal, but its exact primary could not be proved standing"
						+ (string.IsNullOrEmpty(retirementPrimaryFailure) ? ""
							: ": " + retirementPrimaryFailure);
					return false;
				}
				if (!store.TryRestoreRetiredGeneration(new KingdomSealLineage(LineageId,
					LegacyId, OriginGameId, Generation, Revision), out Failure))
				{
					return false;
				}
				stage = store.ReadStage(OriginGameId);
				if (stage == null || stage.Status != KingdomSealStatus.Retired
					|| !SameCurrentIdentity(stage))
				{
					Failure = "the saved retirement did not restore one exact retired journal";
					return false;
				}
				return TryCompleteRetirement(stage, out Failure);
			}
			if (stage == null)
			{
				Dirty = true;
				return TryFlushLiving("loaded world", ProbeEvenIfClean: true, out Failure);
			}
			if (stage.Status == KingdomSealStatus.Retired && SameCurrentIdentity(stage))
			{
				return TryCompleteRetirement(stage, out Failure);
			}

			KingdomSealRecord saved;
			if (!TryCapture(kingdom, LegacyId, Generation, Revision,
				SafeTick(game.TimeTicks), out saved, out Failure))
			{
				return false;
			}
			if (!KingdomSealEngineRules.MayRestoreLoadedPrimary(stage, saved))
			{
				Failure = "the loaded primary is not the same as, or older than, a living or terminal stage";
				return false;
			}
			string primaryFailure;
			if (ExactPrimaryState(OriginGameId, out primaryFailure) != KingdomSealPrimaryState.Present)
			{
				Failure = "the loaded generation differs from its stage, but its exact primary could not be proved standing"
					+ (string.IsNullOrEmpty(primaryFailure) ? "" : ": " + primaryFailure);
				return false;
			}
			if (stage.Status == KingdomSealStatus.Terminal)
			{
				int canceledRevision;
				if (!KingdomSealEngineRules.TryNextRevision(stage.Revision,
					out canceledRevision))
				{
					Failure = "the terminal attempt revision is exhausted";
					return false;
				}
				KingdomSealRecord canceled;
				if (!TryCapture(kingdom, stage.LegacyId, stage.Generation,
					canceledRevision, SafeTick(game.TimeTicks), out canceled, out Failure)
					|| !store.TryStage(canceled, out Failure))
				{
					return false;
				}
				stage = canceled;
			}
			if (!store.TryRestoreLivingGeneration(saved, out Failure))
			{
				// One living write may sit beside the terminal it canceled. A higher living
				// revision replaces that remaining slot; Store then accepts the exact primary.
				int scrubRevision;
				if (!KingdomSealEngineRules.TryNextRevision(stage.Revision, out scrubRevision))
				{
					Failure = "the recoverable living journal revision is exhausted";
					return false;
				}
				KingdomSealRecord scrub;
				if (!TryCapture(kingdom, stage.LegacyId, stage.Generation,
					scrubRevision, SafeTick(game.TimeTicks), out scrub, out Failure)
					|| !store.TryStage(scrub, out Failure)
					|| !store.TryRestoreLivingGeneration(saved, out Failure))
				{
					return false;
				}
			}
			Revision = saved.Revision;
			Dirty = false;
			DirtyReason = null;
			return true;
		}

		private bool EnsureIdentity(KingdomSystem Kingdom, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			XRLGame game = The.Game;
			if (game == null || Kingdom == null || !Kingdom.Founded
				|| !KingdomSealReceipt.ValidId(game.GameID))
			{
				Failure = "the founded realm or its game has no valid identity";
				return false;
			}
			if (HasCompleteIdentity)
			{
				if (OriginGameId != game.GameID)
				{
					Failure = "the saved seal identity belongs to another game";
					return false;
				}
				return true;
			}
			if (!string.IsNullOrEmpty(LineageId) || !string.IsNullOrEmpty(LegacyId)
				|| !string.IsNullOrEmpty(OriginGameId))
			{
				Failure = "the saved seal identity is incomplete";
				return false;
			}

			KingdomSealRecord staged = GetStore().ReadStage(game.GameID);
			if (staged != null && staged.OriginGameId == game.GameID
				&& KingdomSealReceipt.ValidId(staged.LineageId)
				&& KingdomSealReceipt.ValidId(staged.LegacyId))
			{
				AdoptIdentity(staged);
				if (staged.Status == KingdomSealStatus.Retired)
				{
					SealedLegacyId = staged.LegacyId;
				}
				return true;
			}
			LineageId = MintId();
			LegacyId = MintId();
			OriginGameId = game.GameID;
			Generation = 0;
			Revision = 0;
			SealedLegacyId = "";
			Dirty = true;
			return true;
		}

		private bool TryCapture(KingdomSystem Kingdom, string CaptureLegacyId,
			int CaptureGeneration, int CaptureRevision, long WrittenTick,
			out KingdomSealRecord Record, out string Failure)
		{
			Record = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Kingdom == null || !Kingdom.Founded || !KingdomSealReceipt.ValidId(LineageId)
					|| !KingdomSealReceipt.ValidId(CaptureLegacyId)
					|| !KingdomSealReceipt.ValidId(OriginGameId))
				{
					Failure = "the living snapshot has no complete lineage identity";
					return false;
				}
				string founder = The.Player?.BaseDisplayNameStripped;
				if (string.IsNullOrEmpty(founder))
				{
					founder = The.Game?.PlayerName;
				}
				Record = KingdomSealRules.Capture(Kingdom.Capture(),
					new KingdomSealLineage(LineageId, CaptureLegacyId, OriginGameId,
						CaptureGeneration, CaptureRevision),
					Kingdom.KingdomDisplayName, founder, Kingdom.ChronicleEntries,
					Kingdom.OutsiderEntries, WrittenTick);
				Record.WriterVersion = VersionOf(typeof(KingdomSeal).Assembly);
				Record.EngineVersion = VersionOf(typeof(XRLGame).Assembly);
				// Compose/read validates bounds and derived vigour before any store path is touched.
				KingdomSealRecord echo;
				KingdomSealFault fault;
				string detail;
				if (!KingdomSealRecord.TryParse(Record.Compose(), out echo, out fault, out detail))
				{
					Failure = string.IsNullOrEmpty(detail) ? "the coherent snapshot did not validate" : detail;
					Record = null;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Record = null;
				Failure = ex.Message;
				return false;
			}
		}

		private void TryReconcileProfile(string Reason)
		{
			if (ReconcileInProgress || !AuthorityEnabled || !SealEnabled())
			{
				return;
			}
			ReconcileInProgress = true;
			try
			{
				KingdomSealStore store = GetStore();
				int refusedReceipts;
				List<KingdomSealReceipt> receipts = store.ReadReceipts(out refusedReceipts);
				if (refusedReceipts > 0)
				{
					ReportFailure(Reason,
						"one or more receipt files could not be validated; reconciliation was refused");
					return;
				}
				int refusedStages;
				List<string> origins = store.StagedOrigins(out refusedStages);
				if (refusedStages > 0)
				{
					ReportFailure(Reason,
						"one or more stage filenames or the bounded stage scan could not be validated");
					return;
				}
				for (int i = 0; i < origins.Count; i++)
				{
					KingdomSealRecord stage = store.ReadStage(origins[i]);
					if (stage == null)
					{
						ReportFailure(Reason, "the stage journal for " + origins[i]
							+ " could not be validated");
						return;
					}
					if (stage.Status != KingdomSealStatus.Terminal)
					{
						continue;
					}
					string scoreFailure;
					bool exactScore;
					if (!TryExactScore(stage.OriginGameId, out exactScore, out scoreFailure))
					{
						ReportFailure("score proof for " + stage.OriginGameId, scoreFailure);
						continue;
					}
					string primaryFailure;
					KingdomSealPrimaryState primary = ExactPrimaryState(stage.OriginGameId,
						out primaryFailure);
					if (primary == KingdomSealPrimaryState.Unknown)
					{
						ReportFailure("primary proof for " + stage.OriginGameId, primaryFailure);
						continue;
					}
					if (!KingdomSealEngineRules.MayPromote(stage.Status, exactScore, primary))
					{
						continue;
					}
					KingdomSealRecord promoted = KingdomSealRules.Promote(stage,
						KingdomSealEligibility.Ended);
					string failure;
					if (!store.TryWriteLegacy(promoted, out failure))
					{
						ReportFailure("legacy promotion for " + stage.LegacyId, failure);
					}
				}

				Dictionary<string, KingdomSealPrimaryState> primaryByTarget =
					new Dictionary<string, KingdomSealPrimaryState>();
				for (int i = 0; i < receipts.Count; i++)
				{
					KingdomSealReceipt receipt = receipts[i];
					if (receipt.State != KingdomSealReceiptState.Reserved)
					{
						continue;
					}
					KingdomSealPrimaryState primary;
					if (!primaryByTarget.TryGetValue(receipt.TargetGameId, out primary))
					{
						string primaryFailure;
						primary = ExactPrimaryState(receipt.TargetGameId, out primaryFailure);
						primaryByTarget[receipt.TargetGameId] = primary;
						if (primary == KingdomSealPrimaryState.Unknown)
						{
							ReportFailure("receipt primary proof for " + receipt.TargetGameId,
								primaryFailure);
						}
					}
					if (primary == KingdomSealPrimaryState.Absent)
					{
						bool released;
						string releaseFailure;
						if (!store.TryReleaseAbandonedReservation(receipt, out released,
							out releaseFailure))
						{
							ReportFailure("abandoned reservation for " + receipt.LegacyId,
								releaseFailure);
						}
						else if (released)
						{
							KingdomLog.Log("seal: released interrupted import " + receipt.LegacyId
								+ " -> " + receipt.TargetGameId);
						}
						continue;
					}
					// A primary save proves only that the target exists. The inheritance owner
					// commits after Applied/AlreadyApplied and exact marker/object proof.
				}
			}
			catch (Exception ex)
			{
				ReportFailure(Reason, ex.Message, ex);
			}
			finally
			{
				ReconcileInProgress = false;
			}
		}

		private KingdomSealStore GetStore()
		{
			if (!AuthorityEnabled)
			{
				throw new InvalidOperationException(
					"The saved seal coordinator is disabled for this save.");
			}
			if (Store == null)
			{
				Store = new KingdomSealStore(ProfileRootPath());
			}
			return Store;
		}

		private bool SameCurrentIdentity(KingdomSealRecord Record)
		{
			return Record != null && Record.LineageId == LineageId
				&& Record.LegacyId == LegacyId && Record.OriginGameId == OriginGameId
				&& Record.Generation == Generation;
		}

		private bool HasCompleteIdentity => KingdomSealReceipt.ValidId(LineageId)
			&& KingdomSealReceipt.ValidId(LegacyId)
			&& KingdomSealReceipt.ValidId(OriginGameId)
			&& Generation >= 0 && Generation <= 1024 && Revision >= 0;

		private void AdoptIdentity(KingdomSealRecord Record)
		{
			LineageId = Record.LineageId;
			LegacyId = Record.LegacyId;
			OriginGameId = Record.OriginGameId;
			Generation = Record.Generation;
			Revision = Record.Revision;
		}

		private void ValidateSavedState()
		{
			if (SealDisabled)
			{
				if (!KingdomSealEngineRules.IsCanonicalDisabledSealShape(LineageId, LegacyId,
					OriginGameId, Generation, Revision, LastPollTick, SealedLegacyId,
					LastAccessionToken, PendingAccessionToken))
				{
					throw new InvalidOperationException(
						"The disabled saved seal coordinator is not canonical.");
				}
				NeutralizeDisabledState();
				return;
			}
			bool none = string.IsNullOrEmpty(LineageId) && string.IsNullOrEmpty(LegacyId)
				&& string.IsNullOrEmpty(OriginGameId);
			if (!none && !HasCompleteIdentity)
			{
				throw new InvalidOperationException("The saved seal lineage identity is incomplete.");
			}
			if (!string.IsNullOrEmpty(SealedLegacyId)
				&& (!KingdomSealReceipt.ValidId(SealedLegacyId) || SealedLegacyId != LegacyId))
			{
				throw new InvalidOperationException("The saved retirement marker does not name the current legacy.");
			}
			LineageId = LineageId ?? "";
			LegacyId = LegacyId ?? "";
			OriginGameId = OriginGameId ?? "";
			SealedLegacyId = SealedLegacyId ?? "";
			LastAccessionToken = LastAccessionToken ?? "";
			PendingAccessionToken = PendingAccessionToken ?? "";
			string accessionFailure;
			if (none)
			{
				if (LastAccessionToken.Length != 0 || PendingAccessionToken.Length != 0)
				{
					throw new InvalidOperationException("An unfounded seal cannot carry accession state.");
				}
			}
			else if (!KingdomSealEngineRules.TryValidateAccessionTokens(Generation,
				LastAccessionToken, PendingAccessionToken, out accessionFailure))
			{
				throw new InvalidOperationException("The saved accession identity is invalid: "
					+ accessionFailure + ".");
			}
			LastPollTick = SafeTick(LastPollTick);
		}

		private static bool HasExactLegacy(KingdomSealStore Store, string Wanted,
			out string Failure)
		{
			Failure = "";
			int refused;
			List<KingdomSealRecord> legacies = Store.ReadLegacies(out refused);
			if (refused > 0)
			{
				Failure = "one or more immutable legacy files could not be validated";
				return false;
			}
			for (int i = 0; i < legacies.Count; i++)
			{
				if (legacies[i].LegacyId == Wanted)
				{
					return true;
				}
			}
			Failure = "the save marks legacy " + Wanted + " retired, but its immutable file is missing";
			return false;
		}

		private static bool TryExactScore(string Origin, out bool ExactScore, out string Failure)
		{
			ExactScore = false;
			Failure = "";
			if (!KingdomSealReceipt.ValidId(Origin))
			{
				Failure = "the terminal stage has no valid origin id";
				return false;
			}
			try
			{
				Scoreboard2 scoreboard = Scores.Scoreboard;
				if (scoreboard == null || scoreboard.Scores == null)
				{
					Failure = "the scoreboard could not be read";
					return false;
				}
				if (scoreboard.Scores.Count > MaxScoresScanned)
				{
					Failure = "the scoreboard exceeds the bounded reconciliation scan";
					return false;
				}
				for (int i = 0; i < scoreboard.Scores.Count; i++)
				{
					ScoreEntry2 entry = scoreboard.Scores[i];
					if (entry != null && string.Equals(entry.GameId, Origin, StringComparison.Ordinal))
					{
						ExactScore = true;
						break;
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		private static KingdomSealPrimaryState ExactPrimaryState(string GameId,
			out string Failure)
		{
			try
			{
				return KingdomSealEngineRules.ExactPrimaryAcrossRoots(GameId,
					new[] { DataManager.SyncedPath("Saves"), DataManager.SavePath("Saves") },
					MaxSaveDirectoriesScanned, MaxSaveEntriesScanned, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return KingdomSealPrimaryState.Unknown;
			}
		}

		private static KingdomSealReceipt CopyReceipt(KingdomSealReceipt Source,
			KingdomSealReceiptState State, long WrittenTick)
		{
			return new KingdomSealReceipt
			{
				LineageId = Source.LineageId,
				LegacyId = Source.LegacyId,
				TargetGameId = Source.TargetGameId,
				State = State,
				WrittenTick = Math.Max(Source.WrittenTick, SafeTick(WrittenTick))
			};
		}

		private static bool IsKingdomMode(XRLGame Game)
		{
			return Game != null && KingdomSuccessionRules.ModeOn(Game.gameMode,
				Game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey));
		}

		private static bool SealEnabled()
		{
			return Options.GetOption("r_TAF_OptionSeal", "Yes") != "No";
		}

		private static bool LegacyImportEnabled()
		{
			return Options.GetOption("r_TAF_OptionLegacyImport", "Yes") != "No";
		}

		private static string DeathReason(AfterDieEvent Death)
		{
			if (!string.IsNullOrEmpty(Death?.ThirdPersonReason))
			{
				return Death.ThirdPersonReason;
			}
			if (!string.IsNullOrEmpty(Death?.Reason))
			{
				return Death.Reason;
			}
			return "died, and no one living can say how";
		}

		private static string DeathCategory(AfterDieEvent Death)
		{
			string category = Death?.Dying?.Physics?.LastDeathCategory;
			return string.IsNullOrEmpty(category) ? "unknown" : category;
		}

		private static string MintId()
		{
			return Guid.NewGuid().ToString("N");
		}

		private static string VersionOf(Assembly Assembly)
		{
			try
			{
				return Assembly?.GetName()?.Version?.ToString() ?? "unknown";
			}
			catch (Exception)
			{
				return "unknown";
			}
		}

		private static long SafeTick(long Tick)
		{
			return Tick < 0L ? 0L : Tick;
		}

		private void ReportFailure(string Action, string Failure, Exception Exception = null)
		{
			string failure = string.IsNullOrEmpty(Failure) ? "unknown failure" : Failure;
			string key = (Action ?? "seal") + "\u001f" + failure;
			if (string.Equals(LastFailureKey, key, StringComparison.Ordinal))
			{
				return;
			}
			LastFailureKey = key;
			try
			{
				Exception error = Exception ?? new InvalidOperationException(failure);
				MetricsManager.LogError("ThousandAndFirst: seal " + (Action ?? "action") + " failed closed", error);
				KingdomLog.Log("seal: " + (Action ?? "action") + " failed closed (" + failure + ")");
			}
			catch (Exception)
			{
				// A diagnostic failure must never escape into the game loop.
			}
		}

		private static void LogStaticFailure(string Action, Exception Exception)
		{
			try
			{
				MetricsManager.LogError("ThousandAndFirst: seal " + Action + " failed closed", Exception);
				KingdomLog.Log("seal: " + Action + " failed closed (" + Exception.GetType().Name
					+ ": " + Exception.Message + ")");
			}
			catch (Exception)
			{
			}
		}
	}
}
