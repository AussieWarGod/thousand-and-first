using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>What one turn's budget actually spent. LIVING-CITY-ARCHITECTURE &sect;3.5.</summary>
	internal readonly struct KingdomReifySpend
	{
		internal readonly int Heavy;

		internal readonly int Medium;

		internal readonly int Light;

		/// <summary>Of the units above, how many were visible-first. This is what makes the
		/// guarantee perceptual rather than merely amortised.</summary>
		internal readonly int Visible;

		internal readonly int ThirdsSpent;

		internal KingdomReifySpend(int heavy, int medium, int light, int visible, int thirdsSpent)
		{
			Heavy = heavy;
			Medium = medium;
			Light = light;
			Visible = visible;
			ThirdsSpent = thirdsSpent;
		}

		internal int Units
		{
			get { return Heavy + Medium + Light; }
		}
	}
}
