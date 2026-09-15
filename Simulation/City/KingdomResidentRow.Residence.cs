namespace ThousandAndFirst.Simulation.City
{
	internal readonly partial struct KingdomResidentRow
	{
		internal KingdomResidentRow WithResidence(string Residence)
		{
			return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick,
				HomeWorkId, JobWorkId, JobRole, DayShape, Standing, Cause, BoundZoneId,
				RoofBrink, CreedBrink, CreedToward, CreedChannel, KeptCreeds, Origin, Arrived, Residence);
		}
	}
}
