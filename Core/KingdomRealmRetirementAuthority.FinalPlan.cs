using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		private static bool TryBuildFinalPlan(KingdomSystem System,
			KingdomRealmRetirementState State, out KingdomRealmRemovalFinalPlan Plan,
			out string Failure)
		{
			Plan = null; Failure = null;
			if (System == null || State == null
				|| (State.Phase != KingdomRealmRetirementPhase.Planning
					&& State.Phase != KingdomRealmRetirementPhase.CleaningGround)
				|| !KingdomRealmRetirementRules.Valid(State, out Failure)) return false;
			for (int i = 0; State.Phase == KingdomRealmRetirementPhase.CleaningGround
				&& i < State.Locators.Count; i++)
				if (State.Locators[i].State != KingdomRemovalLocatorState.Cleaned)
					return Fail("tracked ground still requires an attended cleanup", out Failure);
			KingdomRealmRemovalFinalPlan plan = new KingdomRealmRemovalFinalPlan();
			for (int i = 0; i < State.Locators.Count; i++)
				plan.LocatorZoneIds.Add(State.Locators[i].ZoneId);
			if (!KingdomRaids.TryInspectRecoveryQuests(System, out List<string> quests,
				out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectCooking(out List<string> recipes,
					out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectJournal(out List<string> journal,
					out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectFactions(System, State.Locators,
					out plan.Factions,
					out List<string> factions, out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectGlobalStates(System, plan,
					out List<string> globals, out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectSystems(System,
					out List<IGameSystem> systems, out List<string> systemRows, out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectPlayer(out List<string> player,
					out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectRoster(out List<string> roster,
					out Failure)
				|| !KingdomRemovalProjectionRuntime.TryInspectCivicRetirementProjection(System,
					State.StartedTick, out List<string> civic,
					out List<string> projectedCivic, out int _, out Failure)
				|| !KingdomRemovalProjectionRuntime.TryValidateWitnessRetirementLocators(System,
					State.Locators, out Failure)) return false;
			if (State.Phase == KingdomRealmRetirementPhase.Planning && quests.Count != 0)
				return Fail("active recovery quests must be resolved before retirement planning",
					out Failure);
			plan.QuestCount = quests.Count; plan.RecipeCount = recipes.Count;
			plan.JournalCount = journal.Count; plan.AbilityCount = player.Count;
			plan.SystemCount = systems.Count;
			plan.Systems.AddRange(systems);
			plan.PlayerRows.AddRange(player);
			if (!TryAddProjectionPair(State, plan, "quests", KingdomRemovalProjectionKind.Quest,
				quests, KingdomRemovalDisposition.Retired,
				"active TAF recovery quests were retired directly without completion or rewards",
				out Failure)
				|| !TryAddProjectionPair(State, plan, "recipes",
					KingdomRemovalProjectionKind.GlobalState, recipes,
					KingdomRemovalDisposition.Converted,
					"learned realm recipes were converted to native CookingRecipe values", out Failure)
				|| !TryAddProjectionPair(State, plan, "journal",
					KingdomRemovalProjectionKind.JournalHistory, journal,
					KingdomRemovalDisposition.Converted,
					"founder history was retained as native Sultan journal notes", out Failure)
				|| !TryAddProjectionPair(State, plan, "factions",
					KingdomRemovalProjectionKind.Faction, factions,
					KingdomRemovalDisposition.Retired,
					"realm factions were retained with relations and reputation but made inert", out Failure)
				|| !TryAddProjectionPair(State, plan, "systems",
					KingdomRemovalProjectionKind.GlobalState, systemRows,
					KingdomRemovalDisposition.TerminalIntent,
					"exact auxiliary system cuts are authorized only after the base fence", out Failure)
				|| !TryAddProjectionPair(State, plan, "global-state",
					KingdomRemovalProjectionKind.GlobalState, globals,
					KingdomRemovalDisposition.TerminalIntent,
					"only empty or exact current-realm global authorities may be cut after the base fence",
					out Failure)
				|| !TryAddProjectionPair(State, plan, "roster",
					KingdomRemovalProjectionKind.GlobalState, roster,
					KingdomRemovalDisposition.TerminalIntent,
					"the exact save-system roster marker cut is authorized only after the base fence",
					out Failure)
				|| !TryAddProjectionPair(State, plan, "civic-semantics",
					KingdomRemovalProjectionKind.JournalHistory, civic,
					projectedCivic,
					KingdomRemovalDisposition.Converted,
					"office, remembrance, civic-voice, and first-feast meaning was retained as native readable notes",
					out Failure)
					|| !TryAddProjectionPair(State, plan, "player",
						KingdomRemovalProjectionKind.Ability, player,
						KingdomRemovalDisposition.TerminalIntent,
						"exact Charter command and part cut is authorized for the final synchronous step",
						out Failure)) return false;
			List<string> closureRows = new List<string>();
			for (int i = 0; i < plan.CompletionRecords.Count; i++)
				closureRows.Add(plan.CompletionRecords[i].AfterDigest);
			plan.CompletionRecords.Add(new KingdomRemovalRecord
			{
				Kind = KingdomRemovalProjectionKind.Authority,
				Id = KingdomRealmRetirementRules.AuthorityRecordId,
				Disposition = KingdomRemovalDisposition.Closed,
				BeforeDigest = State.AuthorityDigest,
				AfterDigest = KingdomRetirementDigestRules.Evidence(
					"realm-authority-closed-v1", closureRows),
				Amount = plan.CompletionRecords.Count,
				Detail = "all known work authority is closed; KingdomSystem remains only as the receipt carrier until fence commit"
			});
			if (!TryPreviewRecordCapacity(State, plan, out Failure)) return false;
			Plan = plan; return true;
		}

		private static bool TryAddProjectionPair(KingdomRealmRetirementState State,
			KingdomRealmRemovalFinalPlan Plan, string Slug, KingdomRemovalProjectionKind Kind,
			List<string> Rows, KingdomRemovalDisposition CompletionDisposition,
			string CompletionDetail, out string Failure)
		{
			return TryAddProjectionPair(State, Plan, Slug, Kind, Rows, null,
				CompletionDisposition, CompletionDetail, out Failure);
		}

		private static bool TryAddProjectionPair(KingdomRealmRetirementState State,
			KingdomRealmRemovalFinalPlan Plan, string Slug, KingdomRemovalProjectionKind Kind,
			List<string> Rows, List<string> ProjectedRows,
			KingdomRemovalDisposition CompletionDisposition,
			string CompletionDetail, out string Failure)
		{
			Failure = null;
			string previewId = "taf:removal-preview:" + Slug + ":v1";
			string completionId = "taf:removal-complete:" + Slug + ":v1";
			string liveDigest = KingdomRetirementDigestRules.Evidence(
				"removal-preview-" + Slug, Rows);
			string projectedDigest = ProjectedRows == null ? null
				: KingdomRetirementDigestRules.Evidence("removal-preview-" + Slug,
					ProjectedRows);
			KingdomRemovalRecord preview = FindRecord(State, Kind, previewId);
			KingdomRemovalRecord priorCompletion = FindRecord(State, Kind, completionId);
			if (preview == null)
				preview = new KingdomRemovalRecord
				{
					Kind = Kind, Id = previewId,
					Disposition = KingdomRemovalDisposition.Preserved,
					BeforeDigest = liveDigest, AfterDigest = projectedDigest, Amount = Rows.Count,
					Detail = "exact destructive preview was committed before " + Slug + " cleanup"
				};
			else
			{
				bool retrying = CallbackCutFamily(Slug) && priorCompletion == null
					&& FindRecord(State, Kind, CallbackAttemptPrefix + Slug + ":v1") != null;
				if (preview.Disposition != KingdomRemovalDisposition.Preserved
					|| !KingdomRealmRetirementRules.Digest(preview.BeforeDigest)
					|| ProjectedRows != null && (preview.AfterDigest != projectedDigest
						|| !KingdomRealmRetirementRules.Digest(preview.AfterDigest)))
					return Fail("live " + Slug + " projection diverged from its frozen preview",
						out Failure);
				if (retrying)
				{
					if (!TryFrozenCutRows(State, Slug, Kind, false, out List<string> frozen,
						out Failure) || KingdomRealmRemovalRetryRules.CutProgress(frozen, Rows, true)
						== KingdomRemovalCutProgress.Quarantine)
						return Fail(Failure ?? "callback remainder is outside its frozen family",
							out Failure);
				}
				else if (priorCompletion == null || CompletionDisposition
					== KingdomRemovalDisposition.TerminalIntent)
				{
					if (preview.BeforeDigest != liveDigest || preview.Amount != Rows.Count)
						return Fail("live " + Slug + " projection diverged from its frozen preview",
							out Failure);
				}
				else if (ProjectedRows == null && Rows.Count != 0)
					return Fail("completed " + Slug + " projection reappeared", out Failure);
			}
			KingdomRemovalRecord completion = new KingdomRemovalRecord
			{
				Kind = Kind, Id = completionId, Disposition = CompletionDisposition,
				BeforeDigest = preview.BeforeDigest,
				AfterDigest = KingdomRetirementDigestRules.Evidence(
					"removal-complete-" + Slug,
					new List<string> { preview.BeforeDigest,
						preview.Amount.ToString(CultureInfo.InvariantCulture) }),
				Amount = preview.Amount, Detail = CompletionDetail
			};
			if (priorCompletion != null && (priorCompletion.Disposition != completion.Disposition
				|| priorCompletion.BeforeDigest != completion.BeforeDigest
				|| priorCompletion.AfterDigest != completion.AfterDigest
				|| priorCompletion.Amount != completion.Amount
				|| priorCompletion.Detail != completion.Detail))
				return Fail("completed " + Slug + " receipt differs from its frozen aggregate",
					out Failure);
			Plan.PreviewRecords.Add(preview);
			if (!AddCutAuthorityRecords(State, Plan, Slug, Kind, Rows, preview,
				out Failure)) return false;
			Plan.CompletionRecords.Add(completion);
			return true;
		}

		private static bool TryPreviewRecordCapacity(KingdomRealmRetirementState State,
			KingdomRealmRemovalFinalPlan Plan, out string Failure)
		{
			Failure = null; KingdomRealmRetirementState preview = State;
			for (int pass = 0; pass < 2; pass++)
			{
				List<KingdomRemovalRecord> rows = pass == 0
					? Plan.PreviewRecords : Plan.CompletionRecords;
				for (int i = 0; i < rows.Count; i++)
				{
					if (!KingdomRealmRetirementRules.TryRecord(preview, preview.Revision,
						rows[i], preview.UpdatedTick, out KingdomRealmRetirementState next,
						out Failure)) return false;
					preview = next;
				}
			}
			if (State.Phase == KingdomRealmRetirementPhase.Planning)
				for (int i = 0; i < State.Locators.Count; i++)
					for (int row = 0; row < 4; row++)
					{
						KingdomRemovalRecord reserve = new KingdomRemovalRecord
						{
							Kind = KingdomRemovalProjectionKind.Object,
							Id = "taf:ground-reserve:" + i + ":" + row,
							Disposition = row == 0 ? KingdomRemovalDisposition.Preserved
								: KingdomRemovalDisposition.Converted,
							BeforeDigest = State.AuthorityDigest,
							AfterDigest = State.AuthorityDigest,
							Detail = "bounded aggregate ground receipt reserve"
						};
						if (!KingdomRealmRetirementRules.TryRecord(preview, preview.Revision,
							reserve, preview.UpdatedTick, out KingdomRealmRetirementState next,
							out Failure)) return false;
						preview = next;
					}
			if (!KingdomRealmRemovalRetryRules.FenceCapacityReserved(preview.Records.Count))
				return Fail("retirement record capacity does not reserve the mandatory fence row",
					out Failure);
			return true;
		}

		private static KingdomRemovalRecord FindRecord(KingdomRealmRetirementState State,
			KingdomRemovalProjectionKind Kind, string Id)
		{
			for (int i = 0; i < (State?.Records?.Count ?? 0); i++)
				if (State.Records[i].Kind == Kind && State.Records[i].Id == Id)
					return State.Records[i].Clone();
			return null;
		}
	}
}
