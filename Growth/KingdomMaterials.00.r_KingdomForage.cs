using System;

namespace XRL.World.Parts
{
	/// <summary>Heart-local brush duty. Resolved only by the settlement pass, never per turn.</summary>
	[Serializable]
	public class r_KingdomForage : IPart
	{
		public long LastWorkedTick;
		public bool NoHandsAnnounced;
		public bool NoBrushAnnounced;
		public bool NoRoomAnnounced;
		public bool EnoughAnnounced;
		public bool BlockedAnnounced;
		/// <summary>Set before irreversible callbacks; a torn removal/deposit never retries.</summary>
		public bool Held;
	}
}
