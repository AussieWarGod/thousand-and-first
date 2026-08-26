using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The pure half of LIVING-CITY-ARCHITECTURE &sect;3.8: what to do about a key, given what the
	/// registry holds for it and what the ground says about the body it names.
	/// <para>
	/// Total over every representable input, engine-free, and the only place the four outcomes are
	/// decided. The engine edge supplies the presence and obeys the verdict; nothing else is
	/// allowed to reason about duplication.
	/// </para>
	/// </summary>
	internal static class KingdomBindingRules
	{
		/// <summary>
		/// Check-before-mint, exactly as &sect;3.8 tabulates it.
		/// <list type="bullet">
		/// <item><description>hit, resolves live in THIS zone &rarr; MOVE it, do not mint;</description></item>
		/// <item><description>hit, resolves live in another RESIDENT zone &rarr; a resident moves
		/// across, a transient is refused;</description></item>
		/// <item><description>hit, does not resolve (its zone is on disk) &rarr; REFUSE THE MINT,
		/// the debt stays owed;</description></item>
		/// <item><description>miss &rarr; mint, and write the binding in the same publish.</description></item>
		/// </list>
		/// </summary>
		internal static KingdomBindingVerdict Judge(KingdomBindingKind kind, KingdomBodyPresence presence)
		{
			switch (presence)
			{
			case KingdomBodyPresence.Here:
				return KingdomBindingVerdict.Move;
			case KingdomBodyPresence.Elsewhere:
				return (kind == KingdomBindingKind.Resident)
					? KingdomBindingVerdict.MoveAcross
					: KingdomBindingVerdict.Refuse;
			case KingdomBodyPresence.Frozen:
				return KingdomBindingVerdict.Refuse;
			case KingdomBodyPresence.None:
				return KingdomBindingVerdict.Mint;
			default:
				// A presence this build has no word for is not a licence to mint. The default of a
				// duplication rule is always the side that cannot duplicate.
				return KingdomBindingVerdict.Refuse;
			}
		}

		/// <summary>Whether a verdict is one that puts a NEW body on the ground. The one question
		/// the reify budget asks, and the reason the four outcomes are not a bool.</summary>
		internal static bool Mints(KingdomBindingVerdict verdict)
		{
			return verdict == KingdomBindingVerdict.Mint;
		}

		/// <summary>
		/// The stale-transient sweep's verdict on one object found in a thawed zone.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.8 t3: any object carrying a <c>KingdomJobId</c> with no
		/// open binding is stale, because the model closed the job and evicted the binding while
		/// the ground was on disk, and what the body is carrying was already credited to the stores
		/// at the dated tick. <b>W2 ships the verdict; the despawn is W3.</b>
		/// </para>
		/// <para>
		/// A resident is never swept, and there is no argument about it here because there is no
		/// input for it: the sweep is keyed on a job id, and a person does not have one.
		/// </para>
		/// </summary>
		internal static KingdomSweepVerdict JudgeStale(int jobId, bool hasOpenBinding)
		{
			if (jobId == 0)
			{
				return KingdomSweepVerdict.NotTransient;
			}
			return hasOpenBinding ? KingdomSweepVerdict.Bound : KingdomSweepVerdict.Stale;
		}
	}

}
