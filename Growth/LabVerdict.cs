using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to commission a procedure came to. Every refusal names a thing the founder
	/// could go and do; none of them says "that failed" (STANDARDS 7b).
	/// <para>
	/// Appended to, never renumbered: these are a published vocabulary the moment a third party's
	/// record can provoke one.
	/// </para>
	/// </summary>
	public enum LabVerdict : byte
	{
		/// <summary>The hall will do it.</summary>
		Allowed = 0,

		/// <summary>The founder's anatomy carries no part of the type this record wants. The
		/// rationing mechanism, and the reason the lab cannot become a shopping list.</summary>
		RefusedNoSlot = 1,

		/// <summary>There is such a part, and something is already grafted to it.</summary>
		RefusedSlotTaken = 2,

		/// <summary>There is such a part and it is not of a kind this procedure will open &mdash;
		/// the <c>SlotCategories</c> gate, and the whole of how a True Kin, a robot and a slime get
		/// different legal sets with no genotype list anywhere.</summary>
		RefusedCategory = 3,

		/// <summary>The hall is not built high enough for this class of work.</summary>
		RefusedRung = 4,

		/// <summary>A weapon-attach record at a slot that bears no natural weapon. Nothing to ride,
		/// so nothing is grafted &mdash; the audit's lesson enforced at the commit.</summary>
		RefusedNoWeapon = 5,

		/// <summary>The vat-house is keeping nothing that answers this record.</summary>
		RefusedUnkept = 6,

		/// <summary>A named procedure this founder has already had. Once, ever.</summary>
		RefusedOnceEver = 7,

		/// <summary>A named procedure nobody has found yet. Never named in the refusal, because
		/// saying its name is the thing the visibility law forbids.</summary>
		RefusedUndiscovered = 8,

		/// <summary>What is kept is of the class, and is not of this record's own band. The
		/// QUESTION-BACKLOG QB-10 seam: two records over one class, priced apart by what the
		/// source itself carries.</summary>
		RefusedMagnitude = 9
	}
}
