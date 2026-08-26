using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomSuccessionRules
	{
		// ==================================================================================
		// The law (Addendum 22 C3)
		// ==================================================================================

		/// <summary>
		/// Which candidate the realm raises. Seniority is <c>KingdomOffices.UpdateOffice</c>'s own
		/// rule &mdash; the settler who has served longest &mdash; asked a second time and for a
		/// second purpose, which is what makes config B free of new machinery.
		/// <para>
		/// Ties are broken by name and then by resident id rather than left to enumeration order,
		/// because two settlers who arrived on the same tick is the ordinary case for a growth pass
		/// that seated three of them, and a realm that raised a different heir depending on how its
		/// rows happened to be sorted would not be keeping a law at all.
		/// </para>
		/// </summary>
		/// <param name="Candidates">Everyone the realm knows about. Nulls and empty names are skipped.</param>
		/// <param name="Law">The realm's declared custom.</param>
		/// <param name="Designee">The named designee, for <see cref="SuccessionLaw.Designee"/>.</param>
		/// <param name="Index">Index into <paramref name="Candidates"/>, or -1.</param>
		/// <returns>True when somebody may take the charter.</returns>
		public static bool TryChooseHeir(KingdomHeir[] Candidates, SuccessionLaw Law, string Designee, out int Index)
		{
			Index = -1;
			if (Candidates == null)
			{
				return false;
			}
			if (Law == SuccessionLaw.Designee && !string.IsNullOrEmpty(Designee))
			{
				for (int i = 0; i < Candidates.Length; i++)
				{
					if (Eligible(Candidates[i]) && string.Equals(Candidates[i].Name, Designee, StringComparison.Ordinal))
					{
						Index = i;
						return true;
					}
				}
			}
			for (int i = 0; i < Candidates.Length; i++)
			{
				if (!Eligible(Candidates[i]))
				{
					continue;
				}
				if (Index < 0 || Senior(Candidates[i], Candidates[Index]))
				{
					Index = i;
				}
			}
			return Index >= 0;
		}

		/// <summary>Whether a candidate may be raised at all: a name, and still on the roll.</summary>
		public static bool Eligible(KingdomHeir Candidate)
		{
			return !string.IsNullOrEmpty(Candidate.Name) && Candidate.OnTheRoll;
		}

		/// <summary>Whether <paramref name="A"/> outranks <paramref name="B"/> under seniority.</summary>
		public static bool Senior(KingdomHeir A, KingdomHeir B)
		{
			if (A.ArrivedTick != B.ArrivedTick)
			{
				return A.ArrivedTick < B.ArrivedTick;
			}
			int byName = string.CompareOrdinal(A.Name ?? "", B.Name ?? "");
			if (byName != 0)
			{
				return byName < 0;
			}
			return A.ResidentId < B.ResidentId;
		}

		/// <summary>
		/// Whether the run continues, and if not, which honest ending it takes. The order is frozen:
		/// the mode first, because Classic and Roleplay must never reach any of this; then the realm,
		/// because an unfounded death is an ordinary death; then the roll; then the ground.
		/// </summary>
		public static SuccessionVerdict Judge(bool ModeOn, bool Founded, bool AnyHeir, bool HeirReachable)
		{
			if (!ModeOn)
			{
				return SuccessionVerdict.NotKingdomMode;
			}
			if (!Founded)
			{
				return SuccessionVerdict.Unfounded;
			}
			if (!AnyHeir)
			{
				return SuccessionVerdict.NoHeir;
			}
			if (!HeirReachable)
			{
				return SuccessionVerdict.HeirUnreachable;
			}
			return SuccessionVerdict.Succeeds;
		}

		/// <summary>Whether the line has run out, which is the one verdict that ends the run through
		/// Qud's own door: score, tombstone, and whatever the mode does with the save.</summary>
		public static bool DynastyEnds(SuccessionVerdict Verdict)
		{
			return Verdict == SuccessionVerdict.NoHeir || Verdict == SuccessionVerdict.HeirUnreachable;
		}

		// ==================================================================================
		// What the realm thinks of the heir on the day (Addendum 22 C4)
		// ==================================================================================

		/// <summary>Regard the heir cannot start below, however badly their row reads.</summary>
		public const int AccessionRegardFloor = -200;

		/// <summary>
		/// Regard the heir cannot start above. Below <c>KingdomExileRules.RegardLiked</c> on purpose
		/// and by construction: a realm may be glad of its heir and must never begin by trusting
		/// them. Trust is what the run is for.
		/// </summary>
		public const int AccessionRegardCeiling = 200;

		/// <summary>What one month of service is worth to the people who watched it.</summary>
		public const int RegardPerMonthServed = 10;

		/// <summary>Months past which longer service buys nothing more. Ten, so a settler of a
		/// year's standing and one of five years' standing are both simply old hands.</summary>
		public const int MonthsServedCap = 10;

		/// <summary>Held the realm's declared creed.</summary>
		public const int RegardForDeclaredCreed = 75;

		/// <summary>Held the realm's declared creed once and left it (Addendum 16's kept roll). The
		/// realm remembers, and a realm that did not would be a realm with no creed worth leaving.</summary>
		public const int RegardForCreedLeft = -75;

		/// <summary>Already held the settlement's one office when the founder fell.</summary>
		public const int RegardForOffice = 50;

		/// <summary>
		/// What the realm's own faction holds the heir at on the day they take the charter, derived
		/// from the heir's row and from nothing else &mdash; which is the whole of C4: the founder's
		/// diplomatic ledger is the founder's, it dies with them, and the one cell with a better
		/// answer than zero is the realm's own, because the realm actually knew this person.
		/// <para>
		/// Zero is indifference, and it is the honest default: <c>KingdomExileRules</c> already rules
		/// that a realm taking somebody back opens the gate and does not smile
		/// (<c>RegardFloorOnReturn</c>), and a realm burying its founder has even less to smile
		/// about. Everything above zero here was earned by the heir before anyone thought of them as
		/// an heir.
		/// </para>
		/// </summary>
		/// <param name="ArrivedTick">When the heir came.</param>
		/// <param name="NowTick">The tick of the accession.</param>
		/// <param name="CreedMatchesRealm">The heir holds the realm's declared creed.</param>
		/// <param name="OnceLeftRealmCreed">The heir's kept roll names the realm's declared creed.</param>
		/// <param name="HoldsOffice">The heir already held the office.</param>
		public static int AccessionRegard(long ArrivedTick, long NowTick, bool CreedMatchesRealm, bool OnceLeftRealmCreed, bool HoldsOffice)
		{
			int regard = MonthsServed(ArrivedTick, NowTick) * RegardPerMonthServed;
			if (CreedMatchesRealm)
			{
				regard += RegardForDeclaredCreed;
			}
			if (OnceLeftRealmCreed)
			{
				regard += RegardForCreedLeft;
			}
			if (HoldsOffice)
			{
				regard += RegardForOffice;
			}
			if (regard < AccessionRegardFloor)
			{
				return AccessionRegardFloor;
			}
			return (regard > AccessionRegardCeiling) ? AccessionRegardCeiling : regard;
		}

		/// <summary>Whole months of service, capped at <see cref="MonthsServedCap"/>. A row whose
		/// arrival is in the future &mdash; which a save carried across a clock rework can hold
		/// &mdash; counts as no service rather than as negative service.</summary>
		public static int MonthsServed(long ArrivedTick, long NowTick)
		{
			if (ArrivedTick <= 0L || NowTick <= ArrivedTick)
			{
				return 0;
			}
			long days = (NowTick - ArrivedTick) / KingdomRules.TicksPerDay;
			long months = days / DaysPerMonth;
			return (months > MonthsServedCap) ? MonthsServedCap : (int)months;
		}
	}
}
