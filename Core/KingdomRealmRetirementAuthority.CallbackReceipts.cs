using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		private const string CallbackAttemptPrefix = "taf:removal-attempt:";
		private const string CallbackRowPrefix = "taf:removal-row:";

		private static bool CallbackCutFamily(string Slug)
		{
			return Slug == "quests" || Slug == "recipes" || Slug == "journal"
				|| Slug == "civic-semantics" || Slug == "factions";
		}

		private static bool SubsetCutFamily(string Slug)
		{
			return CallbackCutFamily(Slug) || Slug == "systems" || Slug == "global-state";
		}

		private static bool AddCutAuthorityRecords(KingdomRealmRetirementState State,
			KingdomRealmRemovalFinalPlan Plan,
			string Slug, KingdomRemovalProjectionKind Kind, IList<string> Rows,
			KingdomRemovalRecord Preview, out string Failure)
		{
			Failure = null;
			if (!SubsetCutFamily(Slug)) return true;
			IList<string> authorityRows = Rows;
			bool terminal = !CallbackCutFamily(Slug);
			string attemptId = terminal ? "taf:removal-complete:" + Slug + ":v1"
				: CallbackAttemptPrefix + Slug + ":v1";
			if (FindRecord(State, Kind, attemptId) != null)
			{
				if (!TryFrozenCutRows(State, Slug, Kind, terminal,
					out List<string> frozen, out Failure)) return false;
				authorityRows = frozen;
			}
			if (Plan == null || Preview == null || authorityRows == null
				|| !KingdomRealmRemovalRetryRules.ExactRemainingSubset(authorityRows, authorityRows))
				return Fail("terminal callback family has duplicate or missing row authority", out Failure);
			for (int i = 0; i < authorityRows.Count; i++)
			{
				string row = authorityRows[i];
				if (row == null) return Fail("terminal callback row is null", out Failure);
				string digest = KingdomRetirementDigestRules.Evidence(
					"removal-row-" + Slug, new List<string> { row });
				int chunks = Math.Max(1, ((row?.Length ?? 0) + 899) / 900);
				for (int chunk = 0; chunk < chunks; chunk++)
				{
					int start = chunk * 900;
					string detail = string.IsNullOrEmpty(row) ? ""
						: row.Substring(start, Math.Min(900, row.Length - start));
					Plan.PreviewRecords.Add(new KingdomRemovalRecord
					{
						Kind = KingdomRemovalProjectionKind.Authority,
						Id = CallbackRowPrefix + Slug + ":" + i.ToString("D6") + ":"
							+ chunk.ToString("D4") + ":" + digest,
						Disposition = KingdomRemovalDisposition.Preserved,
						BeforeDigest = digest, AfterDigest = Preview.BeforeDigest,
						Amount = chunks, Detail = detail
					});
				}
			}
			if (CallbackCutFamily(Slug))
				Plan.PreviewRecords.Add(new KingdomRemovalRecord
				{
					Kind = Kind, Id = CallbackAttemptPrefix + Slug + ":v1",
					Disposition = KingdomRemovalDisposition.TerminalIntent,
					BeforeDigest = Preview.BeforeDigest,
					AfterDigest = KingdomRetirementDigestRules.Evidence("removal-attempt-" + Slug,
						new List<string> { Preview.BeforeDigest,
							Preview.Amount.ToString(CultureInfo.InvariantCulture) }),
					Amount = Preview.Amount,
					Detail = "authenticated callback attempt; exact frozen remainders may resume"
				});
			return true;
		}

		private static bool TryFrozenCutRows(KingdomRealmRetirementState State, string Slug,
			KingdomRemovalProjectionKind Kind, bool Terminal, out List<string> Frozen,
			out string Failure)
		{
			Frozen = null; Failure = null;
			KingdomRemovalRecord preview = FindRecord(State, Kind,
				"taf:removal-preview:" + Slug + ":v1");
			KingdomRemovalRecord attempt = FindRecord(State, Kind, Terminal
				? "taf:removal-complete:" + Slug + ":v1"
				: CallbackAttemptPrefix + Slug + ":v1");
			if (preview == null || attempt == null
				|| preview.Disposition != KingdomRemovalDisposition.Preserved
				|| attempt.Disposition != KingdomRemovalDisposition.TerminalIntent
				|| attempt.BeforeDigest != preview.BeforeDigest || attempt.Amount != preview.Amount
				|| (Terminal && attempt.AfterDigest != KingdomRetirementDigestRules.Evidence(
					"removal-complete-" + Slug, new List<string> { preview.BeforeDigest,
						preview.Amount.ToString(CultureInfo.InvariantCulture) }))
				|| (!Terminal && (attempt.Id != CallbackAttemptPrefix + Slug + ":v1"
					|| attempt.AfterDigest != KingdomRetirementDigestRules.Evidence(
						"removal-attempt-" + Slug, new List<string> { preview.BeforeDigest,
							preview.Amount.ToString(CultureInfo.InvariantCulture) }))))
				return Fail("terminal callback attempt lacks its frozen family binding", out Failure);
			string prefix = CallbackRowPrefix + Slug + ":";
			SortedDictionary<int, SortedDictionary<int, KingdomRemovalRecord>> rows =
				new SortedDictionary<int, SortedDictionary<int, KingdomRemovalRecord>>();
			for (int i = 0; i < State.Records.Count; i++)
			{
				KingdomRemovalRecord record = State.Records[i];
				if (record?.Kind != KingdomRemovalProjectionKind.Authority
					|| !record.Id.StartsWith(prefix, StringComparison.Ordinal)) continue;
				string[] p = record.Id.Substring(prefix.Length).Split(':');
				if (p.Length != 3 || p[0].Length != 6 || p[1].Length != 4
					|| !int.TryParse(p[0], out int ordinal) || !int.TryParse(p[1], out int chunk)
					|| ordinal < 0 || ordinal >= preview.Amount || chunk < 0
					|| chunk >= record.Amount)
					return Fail("terminal callback row ordinal is malformed", out Failure);
				if (record.Disposition != KingdomRemovalDisposition.Preserved || record.Amount <= 0L
					|| record.Amount > KingdomRealmRetirementState.MaxRecords
					|| record.BeforeDigest != p[2] || record.AfterDigest != preview.BeforeDigest
					|| !KingdomRealmRetirementRules.Detail(record.Detail)
					|| record.Id != prefix + ordinal.ToString("D6") + ":"
						+ chunk.ToString("D4") + ":" + record.BeforeDigest)
					return Fail("terminal callback row receipt is forged", out Failure);
				if (!rows.TryGetValue(ordinal,
					out SortedDictionary<int, KingdomRemovalRecord> chunks))
					rows[ordinal] = chunks = new SortedDictionary<int, KingdomRemovalRecord>();
				if (chunks.ContainsKey(chunk))
					return Fail("terminal callback row chunk is duplicated", out Failure);
				chunks[chunk] = record;
			}
			if (rows.Count != preview.Amount)
				return Fail("terminal callback row receipt set is incomplete", out Failure);
			Frozen = new List<string>();
			for (int i = 0; i < rows.Count; i++)
				if (!rows.TryGetValue(i, out SortedDictionary<int, KingdomRemovalRecord> chunks)
					|| chunks.Count == 0)
					return Fail("terminal callback row receipt sequence has a gap", out Failure);
				else
				{
					long expectedChunks = -1L;
					int expectedIndex = 0;
					StringBuilder row = new StringBuilder();
					string digest = null;
					foreach (KeyValuePair<int, KingdomRemovalRecord> chunk in chunks)
					{
						if (chunk.Key != expectedIndex++
							|| expectedChunks < 0L && (expectedChunks = chunk.Value.Amount) <= 0L
							|| chunk.Value.Amount != expectedChunks || digest != null
								&& digest != chunk.Value.BeforeDigest)
							return Fail("terminal callback row chunks diverge", out Failure);
						digest = chunk.Value.BeforeDigest; row.Append(chunk.Value.Detail);
					}
					if (chunks.Count != expectedChunks || digest != KingdomRetirementDigestRules.Evidence(
						"removal-row-" + Slug, new List<string> { row.ToString() }))
						return Fail("terminal callback row chunks fail their digest", out Failure);
					Frozen.Add(row.ToString());
				}
			return KingdomRetirementDigestRules.Evidence("removal-preview-" + Slug, Frozen)
				== preview.BeforeDigest || Fail("terminal callback row family digest diverged", out Failure);
		}

		private static bool CallbackFamilySettled(KingdomSystem System,
			KingdomRealmRetirementState State, string Slug, out string Failure)
		{
			Failure = null;
			KingdomRemovalProjectionKind kind = Slug == "factions"
				? KingdomRemovalProjectionKind.Faction : Slug == "quests"
					? KingdomRemovalProjectionKind.Quest : Slug == "journal"
						|| Slug == "civic-semantics"
							? KingdomRemovalProjectionKind.JournalHistory
							: KingdomRemovalProjectionKind.GlobalState;
			if (!TryFrozenCutRows(State, Slug, kind, false, out List<string> frozen,
				out Failure)) return false;
			List<string> current;
			if (Slug == "quests")
			{
				if (!KingdomRaids.TryInspectRecoveryQuests(System, out current, out Failure)) return false;
			}
			else if (Slug == "recipes")
			{
				if (!KingdomRemovalProjectionRuntime.TryInspectCooking(out current, out Failure)) return false;
			}
			else if (Slug == "journal")
			{
				if (!KingdomRemovalProjectionRuntime.TryInspectJournal(out current, out Failure)) return false;
			}
			else if (Slug == "factions")
			{
				if (!KingdomRemovalProjectionRuntime.TryInspectFactions(System, State.Locators,
					out List<Faction> _, out current, out Failure)) return false;
			}
			else if (Slug == "civic-semantics")
			{
				if (!KingdomRemovalProjectionRuntime.TryInspectCivicRetirementProjection(System,
					State.StartedTick, out current, out List<string> projected,
					out int pending, out Failure)) return false;
				KingdomRemovalRecord preview = FindRecord(State, kind,
					"taf:removal-preview:civic-semantics:v1");
				return pending == 0 && preview != null
					&& KingdomRealmRemovalRetryRules.ExactRemainingSubset(current, projected)
					&& KingdomRealmRemovalRetryRules.ExactRemainingSubset(projected, current)
					&& KingdomRetirementDigestRules.Evidence(
						"removal-preview-civic-semantics", current) == preview.AfterDigest
						|| Fail("civic callback did not reach its frozen projection", out Failure);
			}
			else return Fail("unknown callback family", out Failure);
			return KingdomRealmRemovalRetryRules.CutProgress(frozen, current, true)
				== KingdomRemovalCutProgress.Settled
				|| Fail("callback family did not reach authenticated absence", out Failure);
		}
	}
}
