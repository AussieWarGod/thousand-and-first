using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// What the people of a city believe, and what a realm does about holding two cities that
	/// believe different things.
	/// <para>
	/// A settler's creed is a <b>vanilla faction name</b>. A city's creed is whatever enough of
	/// its residents hold; most cities are mixed and have none. When a realm's two cities hold
	/// creeds the engine's own <c>Factions.xml</c> says are at odds, dissent accrues in real time
	/// — two cities that cannot stand each other go on not standing each other whether or not
	/// anyone is watching — and the founder is told about it four tiers before it can cost them
	/// anything. When it runs to the top the realm stands at a BRINK
	/// (<see cref="KingdomBrinkRules"/>) rather than losing a city on the spot: accrual halts, the
	/// founder is named the city and the honest elapsed, and
	/// <see cref="KingdomCreedRules.SecessionWindowDays"/> world-days stand between the
	/// warning and the split. If those are spent with the quarrel still live, the unhappier city
	/// leaves, keeping its ground, its people and its buildings; nothing is destroyed and nobody
	/// is driven out.
	/// </para>
	/// <para>
	/// The arithmetic all lives in <see cref="KingdomCreedRules"/>. This file is the wiring: what
	/// counts as a creed, what the engine says two creeds think of each other, and the four
	/// moments where the founder can act.
	/// </para>
	/// <para>
	/// A realm of one city never encounters any of this. Every entry point below returns before
	/// doing anything when <c>SettlementCount</c> is under two, so the founder who never founds a
	/// second city cannot be penalised by a system they never opted into.
	/// </para>
	/// </summary>
	public static partial class KingdomCreed
	{
	}
}
