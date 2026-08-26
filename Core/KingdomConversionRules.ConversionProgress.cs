using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// How far along one settler is toward holding with somebody else's creed, and which creed
	/// that is. Immutable: every transition returns a new value (<see cref="KingdomConversionRules.Advance"/>),
	/// because a half-converted soul that can be mutated in place is a soul two callers can
	/// disagree about.
	/// </summary>
	public readonly struct ConversionProgress
	{
		/// <summary>The creed this settler is being pulled toward, or null when nothing is pulling
		/// at them &mdash; which is nearly everyone, nearly always.</summary>
		public readonly string Creed;

		/// <summary>Shared living accumulated toward <see cref="Creed"/>, in the units
		/// <see cref="KingdomConversionRules.SharedLivingForConversion"/> is denominated in. Never
		/// negative.</summary>
		public readonly int Shared;

		public ConversionProgress(string Creed, int Shared)
		{
			this.Creed = string.IsNullOrEmpty(Creed) ? null : Creed;
			this.Shared = (Shared < 0 || this.Creed == null) ? 0 : Shared;
		}

		/// <summary>Nobody is pulling at this settler. The state every settler starts in and
		/// returns to.</summary>
		public static ConversionProgress None
		{
			get { return new ConversionProgress(null, 0); }
		}

		/// <summary>Whether anything at all is pulling at this settler.</summary>
		public bool Any
		{
			get { return Creed != null && Shared > 0; }
		}
	}

}
