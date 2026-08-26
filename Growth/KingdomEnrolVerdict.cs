using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to enter somebody on a city's rolls came to.
	/// <para>
	/// The order below is frozen and the refusals are ordered by what a founder can do about
	/// them, nearest first &mdash; the same discipline <c>CitizenRiteVerdict</c> keeps, and for
	/// the same reason: a refusal is a sentence, and a sentence that names the wrong obstacle is
	/// worse than none.
	/// </para>
	/// </summary>
	public enum KingdomEnrolVerdict : byte
	{
		/// <summary>Enter them.</summary>
		Allowed = 0,

		/// <summary>There is no realm, or this is not its ground.</summary>
		Unfounded = 1,

		/// <summary>No annexe stands in this city. Reachable only through a caller that did not
		/// come in through the building.</summary>
		NoAnnexe = 2,

		/// <summary>The annexe stands and nobody is at the register.</summary>
		Unstaffed = 3,

		/// <summary>Neither the founder nor one of this city's own. The rolls are a city's, and a
		/// city does not enrol a stranger who happened to walk past.</summary>
		NotOurs = 4,

		/// <summary>True Kin already, by birth. There is nothing here to give them &mdash; the
		/// machines have never once asked them a question.</summary>
		Kin = 5,

		/// <summary>Already on the rolls. Once is the whole of it.</summary>
		Enrolled = 6,

		/// <summary>The stores cannot spare the ceremony's water.</summary>
		Unpaid = 7
	}
}
