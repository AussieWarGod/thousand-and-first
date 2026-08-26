using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Where a granted part actually has to sit for its events to reach anybody.
	/// <para>
	/// <b>This is the audit's whole lesson, and it is a fact about vanilla, not a preference.</b>
	/// One melee hit fires <c>"AttackerHit"</c> on the ATTACKER
	/// (<c>D/XRL/World/Parts/Combat.cs:1146-1154</c> &mdash; <c>Attacker.FireEvent(obj5)</c> at
	/// <c>:1154</c>) and <c>"WeaponHit"</c> on the WEAPON
	/// (<c>:1178-1186</c> &mdash; <c>Weapon.FireEvent(obj7)</c> at <c>:1186</c>). A part whose
	/// <c>Register</c> asks only for the weapon event is <b>inert</b> if it is copied onto a
	/// player's torso: nothing will ever fire it there. No record in this registry ships without
	/// stating which of the two it is, and the grant verb puts it where it said.
	/// </para>
	/// <para>
	/// The weapon a natural attack carries is the limb's own <c>DefaultBehavior</c> object:
	/// <c>BodyPart.GetFirstValidWeapon</c> returns it (<c>D/XRL/World/Anatomy/BodyPart.cs:2874-2895</c>),
	/// <c>Combat.cs:729-756</c> hands it through as the <c>Weapon</c> argument, and
	/// <c>Combat.cs:1636-1639</c> returns early on a null weapon &mdash; so there is no unarmed
	/// branch anywhere: <b>every</b> melee attack has a weapon object, and for a natural attack that
	/// object is the limb's default behaviour. <c>Combat.cs:1648</c> names the case outright, in
	/// vanilla's own words, by accepting a weapon the attacker <c>IsADefaultBehavior</c> of.
	/// </para>
	/// </summary>
	public enum LabAttach : byte
	{
		/// <summary>Copied onto the founder themselves. Correct for anything registering an
		/// <c>Attacker*</c> event, and for every part that answers a pooled event on its
		/// bearer.</summary>
		Body = 0,

		/// <summary>Copied onto the natural weapon standing at the granted slot. The only honest
		/// home for a part that asks solely for <c>"WeaponHit"</c> or <c>"WeaponDealDamage"</c>.
		/// Refused, by name, at a slot that bears no natural weapon.</summary>
		Weapon = 1
	}
}
