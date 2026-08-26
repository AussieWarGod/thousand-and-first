using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal enum KingdomSealKind
	{
		Text = 0,
		Number = 1,
		TextList = 2,
		NumberList = 3,
		EmptyList = 4
	}

	/// <summary>
	/// One flat seal payload: ordered keys over bounded primitives, and nothing else.
	/// <para>
	/// Ordered because the canonical form must be reproducible: the same facts written twice
	/// produce the same bytes, and a reader that re-writes what it read produces the file it was
	/// given. Ordering is insertion order, which is the writer's declared order.
	/// </para>
}
