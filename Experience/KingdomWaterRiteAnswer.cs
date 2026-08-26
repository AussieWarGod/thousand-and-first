using System;

namespace ThousandAndFirst
{
	/// <summary>What one settler said, once the water was poured. Every value but
	/// <see cref="Accepted"/> costs the water all the same.</summary>
	public enum WaterRiteAnswer
	{
		/// <summary>They drank to it, and hold with the realm's creed from that evening on.</summary>
		Accepted = 0,

		/// <summary>They have not lived enough of this settlement's life to owe it a belief. The
		/// one refusal shared living alone will lift.</summary>
		TooNew = 1,

		/// <summary>A shrine consecrated to something else stands within sight of their own door,
		/// and it makes its argument every morning. The one refusal the founder can go and change
		/// today.</summary>
		RivalShrine = 2,

		/// <summary>They hold their own belief the way the founder holds the basin. Not a fault
		/// and not a fixable thing; the honest name for a road longer than it looks.</summary>
		Devout = 3,

		/// <summary>What stands between the two creeds is more than any shared life could cross.
		/// One of the two has to move.</summary>
		TooBitter = 4,

		/// <summary>They will not have belief put to them by anybody. An authored <c>Refuses</c>
		/// naming the faith tag, and absolute at every distance.</summary>
		Steadfast = 5
	}
}
