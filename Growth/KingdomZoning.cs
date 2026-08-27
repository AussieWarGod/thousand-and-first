using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the settlement's zoning: what a founder may commission, and on
	/// which ground. Reads the optional gates a <c>&lt;building&gt;</c> entry may declare
	/// (see <see cref="KingdomZoningRules"/> for the arithmetic and MODDING.md for the schema),
	/// keeps the roster of designs the keepers have been taught, and composes every refusal so
	/// that it names both what is missing and what would fix it.
	/// <para>
	/// Nothing here ever blocks in silence. A design the founder cannot raise still appears in
	/// the commission list, tagged with the one thing standing in its way
	/// (<see cref="GateNote"/>), and an attempt on it answers with a whole sentence
	/// (<see cref="Permits"/>). That is STANDARDS 7b applied to the one part of a settlement game
	/// where players complain about it most: plots that will not build and will not say why.
	/// </para>
	/// </summary>
	public static partial class KingdomZoning
	{
	}
}
