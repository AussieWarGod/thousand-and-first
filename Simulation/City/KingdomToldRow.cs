using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One line of the told-log ring. Everything in it has already happened — it is historical
	/// identity proof, not a due-job queue, which is the kernel's own distinction stated in
	/// <c>FixedPeriodToy</c> and repeated at LIVING-CITY-ARCHITECTURE &sect;1.2(f).
	/// </summary>
	internal readonly struct KingdomToldRow
	{
		internal readonly KingdomToldKind Kind;

		internal readonly long Tick;

		internal readonly int SubjectA;

		internal readonly int SubjectB;

		internal readonly string PlaceZoneId;

		internal readonly int Outcome;

		internal KingdomToldRow(KingdomToldKind kind, long tick, int subjectA, int subjectB, string placeZoneId, int outcome)
		{
			Kind = kind;
			Tick = tick;
			SubjectA = subjectA;
			SubjectB = subjectB;
			PlaceZoneId = placeZoneId;
			Outcome = outcome;
		}
	}
}
