using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Identity at the engine's edge: who a body is, which book holds their row, and what the
	/// binding registry says about whether they may be minted at all.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;8.3's answer to <i>where a person lives — object or row</i>:
	/// <b>the row is primary and the body is a durable view bound by a stable id.</b> The body
	/// carries <see cref="ResidentIdProperty"/> and nothing else; everything else about the person
	/// that has to survive their zone going to disk lives in a resident row.
	/// </para>
	/// <para>
	/// <b>The id is not a draw.</b> It is the next number off a realm-scope counter, in mint order,
	/// exactly as <c>KingdomCity.DedicationOrderProperty</c> is. Identity is a substrate: a seeded
	/// draw would make who-is-who depend on how many other things had been rolled first, and the
	/// kernel's whole discipline is that draws belong to happenings.
	/// </para>
	/// <para>
	/// Engine-coupled by design and paired with <c>KingdomResidentRules</c> exactly as
	/// <c>KingdomCity</c> is paired with <c>KingdomCityRules</c>: nothing here decides anything, it
	/// reads the ground, asks the rules, and applies the answer.
	/// </para>
	/// </summary>
	public static partial class KingdomResidents
	{
	}
}
