using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the lab as a place: dressing a carcass, keeping what comes off,
	/// and the slate the founder reads at the hall.
	/// <para>
	/// <b>One screen, two levels, both <c>Popup.PickOption</c>, and no new screen class.</b> That is
	/// simultaneously the vanilla golem quest's shape, Playable Golem's shape and the precedent's
	/// control-menu shape &mdash; which is not a coincidence; it is Qud's house idiom, and this
	/// system has no reason to be the exception.
	/// </para>
	/// <para>
	/// Two things are inherited on purpose and both were expensive to learn elsewhere. Effects are
	/// shown BEFORE commitment, prefixed <c>{{rules|--}}</c>, because the one documented complaint
	/// about the vanilla picker is that players cannot tell what a choice will do; and the
	/// three-way consent prompt's third answer writes to a permanent exclusion list, because a
	/// founder who says never should be believed.
	/// </para>
	/// <para>
	/// Every decision that does not need a real object &mdash; what a body will take, where a part
	/// has to sit, what a thing costs, every sentence a refusal is told with &mdash; is delegated to
	/// the engine-free <see cref="KingdomProcedureRules"/> and <see cref="KingdomLabRules"/>.
	/// </para>
	/// </summary>
	internal static partial class KingdomLab
	{
		/// <summary>The property a preserved part carries to mark it as the vat-house's own work,
		/// so a founder's own jerky is never mistaken for a graftable organ (the protection law: we
		/// count only what we made and marked).</summary>
		internal const string KeptProperty = "r_TAF_LabKept";

		private const string VatPendingProperty = "r_TAF_VatPending";
		private const string VatRemainingProperty = "r_TAF_VatRemaining";
		private const string VatResultProperty = "r_TAF_VatResult";
		private const string VatYieldProperty = "r_TAF_VatYield";
		private const string VatJobProperty = "r_TAF_VatJob";
		private const string VatReadyProperty = "r_TAF_VatReady";
		private const string VatOutputJobProperty = "r_TAF_VatOutputJob";
		private const string VatOutputIdProperty = "r_TAF_VatOutputId";
		private const string VatOutputFingerprintProperty = "r_TAF_VatOutputFingerprint";
		private const string VatOutputPhaseProperty = "r_TAF_VatOutputPhase";
		private const string VatRawPhaseProperty = "r_TAF_VatRawPhase";
		private const string VatRawIdProperty = "r_TAF_VatRawId";
		private const string VatRawBlueprintProperty = "r_TAF_VatRawBlueprint";
		private const string VatRawCountProperty = "r_TAF_VatRawCount";
		private const string VatRawFingerprintProperty = "r_TAF_VatRawFingerprint";
		private const string VatOwnerIdProperty = "r_TAF_VatOwnerId";
		private const string VatBlockedProperty = "r_TAF_VatBlocked";
		private const string LabRegistryState = "r_TAF_LabJobRegistry_v1";
		private const string LabReplayState = "r_TAF_LabReplayProof_v1";

		private sealed class KeptSpendPreparation
		{
			public readonly List<GameObject> Sources;
			public readonly List<string> Stamps;
			public readonly List<GameObject> Owners;
			public readonly List<Cell> Cells;
			public readonly List<string> Ids;
			public readonly List<string> Blueprints;
			public readonly LabProcedure Procedure;
			public readonly KingdomKeptSpendPlan Plan;

			public KeptSpendPreparation(List<GameObject> Sources, List<string> Stamps,
				List<GameObject> Owners, List<Cell> Cells, List<string> Ids,
				List<string> Blueprints, LabProcedure Procedure, KingdomKeptSpendPlan Plan)
			{
				this.Sources = Sources;
				this.Stamps = Stamps;
				this.Owners = Owners;
				this.Cells = Cells;
				this.Ids = Ids;
				this.Blueprints = Blueprints;
				this.Procedure = Procedure;
				this.Plan = Plan;
			}
		}

		private static string RealmIdentity(KingdomSystem System)
		{
			return System?.CurrentRealmId;
		}

		private static KingdomLabMessagePhase PublishMessage(ref int StoredPhase,
			ref string FrozenText, string EventId, string Text, bool ShouldPublish = true)
		{
			KingdomLabMessagePhase phase = KingdomLabRules.ResumeMessage(
				(KingdomLabMessagePhase)StoredPhase);
			StoredPhase = (int)phase;
			if (KingdomLabRules.MessageSettled(phase)) return phase;
			if (phase != KingdomLabMessagePhase.Pending || string.IsNullOrEmpty(EventId))
			{
				StoredPhase = (int)KingdomLabMessagePhase.Lost;
				return KingdomLabMessagePhase.Lost;
			}
			FrozenText = Text ?? "";
			if (!ShouldPublish || string.IsNullOrEmpty(FrozenText))
			{
				StoredPhase = (int)KingdomLabMessagePhase.Skipped;
				return KingdomLabMessagePhase.Skipped;
			}
			StoredPhase = (int)KingdomLabMessagePhase.Intent;
			try
			{
				MessageQueue.AddPlayerMessage(FrozenText);
				StoredPhase = (int)KingdomLabMessagePhase.Delivered;
				return KingdomLabMessagePhase.Delivered;
			}
			catch (Exception ex)
			{
				StoredPhase = (int)KingdomLabMessagePhase.Lost;
				KingdomLog.Log("lab: message intent " + EventId
					+ " returned unknown/lost (" + ex.Message + ")");
				return KingdomLabMessagePhase.Lost;
			}
		}

		private static KingdomLabRegistryEntry RegistryEntry(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			return new KingdomLabRegistryEntry
			{
				JobId = Job?.JobId ?? "",
				BuildingId = Job?.BuildingId ?? "",
				PatientId = Job?.PatientId ?? "",
				GameId = Job?.GameId ?? "",
				RealmId = Job?.RealmId ?? "",
				RealmFoundedTick = Job?.RealmFoundedTick ?? -1L,
				RulerSuccessionOrdinal = Job?.RulerSuccessionOrdinal ?? -1,
				RulerLifeId = Job?.RulerLifeId ?? "",
				ContractVersion = Job?.ContractVersion ?? 0,
				ProcedureKey = Job?.ProcedureKey ?? "",
				Grants = Job?.FrozenGrants ?? "",
				Source = Job?.FrozenSource ?? -1,
				Attach = Job?.FrozenAttach ?? -1,
				Manager = Job?.FrozenManager ?? "",
				Detail = Job?.FrozenDetail ?? "",
				Fingerprint = Job?.FrozenFingerprint ?? "",
				Status = Status,
				UpdatedTick = Math.Max(0L, The.Game?.TimeTicks ?? 0L)
			};
		}

		private static bool WriteCanonical(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (The.Game == null || Job == null) return false;
			bool replayMalformed;
			if (Status == KingdomLabRegistryStatus.Active
				&& KingdomLabRules.ReplayContains(
					The.Game.GetStringGameState(LabReplayState, ""), "apply:" + Job.JobId,
					out replayMalformed)) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			KingdomLabRegistryEntry expected = RegistryEntry(Job, Status);
			if (quarantined || !KingdomLabRules.UpsertRegistry(rows, expected)) return false;
			string written = KingdomLabRules.FormatRegistry(rows);
			The.Game.SetStringGameState(LabRegistryState, written);
			if (!string.Equals(The.Game.GetStringGameState(LabRegistryState, ""), written,
				StringComparison.Ordinal)) return false;
			rows = KingdomLabRules.ParseRegistry(written, out quarantined);
			int at = KingdomLabRules.IndexOfRegistry(rows, Job.JobId);
			return !quarantined && at >= 0 && rows[at].Status == Status
				&& KingdomLabRules.RegistryAuthority(rows[at], expected,
					RequireActive: Status == KingdomLabRegistryStatus.Active);
		}

		private static bool CanonicalAuthority(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (The.Game == null || Job == null) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			int at = KingdomLabRules.IndexOfRegistry(rows, Job.JobId);
			return !quarantined && at >= 0 && rows[at].Status == Status
				&& KingdomLabRules.RegistryAuthority(rows[at], RegistryEntry(Job, Status),
					RequireActive: Status == KingdomLabRegistryStatus.Active);
		}

		private static bool RecordReplayProof(string StableId)
		{
			if (The.Game == null || string.IsNullOrEmpty(StableId)) return false;
			string written;
			if (!KingdomLabRules.AddReplayProof(
				The.Game.GetStringGameState(LabReplayState, ""), StableId, out written)) return false;
			The.Game.SetStringGameState(LabReplayState, written);
			bool malformed;
			return string.Equals(The.Game.GetStringGameState(LabReplayState, ""), written,
				StringComparison.Ordinal)
				&& KingdomLabRules.ReplayContains(written, StableId, out malformed) && !malformed;
		}

		private static bool PurgeCanonical(r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (The.Game == null || Job == null || Status == KingdomLabRegistryStatus.Active
				|| !RecordReplayProof("apply:" + Job.JobId)) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			int at = KingdomLabRules.IndexOfRegistry(rows, Job.JobId);
			if (quarantined || at < 0 || rows[at].Status != Status
				|| !KingdomLabRules.RegistryAuthority(rows[at], RegistryEntry(Job, Status),
					RequireActive: false)
				|| !KingdomLabRules.RemoveRegistry(rows, Job.JobId, Status)) return false;
			string written = KingdomLabRules.FormatRegistry(rows);
			The.Game.SetStringGameState(LabRegistryState, written);
			rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			return !quarantined && KingdomLabRules.IndexOfRegistry(rows, Job.JobId) < 0;
		}

		private static bool PurgeApplicationReceipt(GameObject Building, GameObject Actor,
			KingdomSystem System, r_KingdomLabJob Job, KingdomLabRegistryStatus Status)
		{
			if (Building == null || Job == null
				|| !ReferenceEquals(Job.ParentObject, Building)) return false;
			if (Status == KingdomLabRegistryStatus.Complete)
			{
				SettleCompletedBodyHistory(Actor, System, Job);
				if (!KingdomLabBodyHistoryContractRules.AllowsPhysicalCleanup(
					Job.BodyHistoryState))
				{
					Job.Fault = "Physical procedure is complete; body history still waits: "
						+ Job.BodyHistoryFault;
					return false;
				}
			}
			if (!RecordReplayProof("apply:" + Job.JobId)) return false;
			try { Building.RemovePart(Job); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact terminal job cleanup threw (" + ex.Message + ")");
			}
			if (ReferenceEquals(Job.ParentObject, Building)
				|| KingdomProcedures.ReferencePartOrdinal(Building, Job) >= 0) return false;
			return PurgeCanonical(Job, Status);
		}

		private static bool CurrentAuthority(GameObject Building, GameObject Actor,
			KingdomSystem System, r_KingdomLabJob Job, KingdomLabRegistryStatus Status)
		{
			return Building != null && Actor != null && System != null && Job != null && The.Game != null
				&& string.Equals(Actor.ID, Job.PatientId, StringComparison.Ordinal)
				&& string.Equals(Building.ID, Job.BuildingId, StringComparison.Ordinal)
				&& string.Equals(The.Game.GameID, Job.GameId, StringComparison.Ordinal)
				&& string.Equals(RealmIdentity(System), Job.RealmId, StringComparison.Ordinal)
				&& System.FoundedTick == Job.RealmFoundedTick
				&& (Status != KingdomLabRegistryStatus.Active
					|| !KingdomLabRules.ReplayContains(
						The.Game.GetStringGameState(LabReplayState, ""),
						"apply:" + Job.JobId, out _))
				&& CanonicalAuthority(Job, Status);
		}

	}
}
