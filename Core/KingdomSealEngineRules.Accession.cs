using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealEngineRules
	{
		internal static bool TryValidateAccessionTokens(int Generation, string Last,
			string Pending, out string Failure)
		{
			Failure = "";
			string last = Last ?? "";
			string pending = Pending ?? "";
			if (Generation < 0 || Generation > 1024 || last == pending && last.Length > 0)
			{
				Failure = "the accession token tuple is contradictory";
				return false;
			}
			if (Generation == 0)
			{
				if (last.Length == 0 && pending.Length == 0) return true;
				Failure = "the founder generation cannot carry an accession token";
				return false;
			}
			if (pending.Length == 0)
			{
				if (AccessionTokenIsOrdinal(last, Generation)) return true;
				Failure = "the completed accession token does not name the current generation";
				return false;
			}
			if (!AccessionTokenIsOrdinal(pending, Generation)
				|| (Generation == 1 ? last.Length != 0
					: !AccessionTokenIsOrdinal(last, Generation - 1)))
			{
				Failure = "the pending accession token does not name the adjacent generation";
				return false;
			}
			return true;
		}

		internal static bool AccessionTokenIsOrdinal(string Token, int Ordinal)
		{
			int parsed;
			long tick;
			return Ordinal > 0 && KingdomSuccessionRules.TryReadDeathToken(Token,
				out parsed, out tick) && parsed == Ordinal;
		}

		/// <summary>Whether a successful accession can hand the store from one generation to the
		/// next. A terminal attempt means succession already ruled the line ended and cannot become
		/// a successor. Retirement may advance because continued play does not rewrite its sealed
		/// legacy; it starts a new generation instead.</summary>
		internal static bool MayAdvanceGeneration(KingdomSealRecord Previous,
			KingdomSealRecord Successor)
		{
			if (Previous == null || Successor == null
				|| (Previous.Status != KingdomSealStatus.Living
					&& Previous.Status != KingdomSealStatus.Retired)
				|| Successor.Status != KingdomSealStatus.Living || Successor.IsResolved
				|| Previous.LineageId != Successor.LineageId
				|| Previous.OriginGameId != Successor.OriginGameId
				|| Previous.LegacyId == Successor.LegacyId
				|| !KingdomSealReceipt.ValidId(Successor.LegacyId)
				|| Previous.Generation < 0 || Previous.Generation >= 1024
				|| Successor.Generation != Previous.Generation + 1
				|| Previous.Revision < 0 || Previous.Revision == int.MaxValue
				|| Successor.Revision != Previous.Revision + 1)
			{
				return false;
			}
			return true;
		}

		/// <summary>A loaded primary may replace only its own living/attempt journal, or a
		/// strictly newer abandoned living/attempt generation of the same lineage and origin.
		/// Retirement is an explicit immutable action and is never rolled back here.</summary>
		internal static bool MayRestoreLoadedPrimary(KingdomSealRecord External,
			KingdomSealRecord SavedLiving)
		{
			if (External == null || SavedLiving == null
				|| (External.Status != KingdomSealStatus.Living
					&& External.Status != KingdomSealStatus.Terminal)
				|| SavedLiving.Status != KingdomSealStatus.Living
				|| External.LineageId != SavedLiving.LineageId
				|| External.OriginGameId != SavedLiving.OriginGameId)
			{
				return false;
			}
			if (External.Generation == SavedLiving.Generation)
			{
				return External.LegacyId == SavedLiving.LegacyId;
			}
			return External.Generation > SavedLiving.Generation
				&& External.Revision > SavedLiving.Revision
				&& External.LegacyId != SavedLiving.LegacyId;
		}

		/// <summary>
		/// Compares two living semantic snapshots while ignoring only journal mechanics: revision
		/// and written tick. Engine/writer versions remain facts, so an upgrade writes a new stage.
		/// </summary>
		internal static bool SameLivingSnapshot(KingdomSealRecord A, KingdomSealRecord B)
		{
			if (A == null || B == null || A.Status != KingdomSealStatus.Living
				|| B.Status != KingdomSealStatus.Living)
			{
				return false;
			}
			try
			{
				KingdomSealRecord a = KingdomSealRules.Copy(A);
				KingdomSealRecord b = KingdomSealRules.Copy(B);
				a.Revision = 0;
				b.Revision = 0;
				a.WrittenTick = 0L;
				b.WrittenTick = 0L;
				return string.Equals(a.Compose(), b.Compose(), StringComparison.Ordinal);
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
