using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a staged seal is allowed to become, from the two facts the engine can prove about the
	/// run it came from. <c>INHERITANCE-SEAMS.md:66-76</c>.
	/// </summary>
	internal enum KingdomSealEligibility
	{
		/// <summary>No score and a save still standing: the run is being played, or was put down.
		/// Nothing crosses.</summary>
		Living = 0,
		/// <summary>A score and a save both: a checkpoint death, permadeath switched off, or a
		/// cleanup that did not finish. Not proof of an end, so nothing crosses automatically.</summary>
		Checkpointed = 1,
		/// <summary>A score and no save: the engine itself ended and cleared that run. This is the
		/// only automatic crossing.</summary>
		Ended = 2,
		/// <summary>Neither. A save deleted by hand, a cleared scoreboard, a stage left by a
		/// vanished game. Never automatic; recoverable only by asking.</summary>
		Orphaned = 3
	}
}
