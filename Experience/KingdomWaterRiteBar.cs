using System;

namespace ThousandAndFirst
{
	/// <summary>Why the founder may not put the basin down in front of this settler at all.</summary>
	public enum WaterRiteBar
	{
		/// <summary>Nothing stands in the way. The rite may be offered, and it will cost what
		/// <see cref="KingdomWaterRiteRules.Cost"/> says.</summary>
		Ready = 0,

		/// <summary>The founder is not standing on the settlement's own ground.</summary>
		NotOnOurGround = 1,

		/// <summary>The realm holds no creed of its own, so there is nothing to share water
		/// toward.</summary>
		RealmBelievesNothing = 2,

		/// <summary>They already hold what the realm holds. There is nothing between them.</summary>
		NothingBetweenYou = 3,

		/// <summary>Reserved compatibility value from the earlier bundled-office prototype.
		/// A title-only civic office never produces this bar.</summary>
		TheirOffice = 4,

		/// <summary>They could not leave if they wanted to, so their yes would not be a yes.</summary>
		NoRoadOut = 5,

		/// <summary>They have been asked as many times as anyone should be. The question is shut
		/// for as long as the realm holds what it holds.</summary>
		AskedTooOften = 6,

		/// <summary>They answered, and nothing has changed since. See
		/// <see cref="KingdomWaterRiteRules.SomethingChanged"/>.</summary>
		AlreadyAnswered = 7,

		/// <summary>The founder poured for one of their own too recently.</summary>
		PouredTooRecently = 8,

		/// <summary>The dedicated stores cannot bear the drams.</summary>
		StoresCannotBear = 9
	}
}
