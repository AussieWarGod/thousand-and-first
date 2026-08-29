using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// The whole archive, read a page at a time, for anything that needs every covenant rather than
	/// a bounded summary of them.
	/// <para>
	/// The joint civic view answers one question about the realm and answers it in one bounded
	/// report, so it names a few covenants and counts the rest. That is the right shape for a
	/// summary and the wrong shape for a reader that wants all of them; widening the summary until
	/// forty-eight rows fit would give every other owner in that view a ceiling raised for reasons
	/// that have nothing to do with them. So the register is a separate seam with its own bound,
	/// and it hands back copies: a page a caller could edit would be a page that could edit the
	/// archive.
	/// </para>
	/// </summary>
	public sealed class KingdomVillageCovenantRegister
	{
		/// <summary>Rows per page. Small on purpose &mdash; a caller that wants more asks again.</summary>
		public const int PageRows = 8;

		/// <summary>The realm every row on this page belongs to.</summary>
		public string RealmId { get; private set; }

		/// <summary>How many covenants the archive holds in total, not on this page.</summary>
		public int Total { get; private set; }

		/// <summary>Where this page starts in the archive's canonical order.</summary>
		public int Offset { get; private set; }

		/// <summary>Where the next page starts, or the total when this was the last one.</summary>
		public int NextOffset { get; private set; }

		private readonly List<KingdomVillageCovenantReceipt> Held =
			new List<KingdomVillageCovenantReceipt>();

		public int Count { get { return Held.Count; } }

		/// <summary>One row of this page. The caller owns what it is handed.</summary>
		public KingdomVillageCovenantReceipt Row(int Index)
		{
			if (Index < 0 || Index >= Held.Count)
				throw new ArgumentOutOfRangeException("Index",
					"the covenant register was asked for row " + Index + " of " + Held.Count);
			return Held[Index].Copy();
		}

		/// <summary>Every row of this page, copied.</summary>
		public List<KingdomVillageCovenantReceipt> Rows()
		{
			List<KingdomVillageCovenantReceipt> copy =
				new List<KingdomVillageCovenantReceipt>(Held.Count);
			for (int i = 0; i < Held.Count; i++) copy.Add(Held[i].Copy());
			return copy;
		}

		/// <summary>
		/// Reads one page of an archive, refusing everything the summary refuses.
		/// <para>
		/// A future, quarantined, unbound or foreign archive yields no page at all. This seam is a
		/// wider door onto the same evidence, not a lower one, and an archive nobody may read here
		/// is not an archive somebody may read by asking a page at a time.
		/// </para>
		/// </summary>
		public static bool TryPage(KingdomVillageCovenantArchive archive, string exactRealmId,
			int offset, out KingdomVillageCovenantRegister register, out string failure)
		{
			register = null;
			if (!KingdomVillageCovenantRules.TryValidate(archive, out failure)) return false;
			if (archive.State != KingdomVillageCovenantState.Compatible)
				return KingdomVillageCovenantRules.Fail("the covenant archive is " + archive.State
					+ " and no page of it can be read", out failure);
			if (!KingdomIdentityRules.IsRealmId(exactRealmId))
				return KingdomVillageCovenantRules.Fail("the covenant register was asked for a "
					+ "realm whose id is not canonical", out failure);
			if (!archive.IdentityBound || !string.Equals(archive.RealmId, exactRealmId,
				StringComparison.Ordinal))
				return KingdomVillageCovenantRules.Fail("the covenant archive is not bound to this "
					+ "exact realm", out failure);
			if (offset < 0 || (offset > 0 && offset >= archive.Rows.Count))
				return KingdomVillageCovenantRules.Fail("the covenant register was asked for row "
					+ offset + " of " + archive.Rows.Count, out failure);

			KingdomVillageCovenantRegister page = new KingdomVillageCovenantRegister
			{
				RealmId = archive.RealmId,
				Total = archive.Rows.Count,
				Offset = offset
			};
			int end = offset + PageRows;
			if (end > archive.Rows.Count) end = archive.Rows.Count;
			for (int i = offset; i < end; i++) page.Held.Add(archive.Rows[i].Copy());
			page.NextOffset = end;
			register = page;
			failure = "";
			return true;
		}
	}
}
