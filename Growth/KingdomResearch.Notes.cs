using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomResearch
	{
		// ==================================================================================
		// Discovery — the founder's ledger, in vanilla's own book
		// ==================================================================================

		/// <summary>The journal id one node's discovery bit lives under.</summary>
		public static string NoteId(string Key)
		{
			return string.IsNullOrEmpty(Key) ? null : (NotePrefix + Key.Trim().ToLowerInvariant());
		}

		/// <summary>
		/// Files one unrevealed journal note per node, once per game. Vanilla refuses an id it
		/// already holds, so this is idempotent whatever calls it; the flag only keeps it from
		/// walking the registry on every read.
		/// </summary>
		public static void FileNotes()
		{
			if (NotesFiled || !Enabled)
			{
				return;
			}
			EnsureLoaded();
			NotesFiled = true;
			for (int i = 0; i < _nodes.Count; i++)
			{
				ResearchNode node = _nodes[i];
				string id = NoteId(node.Key);
				if (id == null || JournalAPI.GetObservation(id) != null)
				{
					continue;
				}
				JournalAPI.AddObservation(KingdomResearchRules.LeadText(node.Named, node.Branch), id, NoteCategory, id, null,
					revealed: false, -1L);
			}
		}

		/// <summary>Whether the founder has heard of this node at all. An O(1) lookup in vanilla's
		/// own note map, deliberately not the scan beside it.</summary>
		public static bool Discovered(string Key)
		{
			if (!Enabled)
			{
				return false;
			}
			FileNotes();
			string id = NoteId(Key);
			return id != null && JournalAPI.HasNote(id);
		}

		/// <summary>
		/// Tells the founder a node exists, and where they heard it. Vanilla stamps the provenance
		/// on the entry itself, so the chronicle line writes itself and the note is sellable at a
		/// water ritual like every other thing they have learned about the world.
		/// </summary>
		/// <param name="Key">The node.</param>
		/// <param name="LearnedFrom">Who said so, in the founder's words. May be null.</param>
		/// <returns>True when this call is what revealed it.</returns>
		public static bool Reveal(string Key, string LearnedFrom)
		{
			if (!Enabled || Discovered(Key))
			{
				return false;
			}
			string id = NoteId(Key);
			if (id == null || !JournalAPI.TryRevealNote(id,
				string.IsNullOrEmpty(LearnedFrom) ? LearnedFrom
					: KingdomPresentation.Rich(LearnedFrom)))
			{
				return false;
			}
			KingdomLog.Log("research: revealed " + Key + ((LearnedFrom == null) ? "" : (" (" + LearnedFrom + ")")));
			return true;
		}

	}
}
