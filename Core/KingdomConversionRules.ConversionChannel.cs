using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The five ways a settler's creed can change, as Addendum 5 names them. The value is also the
	/// kernel <c>eventKindCode</c> each channel draws on, so it must never be zero and must never
	/// be renumbered: a renumbering would re-roll every conversion that has not happened yet.
	/// </summary>
	public enum ConversionChannel
	{
		/// <summary>The household majority, counted in COHABITATION DAYS under one roof.</summary>
		Osmosis = 1,

		/// <summary>The witnessed shared meal, nudging attendees toward the table's majority.
		/// </summary>
		Culture = 2,

		/// <summary>A shrine consecrated to a creed, staffed, working on its own quarter.</summary>
		Shrine = 3,

		/// <summary>The water ritual with one's own settler: invited, consented, one at a time.
		/// </summary>
		Diplomacy = 4
	}

}
