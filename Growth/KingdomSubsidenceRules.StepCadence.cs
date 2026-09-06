namespace ThousandAndFirst
{
	public static partial class KingdomSubsidenceRules
	{
		/// <summary>World days between household departures. This is the existing subsidence
		/// clock's cadence, shared by the trajectory and its durable single-step consumer.</summary>
		public const int StepDays = 4;

		/// <summary>Defensive loop bound beyond every possible departure and lost rung.</summary>
		public const int MaxSteps = KingdomRules.MaxPopulation + 8;

		/// <summary>Named departures per complete slide, not per household step.</summary>
		public const int NamedDeparturesPerSlide = 3;

		/// <summary>Samples the first two and final departure; short slides name everyone.</summary>
		/// <param name="Index">Zero-based departure index.</param>
		/// <param name="Departed">Total planned departures in the slide.</param>
		public static bool TellsDeparture(int Index, int Departed)
		{
			if (Index < 0 || Departed <= 0 || Index >= Departed) return false;
			return Departed <= NamedDeparturesPerSlide || Index < NamedDeparturesPerSlide - 1
				|| Index == Departed - 1;
		}

		/// <summary>How many departures are named in a complete slide.</summary>
		public static int NamedDepartures(int Departed)
		{
			return Departed <= 0 ? 0 : Departed < NamedDeparturesPerSlide ? Departed : NamedDeparturesPerSlide;
		}

		/// <summary>One household at Camp, five at City. A partially completed step keeps its
		/// originally frozen quota even if the settlement falls to a lower stage meanwhile.</summary>
		public static int SettlersPerStep(GrowthStage Stage)
		{
			int index = (int)Stage;
			if (index < 0) index = 0;
			if (index > (int)GrowthStage.City) index = (int)GrowthStage.City;
			return index + 1;
		}
	}
}
