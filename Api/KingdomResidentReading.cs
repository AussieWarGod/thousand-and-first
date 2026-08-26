using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One settler, as the roll holds them.</summary>
	public readonly struct KingdomResidentReading
	{
		/// <summary>The model's own id. One identity, at most one body (&sect;3.8).</summary>
		public readonly int ResidentId;

		/// <summary>Their name.</summary>
		public readonly string Name;

		/// <summary>The ground their body was last bound in, or null.</summary>
		public readonly string ZoneId;

		/// <summary>Where their day puts them.</summary>
		public readonly KingdomDayPlace Day;

		/// <summary>Whether they live here.</summary>
		public readonly KingdomRollStanding Standing;

		/// <summary>When they arrived.</summary>
		public readonly long ArrivedTick;

		/// <summary>The work they sleep in, or zero.</summary>
		public readonly int HomeWorkId;

		/// <summary>The work they are set on, or zero.</summary>
		public readonly int JobWorkId;

		/// <summary>Builds a resident reading.</summary>
		public KingdomResidentReading(int ResidentId, string Name, string ZoneId, KingdomDayPlace Day,
			KingdomRollStanding Standing, long ArrivedTick, int HomeWorkId, int JobWorkId)
		{
			this.ResidentId = ResidentId;
			this.Name = Name;
			this.ZoneId = ZoneId;
			this.Day = Day;
			this.Standing = Standing;
			this.ArrivedTick = ArrivedTick;
			this.HomeWorkId = HomeWorkId;
			this.JobWorkId = JobWorkId;
		}
	}
}
