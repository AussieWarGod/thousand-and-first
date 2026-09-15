namespace ThousandAndFirst.Simulation.City
{
	internal readonly partial struct KingdomResidentRow
	{
		internal KingdomResidentRow WithResidence(string Residence, int? HomeWorkId = null)
		{
			return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick,
				HomeWorkId ?? this.HomeWorkId, JobWorkId, JobRole, DayShape, Standing, Cause, BoundZoneId,
				RoofBrink, CreedBrink, CreedToward, CreedChannel, KeptCreeds, Origin, Arrived, Residence);
		}
	}
}
