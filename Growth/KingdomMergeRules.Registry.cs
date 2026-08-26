using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomMergeRules
	{
		// --- The load-order table -----------------------------------------------------------

		private static readonly Dictionary<string, BuildingDraft> _drafts = new Dictionary<string, BuildingDraft>();

		private static readonly List<CatalogueFinding> _findings = new List<CatalogueFinding>();

		/// <summary>Everything the merges had to say, in the order the elements were read. Reported
		/// with the rest of the catalogue's findings so one load produces one log.</summary>
		public static List<CatalogueFinding> Findings => _findings;

		/// <summary>Forgets every draft and every finding. Called by the loader before it re-reads
		/// the XML streams, beside <c>KingdomZoning.ClearGates</c>.</summary>
		public static void ClearDrafts()
		{
			_drafts.Clear();
			_findings.Clear();
		}

		/// <summary>
		/// Folds one element's draft into whatever earlier files declared under the same key, keeps
		/// the result as the design of record, and hands it back for parsing and registration.
		/// <para>
		/// One pass, one read: everything the registries need comes out of the returned draft, so
		/// no stream is ever read twice and no attribute goes unasked-for.
		/// </para>
		/// </summary>
		public static BuildingDraft Absorb(BuildingDraft Later)
		{
			if (Later == null || string.IsNullOrEmpty(Later.Key))
			{
				return Later;
			}
			BuildingDraft standing;
			_drafts.TryGetValue(Later.Key, out standing);
			BuildingDraft merged = Merge(standing, Later, _findings);
			_drafts[Later.Key] = merged;
			return merged;
		}

		/// <summary>The design of record for a key: every file that named it, folded.</summary>
		public static bool TryGetDraft(string Key, out BuildingDraft Draft)
		{
			Draft = null;
			return !string.IsNullOrEmpty(Key) && _drafts.TryGetValue(Key, out Draft) && Draft != null;
		}

		/// <summary>How many declarations a key's design is the merge of. Zero for a key no file
		/// named.</summary>
		public static int DeclarationsOf(string Key)
		{
			BuildingDraft draft;
			return TryGetDraft(Key, out draft) ? draft.Declarations : 0;
		}

	}
}
