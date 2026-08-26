using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to zone a megastructure came to.
	/// <para>
	/// A refusal here is reserved for a design the founder actually chose and cannot have on this
	/// ground, so the telling means something &mdash; the same shape <c>KingdomGateVerdict</c> keeps
	/// one lane over.
	/// </para>
	/// </summary>
	public enum KingdomPurposeVerdict : byte
	{
		/// <summary>Nothing in the way. Either the design is ordinary, or this city has no purpose
		/// yet and is about to have one.</summary>
		Allowed = 0,

		/// <summary>This city already keeps a megastructure, and it is not this one.</summary>
		RefusedKept = 1,

		/// <summary>The design is one only a capital may raise, and the crown is not set down in
		/// this city (Addendum 22 A4; the capital ruling extending Addendum 19).</summary>
		RefusedUncrowned = 2
	}

}
