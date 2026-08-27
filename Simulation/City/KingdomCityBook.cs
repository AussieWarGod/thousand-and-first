using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The city book as the save file holds it: one settlement's whole model, written as columns.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.3 puts the model on <c>KingdomSettlement</c> as a named-field
	/// composite, and the frozen-model doctrine says why this type exists at all instead of the
	/// carrier being <see cref="KingdomCityState"/> itself: <b>a named-field reader must assign
	/// fields, and the rules layer must not.</b> So the rules layer keeps <see cref="KingdomCityState"/>
	/// sealed, frozen and total; this holds the same rows in mutable columns the engine can fill,
	/// and the two meet at exactly two methods — <see cref="TryRead"/> and <see cref="TryPublish"/>.
	/// </para>
	/// <para>
	/// <b>Columns, not a list of row objects.</b> &sect;0.0(c) budgets the model with no per-row
	/// object header, and a <c>List</c> of row composites would put one on every row and hold them
	/// for the life of the game. Flat primitive columns carry the same fields at the same widths,
	/// and <c>List&lt;int&gt;</c> / <c>List&lt;long&gt;</c> / <c>List&lt;string&gt;</c> are exactly
	/// what this mod already writes through named fields elsewhere.
	/// </para>
	/// <para>
	/// <b>One publisher.</b> Every column is rewritten in one call from one frozen snapshot, after
	/// the rules have succeeded. Nothing here is ever partially incremented, so a fault leaves the
	/// settlement byte-identical &mdash; the same contract <c>FixedPeriodToyState</c> keeps.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class KingdomCityBook
#if !TAF_TESTS
		: IComposite
#endif
	{
	}
}
