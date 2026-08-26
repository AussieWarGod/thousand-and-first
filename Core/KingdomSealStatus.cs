using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Where a seal is in its life. The state machine of
	/// <c>INHERITANCE-SEAMS.md:158-218</c>, named.</summary>
	internal enum KingdomSealStatus
	{
		/// <summary>A live stage. The realm is still being played; this record creates no
		/// behaviour anywhere and proves nothing.</summary>
		Living = 0,
		/// <summary>A terminal attempt: the founder died, and the cause has been written down. It
		/// is still only an attempt &mdash; a checkpoint restore or continued play overwrites it.</summary>
		Terminal = 1,
		/// <summary>Deliberate retirement. The founder closed the book themselves, and this
		/// generation of the lineage can no longer be rewritten by continuing to play.</summary>
		Retired = 2,
		/// <summary>Promoted: an ended run, proved ended, with the interregnum drawn and the
		/// inherited state fixed. Immutable from here.</summary>
		Promoted = 3
	}

}
