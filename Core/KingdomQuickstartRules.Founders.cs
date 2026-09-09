using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free authority for the founding cohort. Everything here is a pure function of a
	/// receipt: the runtime may only move the cohort between the states below, and only in the one
	/// direction each move allows.
	/// </summary>
	public static partial class KingdomQuickstartRules
	{
		/// <summary>How many founders a cohort is. Fixed: they arrive together or not at all.</summary>
		public const int FounderCount = 4;

		/// <summary>
		/// Read once, at world creation, and never again. A world that decided it wants no founders
		/// is stamped <see cref="KingdomQuickstartFoundersDisposition.Omitted"/> before its receipt
		/// is first published, so turning this on later cannot retro-seed it and turning it off
		/// later cannot abandon a cohort already half-standing.
		/// </summary>
		public const string FoundersOption = "r_TAF_OptionQuickstartFounders";

		private static readonly int[] FounderCellsX = { 32, 34, 32, 34 };
		private static readonly int[] FounderCellsY = { 11, 11, 13, 13 };

		/// <summary>
		/// Where founder <paramref name="Index"/> stands. Inside the approach band the quickstart
		/// mask already bares (X29-37, Y11-13), clear of the supply column (X27-30), of every
		/// reserved role cell (X28), of the heart rect and of the founder's own start cell.
		/// </summary>
		public static bool TryFounderCell(int Index, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (Index < 0 || Index >= FounderCount) return false;
			X = FounderCellsX[Index];
			Y = FounderCellsY[Index];
			return true;
		}

		/// <summary>
		/// True when no wake may do anything further with this receipt. One predicate, called by
		/// both the bootstrap and the lifecycle, so the two bounds cannot drift apart.
		/// </summary>
		public static bool IsTerminal(KingdomQuickstartReceipt Receipt)
		{
			if (!Valid(Receipt)) return false;
			if (Receipt.Phase == KingdomQuickstartPhase.FoundersSeeded) return true;
			return Receipt.Phase == KingdomQuickstartPhase.Complete
				&& (Receipt.FoundersDisposition == KingdomQuickstartFoundersDisposition.Omitted
					|| Receipt.FoundersDisposition
						== KingdomQuickstartFoundersDisposition.Faulted);
		}

		/// <summary>
		/// The three lawful moves the cohort makes WITHIN <see cref="KingdomQuickstartPhase.Complete"/>,
		/// where an ordinary phase advance cannot reach.
		/// <list type="bullet">
		/// <item>Pending to Pending, ids cleared: a reversible first stage failed and unwound.</item>
		/// <item>Pending to Seeding, naming four: the durable fence before the first irreversible write.</item>
		/// <item>Seeding to Faulted, keeping those four: a named body cannot be found and the
		/// cohort can neither be completed nor reversed.</item>
		/// </list>
		/// <paramref name="Ids"/> is supplied only for the Seeding move; the other two name nobody
		/// new, so passing ids to them is refused rather than ignored.
		/// </summary>
		public static bool TryRestateFounders(KingdomQuickstartReceipt Current,
			KingdomQuickstartFoundersDisposition Disposition, string[] Ids,
			out KingdomQuickstartReceipt Restated)
		{
			Restated = null;
			if (!Valid(Current) || Current.Phase != KingdomQuickstartPhase.Complete) return false;
			bool clear = Current.FoundersDisposition
					== KingdomQuickstartFoundersDisposition.Pending
				&& Disposition == KingdomQuickstartFoundersDisposition.Pending;
			bool name = Current.FoundersDisposition
					== KingdomQuickstartFoundersDisposition.Pending
				&& Disposition == KingdomQuickstartFoundersDisposition.Seeding;
			bool fault = Current.FoundersDisposition
					== KingdomQuickstartFoundersDisposition.Seeding
				&& Disposition == KingdomQuickstartFoundersDisposition.Faulted;
			if (!clear && !name && !fault) return false;
			if (name != (Ids != null)) return false;
			KingdomQuickstartReceipt copy = Current.Copy();
			copy.FoundersDisposition = Disposition;
			if (clear)
				for (int i = 0; i < FounderCount; i++) copy.FounderObjectIds[i] = "";
			if (name)
			{
				if (Ids.Length != FounderCount) return false;
				for (int i = 0; i < FounderCount; i++) copy.FounderObjectIds[i] = Ids[i] ?? "";
			}
			if (!Valid(copy)) return false;
			Restated = copy;
			return true;
		}

		/// <summary>
		/// Stable, checksummed ownership mark for one founder body, indexed so the four cannot be
		/// mistaken for each other. Like <see cref="GrantMarker"/> it excludes receipt phase and
		/// object identity, so the same mark proves a body on both sides of publication.
		/// </summary>
		public static string FounderMarker(KingdomQuickstartReceipt Receipt, int Index)
		{
			if (!Valid(Receipt) || Index < 0 || Index >= FounderCount
				|| Receipt.Phase < KingdomQuickstartPhase.Complete) return null;
			string body = "qg2|" + B64(Receipt.ProfileKey) + "|" + B64(Receipt.ZoneId)
				+ "|" + ((int)KingdomQuickstartPhase.FoundersSeeded).ToString(
					CultureInfo.InvariantCulture)
				+ "|" + Index.ToString(CultureInfo.InvariantCulture)
				+ "|" + B64(Receipt.FoodBlueprint);
			return body + "|" + Digest(body);
		}

		/// <summary>
		/// The founders half of <see cref="Valid"/>. A disposition with no cohort names nobody and
		/// may sit at any phase below the seeded one; a disposition with a cohort names exactly four
		/// distinct identities and may only sit at Complete or above; and Seeded is both the only
		/// disposition the seeded phase admits and the only phase Seeded may sit at.
		/// </summary>
		private static bool FoundersValid(KingdomQuickstartReceipt Receipt)
		{
			string[] ids = Receipt.FounderObjectIds;
			if (ids == null || ids.Length != FounderCount) return false;
			bool cohort;
			switch (Receipt.FoundersDisposition)
			{
			case KingdomQuickstartFoundersDisposition.Omitted:
			case KingdomQuickstartFoundersDisposition.Pending:
				cohort = false;
				break;
			case KingdomQuickstartFoundersDisposition.Seeding:
			case KingdomQuickstartFoundersDisposition.Seeded:
			case KingdomQuickstartFoundersDisposition.Faulted:
				cohort = true;
				break;
			default:
				return false;
			}
			if (!cohort)
			{
				for (int i = 0; i < FounderCount; i++)
					if (!string.IsNullOrEmpty(ids[i])) return false;
				return Receipt.Phase != KingdomQuickstartPhase.FoundersSeeded;
			}
			if (Receipt.Phase < KingdomQuickstartPhase.Complete) return false;
			for (int i = 0; i < FounderCount; i++)
			{
				if (!Identity(ids[i])) return false;
				for (int j = 0; j < i; j++)
					if (string.Equals(ids[i], ids[j], StringComparison.Ordinal)) return false;
			}
			return (Receipt.Phase == KingdomQuickstartPhase.FoundersSeeded)
				== (Receipt.FoundersDisposition
					== KingdomQuickstartFoundersDisposition.Seeded);
		}
	}
}
