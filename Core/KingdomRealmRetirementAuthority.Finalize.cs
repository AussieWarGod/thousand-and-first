using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		public static bool TryFinalizeForRemoval(KingdomSystem System,
			out KingdomRealmRetirementReport Report, out string Failure)
		{
			Report = null; Failure = null;
			if (System == null || !System.TryReadRealmRetirement(
				out KingdomRealmRetirementState state, out Failure) || state == null)
				return Fail(Failure ?? "realm-removal receipt is absent", out Failure);
			if (state.Phase == KingdomRealmRetirementPhase.CleaningGround)
			{
				if (!TryCloseKnownProjections(System, ref state, out Failure))
				{
					Report = FromState(state); return false;
				}
			}
			long tick = Math.Max(state.UpdatedTick, The.Game?.TimeTicks ?? 0L);
			if (state.Phase == KingdomRealmRetirementPhase.ReadyForFence)
			{
				if (!KingdomIdentityFenceRuntime.TryCommitRemovalFence(System, state, tick,
					out KingdomRealmRetirementState committed, out Failure)
					|| !TryPublish(System, state, committed, out Failure))
				{
					Report = FromState(state); return false;
				}
				state = committed;
			}
			if (state.Phase == KingdomRealmRetirementPhase.FenceCommitted)
			{
				if (!KingdomRealmRetirementRules.TrySetPhase(state, state.Revision,
					KingdomRealmRetirementPhase.FenceCommitted,
					KingdomRealmRetirementPhase.PreparedForRemoval, state.UpdatedTick,
					out KingdomRealmRetirementState prepared, out Failure)
					|| !TryPublish(System, state, prepared, out Failure))
				{
					Report = FromState(state); return false;
				}
				state = prepared;
			}
			if (state.Phase != KingdomRealmRetirementPhase.PreparedForRemoval)
				return Fail("realm-removal authority is not ready for final carrier removal",
					out Failure);
			Report = FromState(state);
			if (!KingdomIdentityFenceRuntime.TryVerifyPreparedRemoval(System, state,
				out Failure) || !TryCutTerminalProjections(System, state, out Failure)) return false;
			if (!KingdomIdentityFenceRuntime.TryVerifyPreparedRemoval(System, state,
				out Failure)) return false;
			string terminalCallback = null;
			try { The.Game.RemoveSystem(System); }
			catch (Exception ex) { terminalCallback = ex.Message; }
			if (!KingdomRealmRemovalRetryRules.TerminalSystemRemovalSettled(
				The.Game.Systems.Contains(System), terminalCallback != null))
				return Fail("the exact KingdomSystem carrier remains registered after its final cut",
					out Failure);
			Report.Summary = "The attended current-session carrier cut is complete. Save now and quit. Loading without the mod relies only on the retained base-game fence; no mod-absent cleanup or clean uninstall is promised.";
			if (terminalCallback != null)
				Report.Summary += " The final system callback threw after native registry removal; registry absence is the terminal result (" + terminalCallback + ").";
			return true;
		}

		private static bool TryCloseKnownProjections(KingdomSystem System,
			ref KingdomRealmRetirementState State, out string Failure)
		{
			Failure = null;
			if (!TryCurrentDigests(System, State.Locators, out string _,
				out string authority, out Failure) || authority != State.AuthorityDigest)
				return Fail("realm authority diverged before global cleanup", out Failure);
			long tick = Math.Max(State.UpdatedTick, The.Game?.TimeTicks ?? 0L);
			if (!TryBuildFinalPlan(System, State, out KingdomRealmRemovalFinalPlan plan,
				out Failure)) return false;
			for (int i = 0; i < plan.PreviewRecords.Count; i++)
				if (!PublishRecord(System, ref State, plan.PreviewRecords[i], tick,
					out Failure)) return false;
			if (!TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !KingdomPolityRemovalRuntime.TryPrepareFinalRetirement(System, tick,
					out KingdomPolityFinalRetirementPlan polityPlan, out Failure)
				|| !TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !KingdomPolityRemovalRuntime.TryApplyFinalRetirement(System, polityPlan,
					out Failure)) return false;

			if (!TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !KingdomRaids.TryRetireRecoveryQuests(System, out int _, out Failure)
				|| !TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !CallbackFamilySettled(System, State, "quests", out Failure)
				|| !PublishNamedCompletion(System, ref State, plan, "quests", tick, out Failure))
				return false;
			if (!TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !KingdomRemovalProjectionRuntime.TryConvertCooking(out int _, out Failure)
				|| !TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !CallbackFamilySettled(System, State, "recipes", out Failure)
				|| !PublishNamedCompletion(System, ref State, plan, "recipes", tick, out Failure))
				return false;
			if (!TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !KingdomRemovalProjectionRuntime.TryConvertJournal(out int _, out Failure)
				|| !TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !CallbackFamilySettled(System, State, "journal", out Failure)
				|| !PublishNamedCompletion(System, ref State, plan, "journal", tick, out Failure))
				return false;
			if (!TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !TryRetireCivicSemantics(System, State, out Failure)
				|| !TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !CallbackFamilySettled(System, State, "civic-semantics", out Failure)
				|| !PublishNamedCompletion(System, ref State, plan, "civic-semantics", tick,
					out Failure)) return false;
			if (!TryBuildFinalPlan(System, State, out plan, out Failure)) return false;
			KingdomRemovalRecord faction = FindRecord(State,
				KingdomRemovalProjectionKind.Faction, "taf:removal-preview:factions:v1");
			if (faction == null || !TryFrozenCutRows(State, "factions",
				KingdomRemovalProjectionKind.Faction, false, out List<string> frozenFactions,
				out Failure) || !KingdomRemovalProjectionRuntime.TryRetireFactions(System,
				plan.Factions, State.Locators, frozenFactions, out Failure)
				|| !TryBuildFinalPlan(System, State, out plan, out Failure)
				|| !CallbackFamilySettled(System, State, "factions", out Failure)
				|| !PublishNamedCompletion(System, ref State, plan, "factions", tick,
					out Failure)) return false;
			if (!TryBuildFinalPlan(System, State, out plan, out Failure)) return false;
			string[] terminal = { "systems", "global-state", "roster", "player" };
			for (int i = 0; i < terminal.Length; i++)
				if (!PublishNamedCompletion(System, ref State, plan, terminal[i], tick,
					out Failure)) return false;
			if (!PublishRecord(System, ref State, plan.CompletionRecords[
				plan.CompletionRecords.Count - 1], tick, out Failure)) return false;
			if (!TryCurrentDigests(System, State.Locators, out string _,
				out string afterAuthority, out Failure) || afterAuthority != State.AuthorityDigest)
				return Fail("realm identity changed during global cleanup", out Failure);
			if (!TryBuildFinalPlan(System, State, out KingdomRealmRemovalFinalPlan _,
				out Failure) || !KingdomRealmRetirementRules.TrySetPhase(State, State.Revision,
				KingdomRealmRetirementPhase.CleaningGround,
				KingdomRealmRetirementPhase.ReadyForFence, tick,
				out KingdomRealmRetirementState ready, out Failure)
				|| !TryPublish(System, State, ready, out Failure)) return false;
			State = ready; return true;
		}

		private static bool TryRetireCivicSemantics(KingdomSystem System,
			KingdomRealmRetirementState State, out string Failure)
		{
			KingdomRemovalRecord preview = FindRecord(State,
				KingdomRemovalProjectionKind.JournalHistory,
				"taf:removal-preview:civic-semantics:v1");
			if (preview == null || preview.Disposition != KingdomRemovalDisposition.Preserved
				|| !KingdomRealmRetirementRules.Digest(preview.AfterDigest))
				return Fail("civic retirement projection lacks its frozen terminal digest",
					out Failure);
			return KingdomRemovalProjectionRuntime.TryRetireCivicSemantics(System,
				State.StartedTick, preview.AfterDigest, out int _, out Failure);
		}

		private static bool PublishNamedCompletion(KingdomSystem System,
			ref KingdomRealmRetirementState State, KingdomRealmRemovalFinalPlan Plan,
			string Slug, long Tick, out string Failure)
		{
			Failure = null;
			string id = "taf:removal-complete:" + Slug + ":v1";
			for (int i = 0; i < (Plan?.CompletionRecords?.Count ?? 0); i++)
				if (Plan.CompletionRecords[i].Id == id)
					return PublishRecord(System, ref State, Plan.CompletionRecords[i], Tick,
						out Failure);
			return Fail("final plan lacks completion family " + Slug, out Failure);
		}

		private static bool TryCutTerminalProjections(KingdomSystem System,
			KingdomRealmRetirementState State, out string Failure)
		{
			Failure = null;
			KingdomRealmRemovalFinalPlan plan = new KingdomRealmRemovalFinalPlan();
			if (!KingdomRemovalProjectionRuntime.TryInspectSystems(System,
				out List<XRL.IGameSystem> systems, out List<string> systemRows, out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectGlobalStates(System, plan,
					out List<string> globalRows, out Failure)) return false;
			plan.Systems.AddRange(systems);
			if (!TryFrozenCutRows(State, "systems", KingdomRemovalProjectionKind.GlobalState,
				true, out List<string> frozenSystems, out Failure)
				|| KingdomRealmRemovalRetryRules.CutProgress(frozenSystems, systemRows, true)
					== KingdomRemovalCutProgress.Quarantine
				|| !TryFrozenCutRows(State, "global-state",
					KingdomRemovalProjectionKind.GlobalState, true,
					out List<string> frozenGlobals, out Failure)
				|| KingdomRealmRemovalRetryRules.CutProgress(frozenGlobals, globalRows, true)
					== KingdomRemovalCutProgress.Quarantine)
				return Fail(Failure ?? "terminal projection remainder is outside frozen authority",
					out Failure);
			bool rosterPresent = The.Game.HasIntGameState(KingdomSaveSystemRosterRules.StateKey);
			if (rosterPresent)
			{
				if (!KingdomRemovalProjectionRuntime.TryInspectRoster(out List<string> roster,
					out Failure) || !ExactTerminalFamily(State, "roster",
						KingdomRemovalProjectionKind.GlobalState, roster, out Failure)) return false;
			}
			KingdomRemovalRecord playerReceipt = FindRecord(State,
				KingdomRemovalProjectionKind.Ability,
				"taf:removal-complete:player:v1");
			if (!KingdomRemovalProjectionRuntime.TryAuthenticatePlayerCutProgress(
				playerReceipt, out bool playerAbsent, out Failure)) return false;
			if (rosterPresent)
			{
				if (!KingdomRemovalProjectionRuntime.TryInspectRoster(out List<string> exactRoster,
					out Failure) || !ExactTerminalFamily(State, "roster",
						KingdomRemovalProjectionKind.GlobalState, exactRoster, out Failure)
					|| !KingdomSaveSystemRosterRuntime.TryClearForPreparedRemoval(The.Game,
						out Failure)) return false;
			}
			if (globalRows.Count > 0)
			{
				KingdomRealmRemovalFinalPlan exactGlobals = new KingdomRealmRemovalFinalPlan();
				if (!KingdomRemovalProjectionRuntime.TryInspectGlobalStates(System, exactGlobals,
					out List<string> exactRows, out Failure)
					|| KingdomRealmRemovalRetryRules.CutProgress(frozenGlobals, exactRows, true)
						== KingdomRemovalCutProgress.Quarantine
					|| !KingdomRemovalProjectionRuntime.TryRemoveGlobalStates(System, exactGlobals,
						out Failure)) return false;
			}
			if (!playerAbsent)
			{
				if (!KingdomRemovalProjectionRuntime.TryAuthenticatePlayerCutProgress(
					playerReceipt, out playerAbsent, out Failure) || playerAbsent
					|| !KingdomRemovalProjectionRuntime.TryRemovePlayerProjection(playerReceipt,
						out int _, out Failure)) return false;
			}
			if (systems.Count > 0)
			{
				if (!KingdomRemovalProjectionRuntime.TryInspectSystems(System,
					out List<IGameSystem> exactSystems, out List<string> exactRows, out Failure)
					|| KingdomRealmRemovalRetryRules.CutProgress(frozenSystems, exactRows, true)
						== KingdomRemovalCutProgress.Quarantine)
					return false;
				plan.Systems.Clear(); plan.Systems.AddRange(exactSystems);
				if (!KingdomRemovalProjectionRuntime.TryRemoveAuxiliarySystems(System, plan,
					out int _, out Failure)) return false;
			}
			KingdomRealmRemovalFinalPlan verify = new KingdomRealmRemovalFinalPlan();
			return !The.Game.HasIntGameState(KingdomSaveSystemRosterRules.StateKey)
				&& KingdomRemovalProjectionRuntime.PlayerProjectionAbsent(out Failure)
				&& KingdomRemovalProjectionRuntime.TryInspectGlobalStates(System, verify,
					out globalRows, out Failure) && globalRows.Count == 0
				&& KingdomRemovalProjectionRuntime.TryInspectSystems(System, out systems,
					out systemRows, out Failure) && systemRows.Count == 0
				|| Fail(Failure ?? "a terminal projection remains after the base-fence cut",
					out Failure);
		}

		private static bool ExactTerminalFamily(KingdomRealmRetirementState State,
			string Slug, KingdomRemovalProjectionKind Kind, IList<string> Rows,
			out string Failure)
		{
			Failure = null;
			KingdomRemovalRecord receipt = FindRecord(State, Kind,
				"taf:removal-complete:" + Slug + ":v1");
			string digest = KingdomRetirementDigestRules.Evidence(
				"removal-preview-" + Slug, Rows);
			return receipt != null
				&& receipt.Disposition == KingdomRemovalDisposition.TerminalIntent
				&& receipt.Amount == Rows.Count && receipt.BeforeDigest == digest
				|| Fail("terminal " + Slug + " projection changed after its frozen preview",
					out Failure);
		}
	}
}
