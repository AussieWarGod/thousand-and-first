using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		public static bool TryEncode(IList<KingdomConstructionJob> Jobs, out string Text)
		{
			Text = null;
			List<KingdomConstructionJob> rows;
			if (!TryNormalize(Jobs, out rows))
			{
				return false;
			}
			StringBuilder output = new StringBuilder(FormatHeader);
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomConstructionJob row = rows[i];
				KingdomConstructionClaims claim = row.Claims;
				KingdomConstructionOutbox box = row.Outbox;
				output.Append('\n').Append(row.Id).Append('|')
					.Append(EncodeText(row.OwnerKey)).Append('|').Append(EncodeText(row.ZoneId)).Append('|')
					.Append((int)row.Route).Append('|').Append((int)row.Phase).Append('|').Append((int)row.Projection).Append('|')
					.Append(row.X.ToString(CultureInfo.InvariantCulture)).Append('|').Append(row.Y.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(EncodeText(row.SubjectId)).Append('|').Append(EncodeText(row.SourceId)).Append('|')
					.Append(EncodeText(row.OutputId)).Append('|').Append((int)row.PhysicalPhase).Append('|')
					.Append(row.PhysicalIndex.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.PhysicalAmount.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.PhysicalSpilled.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(EncodeText(row.PhysicalItemId)).Append('|').Append(EncodeText(row.PhysicalDestinationId)).Append('|')
					.Append(EncodeText(row.PhysicalReceipt)).Append('|')
					.Append(EncodeText(row.TargetKey)).Append('|').Append(EncodeText(row.Payload)).Append('|')
					.Append(row.CreatedTick.ToString(CultureInfo.InvariantCulture)).Append('|').Append(row.StartedTick.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.DueTick.ToString(CultureInfo.InvariantCulture)).Append('|').Append(row.UpdatedTick.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.Revision.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(claim.WaterRequested.ToString(CultureInfo.InvariantCulture)).Append('|').Append(claim.WaterSpent.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(claim.WaterOutstanding.ToString(CultureInfo.InvariantCulture)).Append('|').Append(claim.WaterLost.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(claim.Exact ? '1' : '0').Append('|')
					.Append(EncodeText(claim.MaterialRequested)).Append('|').Append(EncodeText(claim.MaterialSpent)).Append('|')
					.Append(EncodeText(claim.MaterialOutstanding)).Append('|').Append(EncodeText(claim.MaterialLost)).Append('|')
					.Append(EncodeText(row.Failure)).Append('|')
					.Append(EncodeText(box == null ? null : box.EventId)).Append('|')
					.Append(box == null ? 0 : box.Mode).Append('|')
					.Append(EncodeText(box == null ? null : box.Chronicle)).Append('|')
					.Append(box == null ? 0 : (int)box.ChronicleState).Append('|')
					.Append(EncodeText(box == null ? null : box.Ledger)).Append('|')
					.Append(box == null ? 0 : (int)box.LedgerState).Append('|')
					.Append(EncodeText(box == null ? null : box.Message)).Append('|')
					.Append(box == null ? 0 : (int)box.MessageState).Append('|')
					.Append(EncodeText(box == null ? null : box.Deed)).Append('|')
					.Append(box == null ? 0 : (int)box.DeedState).Append('|')
					.Append(box == null ? -1 : box.LedgerBeforeCount).Append('|')
					.Append(EncodeText(box == null ? null : box.LedgerBeforeHash)).Append('|')
					.Append(box == null ? -1 : box.LedgerAfterCount).Append('|')
					.Append(EncodeText(box == null ? null : box.LedgerAfterHash)).Append('|')
					.Append(row.Compacted ? '1' : '0').Append('|')
					.Append(EncodeText(row.CompactHash)).Append('|')
					.Append(row.BuildTruthSchema).Append('|')
					.Append(row.BuildHasPlot ? '1' : '0').Append('|')
					.Append(row.BuildFrontier ? '1' : '0').Append('|')
					.Append(row.BuildDefence.ToString(CultureInfo.InvariantCulture));
				if (output.Length > MaxRegistryChars)
				{
					return false;
				}
			}
			Text = output.ToString();
			return true;
		}

		public static bool TryDecode(string Text, out List<KingdomConstructionJob> Jobs)
		{
			Jobs = null;
			if (Text == null || Text.Length > MaxRegistryChars)
			{
				return false;
			}
			string[] lines = Text.Split('\n');
			bool legacy = lines.Length > 0 && lines[0] == LegacyFormatHeader;
			bool older = lines.Length > 0 && lines[0] == OlderFormatHeader;
			bool prior = lines.Length > 0 && lines[0] == PriorFormatHeader;
			if (lines.Length == 0 || (!legacy && !older && !prior && lines[0] != FormatHeader)
				|| lines.Length - 1 > MaxRows)
			{
				return false;
			}
			List<KingdomConstructionJob> rows = new List<KingdomConstructionJob>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 1; i < lines.Length; i++)
			{
				if (lines[i].Length == 0)
				{
					return false;
				}
				KingdomConstructionJob row;
				if (!TryDecodeRow(lines[i], legacy, older, prior, out row) || !ids.Add(row.Id))
				{
					return false;
				}
				rows.Add(row);
			}
			List<KingdomConstructionJob> normalized;
			if (!TryNormalize(rows, out normalized))
			{
				return false;
			}
			Jobs = normalized;
			return true;
		}

		/// <summary>Canonical sort plus lossless terminal compaction. Never drops replay IDs.</summary>
		public static bool TryNormalize(IList<KingdomConstructionJob> Jobs,
			out List<KingdomConstructionJob> Normalized)
		{
			Normalized = null;
			if (Jobs == null)
			{
				return false;
			}
			List<KingdomConstructionJob> active = new List<KingdomConstructionJob>();
			List<KingdomConstructionJob> terminal = new List<KingdomConstructionJob>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Jobs.Count; i++)
			{
				KingdomConstructionJob row = Jobs[i];
				if (!ValidJob(row) || !ids.Add(row.Id))
				{
					return false;
				}
				KingdomConstructionJob copy = row.Copy();
				// Missing/unsettled telling remains active so dispatcher can reconstruct/retry it.
				if (copy.Compacted)
				{
					terminal.Add(copy);
				}
				else if (TerminalClosureSettled(copy)
					&& copy.PhysicalPhase != KingdomPhysicalPhase.TellingsPending)
				{
					terminal.Add(Compact(copy));
				}
				else
				{
					active.Add(copy);
				}
			}
			if (active.Count > MaxActiveRows || Jobs.Count > MaxRows)
			{
				return false;
			}
			active.AddRange(terminal);
			active.Sort(CompareCanonical);
			Normalized = active;
			return true;
		}

	}
}
