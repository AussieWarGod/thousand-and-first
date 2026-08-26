using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Pure arithmetic and eligibility for improving a work the settlement already has into a
	/// better one: the cask rack that becomes a great cistern, the thorn palisade that becomes a
	/// stone rampart. Engine-free by design, so every refusal &mdash; and every refusal's
	/// sentence &mdash; is provable without a running game. The engine-coupled half, which reads
	/// real objects and raises real scaffolding, lives in <c>KingdomUpgrade</c> in this folder.
	/// <para>
	/// Nothing here decides that an improvement is a good idea. It decides whether one is
	/// <i>earned</i>, and when it is not, which single sentence the founder is owed.
	/// </para>
	/// </summary>
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// What a work's improvement is waiting on, if anything. Ordered the way
		/// <see cref="Assess"/> checks them: what the data says comes first, then what the
		/// founder said, then what the settlement has earned, and the pacing gate last so a work
		/// that is otherwise ready reports the honest "the settlement is already busy" rather
		/// than a condition it actually meets.
		/// <para>
		/// Every member is either a <i>not applicable</i> case, which says nothing (STANDARDS 7b's
		/// first kind), or an <i>applicable but blocked</i> case, which owes the founder a line
		/// &mdash; see <see cref="ReasonLine"/>, which returns null for exactly the former set.
		/// </para>
		/// </summary>
		public enum UpgradeVerdict
		{
			/// <summary>Earned, affordable, and free to begin.</summary>
			Ready = 0,

			/// <summary>The design names nothing to grow into. Silent: most designs never
			/// change, and that is not a stall.</summary>
			NoSuccessor = 1,

			/// <summary>The design names a successor no registry entry defines. Malformed data,
			/// not a settlement condition; announced so a third-party chain that half-loaded
			/// says so instead of doing nothing forever.</summary>
			SuccessorUnknown = 2,

			/// <summary>The successor is not built in this style of city. Silent: it never
			/// appeared in the commission list either, so nothing has stalled.</summary>
			StyleForbids = 3,

			/// <summary>Not a work the settlement raised &mdash; the founder built or placed it
			/// and the settlement merely adopted it. Silent as a message and stated in the list:
			/// the protection law means the settlement never rebuilds what the player made.
			/// </summary>
			NotOurWork = 4,

			/// <summary>Already improving. The scaffold is standing; nothing is wrong.</summary>
			AlreadyWorking = 5,

			/// <summary>The founder told the settlement to leave this ground as it is.</summary>
			HeldOnThisGround = 6,

			/// <summary>The founder told the settlement to leave this one work as it is.</summary>
			HeldByFounder = 7,

			/// <summary>The settlement is not yet large enough for the successor.</summary>
			StageTooLow = 8,

			/// <summary>Every settler is already spoken for; nobody is free to do the work.
			/// </summary>
			NotEnoughHands = 9,

			/// <summary>The successor could not hold what the predecessor is carrying, so the
			/// improvement is refused rather than risking the founder's water or goods on a
			/// badly authored chain.</summary>
			WouldSpill = 10,

			/// <summary>The stores cannot pay for it without dropping below the reserve the
			/// settlement lives on.</summary>
			NotEnoughWater = 11,

			/// <summary>Another improvement is already under way on this ground. The settlement
			/// betters one work at a time.</summary>
			WorksElsewhere = 12,

			/// <summary>The next tier wants more of the plot than the founder staked, or the ground
			/// it would grow onto is where a household's yard trade stands. The sentence comes from
			/// <c>KingdomPlots.GrowRefused</c>, which is the only half that can see real ground;
			/// <see cref="ReasonLine"/> carries the general line for it so no blocked verdict is
			/// ever silent, and the engine half replaces it with the particular one.</summary>
			NoGroundToGrow = 13,

			/// <summary>The settlement's own craft has not come far enough for the successor, or
			/// its keepers have not learned it. Material and craft gate everything, improvements
			/// included; the district and territory gates deliberately do not apply, because the
			/// predecessor is already standing on ground that passed them.</summary>
			CraftNotMet = 14,

			/// <summary>The stockpiles do not cover the improvement's material. Asked BEFORE the
			/// work is begun rather than discovered halfway through paying for it.</summary>
			NotEnoughMaterial = 15,

			/// <summary>Housing whose residents have nowhere they would tolerate sleeping while it
			/// is rebuilt. Never a matter of how long they have lived there &mdash; only of whether
			/// somewhere of their own standard is standing empty now.</summary>
			NoTolerableLodging = 16,

			/// <summary>
			/// Earned, affordable, materialled, crafted, and free to begin &mdash; and NOT begun,
			/// because the settlement leans on what this work puts out and the stores could not
			/// carry the loss for as long as the work would take. The one verdict that is an OFFER
			/// rather than a refusal: the founder may force it from the Charter once the dip has
			/// been disclosed to them (<see cref="DipLine"/>). Checked last, so every real refusal
			/// outranks it and a work is only ever offered when nothing else stands in its way.
			/// </summary>
			HeldOffer = 17
		}

		/// <summary>An upgrade chain read off one <c>&lt;building&gt;</c> entry's optional
		/// attributes. A design with no <c>UpgradesTo</c> yields one of these with
		/// <see cref="Defined"/> false, which is the state every design that ships today is in.
		/// </summary>
		public class UpgradeChain
		{
			/// <summary>Registry key of the design this one grows into, or null.</summary>
			public string SuccessorKey;

			/// <summary>Authored water cost, or <see cref="Unset"/> to compute one.</summary>
			public int CostDramsOverride = Unset;

			/// <summary>Authored build time, or <see cref="UnsetTicks"/> to compute one.</summary>
			public long BuildTicksOverride = UnsetTicks;

			/// <summary>Authored free-hand requirement, or <see cref="Unset"/> to compute one.
			/// </summary>
			public int CrewOverride = Unset;

			/// <summary>Whether <see cref="MinStageOverride"/> was authored at all. False means
			/// the successor's own <c>MinStage</c> is the gate.</summary>
			public bool HasMinStageOverride;

			public GrowthStage MinStageOverride;

			/// <summary>False for every design that never changes.</summary>
			public bool Defined => !string.IsNullOrEmpty(SuccessorKey);
		}

		/// <summary>Sentinel for an integer attribute the author did not write. Negative so it
		/// can never be confused with a real cost or crew, both of which may legitimately be
		/// zero.</summary>
		public const int Unset = -1;

		/// <summary>Sentinel for an unwritten tick count. Zero, because a build time of zero is
		/// already rejected as malformed.</summary>
		public const long UnsetTicks = 0L;

		/// <summary>Water an improvement costs at the very least, however cheap the arithmetic
		/// makes it. Something is always carried, mixed, and poured, so an improvement is never
		/// free even when the successor's own design is no dearer than the predecessor's.
		/// </summary>
		public const int MinimumCostDrams = 2;

		/// <summary>What fraction of a fresh build an improvement takes when the author did not
		/// say. Under a hundred because the predecessor's own footing, frame, and materials are
		/// standing there already; not far under, because the founder should see the work
		/// happen.</summary>
		public const int BuildTicksPercent = 75;

		/// <summary>Settlers who must be free even for an improvement the successor needs no
		/// permanent crew for. Somebody does the work; nobody does it for nothing.</summary>
		public const int MinimumCrew = 1;

	}
}
