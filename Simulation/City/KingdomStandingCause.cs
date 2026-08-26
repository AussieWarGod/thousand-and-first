using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Why a row left <see cref="KingdomResidentStanding.Resident"/>.
	/// <para>
	/// A small named vocabulary rather than a stored sentence, for the reason the district code is
	/// a code: the prose belongs in one place and the row carries what the prose is derived from.
	/// The four death causes are <c>KingdomOfficeRules.DeathCause</c>'s own, in its own order, so
	/// the funeral the city already tells is the ONE telling &mdash; see
	/// <c>KingdomResidentRules.TryDeathCauseOrdinal</c>, which is the only bridge between them and
	/// exists so no second cause vocabulary is ever written.
	/// </para>
	/// </summary>
	internal enum KingdomStandingCause : byte
	{
		/// <summary>Nothing has happened. The only cause a <c>Resident</c> row may carry.</summary>
		None = 0,

		/// <summary>Dead, and no killer was reported. <c>DeathCause.Unknown</c>.</summary>
		Unwitnessed = 1,

		/// <summary>Dead by a hand the settlement cannot name. <c>DeathCause.Violence</c>.</summary>
		Violence = 2,

		/// <summary>Dead defending the stores when raiders came. <c>DeathCause.Raid</c>.</summary>
		Raid = 3,

		/// <summary>Dead by the founder's own hand. <c>DeathCause.Player</c>.</summary>
		Founder = 4,

		/// <summary>Abroad: walked out following the founder.</summary>
		Followed = 5,

		/// <summary>Abroad: taken by somebody else's hand &mdash; charmed, recruited, carried
		/// off.</summary>
		Taken = 6,

		/// <summary>Abroad: the body is not in the ground the row was bound to, and the realm
		/// cannot say where it went. Honestly unknown rather than guessed at.</summary>
		Astray = 7
	}
}
