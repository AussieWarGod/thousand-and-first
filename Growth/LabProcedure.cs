using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// One authored procedure. A catalogue record in the same idiom as a building, shipped in
	/// <c>KingdomProcedures.xml</c>, mergeable by key, and extended by anybody who ships a file
	/// with the matching root (STANDARDS &sect;6).
	/// </summary>
	public sealed class LabProcedure
	{
		/// <summary>Registry identity. Merge-by-key, folded, like every other data lane.</summary>
		public string Key;

		/// <summary>What the slate calls it. Falls back to <see cref="Key"/>.</summary>
		public string DisplayName;

		public LabClass Class = LabClass.Rider;

		/// <summary>
		/// The <c>IPart</c> class this procedure grants &mdash; <b>never a creature, and never "a
		/// creature's power"</b> (DIVERSITY &sect;3.4 hard rule 1). This is what makes the registry
		/// a contract: a modded creature carrying <c>PoisonOnHit</c> is a lawful source for the
		/// envenomed sting the day that mod ships, with no entry of ours.
		/// </summary>
		public string Grants;

		/// <summary><c>BodyPart.Type</c> names, comma separated &mdash; exactly
		/// <c>CyberneticsBaseItem.Slots</c>'s own shape
		/// (<c>D/XRL/World/Parts/CyberneticsBaseItem.cs:14,155-157</c>). Checked against the
		/// founder's OWN anatomy, never against a table.</summary>
		public string Slots;

		/// <summary><c>BodyPartCategory</c> names, comma separated. Empty admits any live
		/// category, so a record that says nothing about kind is a record about every kind.</summary>
		public string SlotCategories;

		public LabSource Source = LabSource.Part;

		public LabAttach Attach = LabAttach.Body;

		/// <summary>The rung of hall this class of work wants. 0 the slab, 1 the vat-house, 2 the
		/// grafting hall, 3 the chimeric theatre.</summary>
		public int MinRung = 2;

		/// <summary>Drams the commission draws from the city's dedicated stores.</summary>
		public int Cost;

		/// <summary>Bits, in vanilla's own bit-string vocabulary.</summary>
		public string Bits;

		/// <summary>Days of the hall's real labour. Never a timer: a hall with no hands works no
		/// days at all (Addendum 8 clause 2).</summary>
		public int StaffDays = 1;

		/// <summary>Preserved parts consumed. One creature, one limb.</summary>
		public int Preserved = 1;

		/// <summary>Standing this costs, in the <c>-Faction</c> removal idiom the QoL vocabulary
		/// already speaks. Spent through the existing <c>AdjustStanding</c> path.</summary>
		public string Creeds;

		/// <summary>Roster tokens the city must hold, in <c>KingdomZoningRules.Knows</c>'s own
		/// grammar. The lab's own gates ride the shipped knowledge lane and mint nothing.</summary>
		public string Knowledge;

		/// <summary>
		/// The band of the source part's own field this record will take, as
		/// <c>Field:Low-High</c>. Null takes anything.
		/// <para>
		/// <b>The QB-10 mechanism, and it names a FIELD rather than a creature</b>, so hard rule 1
		/// survives intact: <c>ReflectDamage</c> ships as two records over one class because a
		/// quartz baboon carries <c>ReflectPercentage="5"</c> and a mirror bug carries
		/// <c>"100"</c>, and under "your sting is its sting" those are not the same product at the
		/// same price. Nothing is clamped &mdash; the founder still gets exactly what they brought
		/// home; the band only decides which slate the thing they brought home appears on.
		/// </para>
		/// </summary>
		public string Magnitude;

		/// <summary>Lines the slate prints under the name, before anything is committed. Authored,
		/// because the one documented complaint about the vanilla picker is consequence-legibility
		/// (DIVERSITY &sect;3.0d), and because a procedure with a cost to the founder's own city
		/// must say so in words (STANDARDS 7b).</summary>
		public List<string> Discloses = new List<string>();

		/// <summary>What the slate calls it, with the key as the fallback so a half-authored record
		/// still reads as something.</summary>
		public string Named => string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;

		/// <summary>Whether this is one of the four. Never listed until found, once ever per
		/// founder, and reset for an heir (Addendum 22 C11).</summary>
		public bool IsNamed => Class == LabClass.Named;
	}
}
