using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// One place on the founder's body, as the rules half sees it. Read off the real anatomy by
	/// <c>KingdomProcedures.Census</c> and never constructed anywhere else, so every judgment below
	/// is a pure function of what the founder actually is.
	/// </summary>
	public readonly struct LabSlot
	{
		/// <summary>The vanilla <c>BodyPart.Type</c> &mdash; "Arm", "Face", "Fungal Outcrop". An
		/// OPEN vocabulary: <c>B/Bodies.xml</c> declares 157 of them and
		/// <c>CyberneticsGraftedMirrorArm</c> mints "Thrown Weapon" at runtime
		/// (<c>D/XRL/World/Parts/CyberneticsGraftedMirrorArm.cs:31</c>), so nothing here validates
		/// against a closed list.</summary>
		public readonly string Type;

		/// <summary>The <c>BodyPartCategory</c> code, 1 to 23
		/// (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:8-52</c>). Zero for a part whose category
		/// could not be read, which every <c>SlotCategories</c> gate then refuses rather than
		/// guesses at.</summary>
		public readonly int Category;

		/// <summary>Vanilla's own disqualifier: an extrinsic part is worn scaffolding, not the
		/// body. <c>BodyPart.CanReceiveCyberneticImplant</c> refuses on exactly this and on
		/// category (<c>D/XRL/World/Anatomy/BodyPart.cs:7072-7083</c>).</summary>
		public readonly bool Extrinsic;

		/// <summary>Whether this limb carries a <c>DefaultBehavior</c> object &mdash; the thing a
		/// natural attack is actually made with, and the only lawful home for a
		/// <see cref="LabAttach.Weapon"/> record.</summary>
		public readonly bool Bears;

		/// <summary>The key of the procedure already grafted here, or null. One graft to a place:
		/// the ceiling is the founder's body, not their patience.</summary>
		public readonly string Grafted;

		public LabSlot(string Type, int Category, bool Extrinsic, bool Bears, string Grafted)
		{
			this.Type = Type ?? "";
			this.Category = Category;
			this.Extrinsic = Extrinsic;
			this.Bears = Bears;
			this.Grafted = string.IsNullOrEmpty(Grafted) ? null : Grafted;
		}
	}
}
