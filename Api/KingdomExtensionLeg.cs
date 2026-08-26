using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One immutable leg proposed by an extension job. Zone ids must be held by the
	/// supplied city reading; coordinates are bounded to Qud's ordinary 80 by 25 zone envelope.</summary>
	public readonly struct KingdomExtensionLeg
	{
		/// <summary>Held zone this leg crosses.</summary>
		public readonly string ZoneId;
		/// <summary>Entry X coordinate.</summary>
		public readonly short EnterX;
		/// <summary>Entry Y coordinate.</summary>
		public readonly short EnterY;
		/// <summary>Exit X coordinate.</summary>
		public readonly short ExitX;
		/// <summary>Exit Y coordinate.</summary>
		public readonly short ExitY;

		/// <summary>Builds one proposed itinerary leg.</summary>
		public KingdomExtensionLeg(string ZoneId, short EnterX, short EnterY, short ExitX, short ExitY)
		{
			this.ZoneId = ZoneId;
			this.EnterX = EnterX;
			this.EnterY = EnterY;
			this.ExitX = ExitX;
			this.ExitY = ExitY;
		}
	}
}
