using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side geometry and stamp stay where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// One plot being raised. Stands in the middle of its own rect from the moment the ground is
	/// staked until the moment the finished work replaces it, and carries everything the raising
	/// needs to know: which rect, which design, which stage, and when the next one falls due.
	/// <para>
	/// A brand-new part, so its serialized field layout is free (STANDARDS 1 forbids APPENDING to
	/// an existing part, not declaring a new one). Nothing here is read positionally by any older
	/// save, because no older save has ever seen this part.
	/// </para>
	/// <para>
	/// New works advance from the settlement pass by resolving elapsed world time through free
	/// labour, the same doctrine <c>r_KingdomScaffold</c> uses: a long absence gives an honestly
	/// crewed plot time to rise, while an empty settlement's frame does not lift itself. The named
	/// labour receipt lives on the parent object so this part's positional field layout still ends
	/// at <see cref="DoorY"/>. Works loaded from the pre-receipt save shape keep their old absolute
	/// clock path for compatibility.
	/// </para>
	/// </summary>
	/// <summary>
	/// The yielding mark, carried by a plot the founder deliberately staked in the ground the
	/// heart was surveyed for. It does nothing at all on its own &mdash; it is the sentence, kept
	/// where the founder can read it back.
	/// <para>
	/// Consent before cost, the carry-sign idiom: the ground is legal to build on and building
	/// there is never refused, but the promise made at the moment the ground was spoken for is
	/// readable on the thing forever, rather than living in a message that scrolled away.
	/// </para>
	/// <para>
	/// A brand-new part, so its serialized field layout is free (STANDARDS 1). It carries no
	/// fields at all, and no turn tick.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomYielding : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n{{rules|").Append(ThousandAndFirst.KingdomPlotRules.YieldingMark).Append("}}");
			return base.HandleEvent(E);
		}
	}
}
