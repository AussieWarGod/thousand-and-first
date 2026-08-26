using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to set the crown down came to.
	/// <para>
	/// The two answers that are not refusals are told apart because they are different events in
	/// the world: a realm getting a capital for the first time and a realm moving one are not the
	/// same sentence, and the second one costs the founder a crossing topology.
	/// </para>
	/// </summary>
	public enum KingdomCrownVerdict : byte
	{
		/// <summary>The realm had no capital. This city is it now.</summary>
		Crowns = 0,

		/// <summary>A crown stood in another city. This act moves it, and the arches re-key.</summary>
		Moves = 1,

		/// <summary>The crown is already set down here. Nothing to do, and the caller says so
		/// rather than asking a question with one answer.</summary>
		AlreadyHere = 2,

		/// <summary>There is no realm yet, so there is nothing for a capital to be the capital
		/// of.</summary>
		RefusedUnfounded = 3,

		/// <summary>This hall does not stand on ground the realm holds.</summary>
		RefusedNotOurGround = 4,

		/// <summary>The settlement never raised this hall.</summary>
		RefusedNotOurWork = 5,

		/// <summary>The city's name could not be written into the realm's own record &mdash; empty,
		/// or carrying the record's separator. Refused rather than escaped, exactly as the arches'
		/// register refuses one (<c>KingdomMirrorGateRules.Storable</c>).</summary>
		RefusedNamed = 6
	}

}
