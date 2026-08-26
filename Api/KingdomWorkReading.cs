using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One work, and what the model knows about how it is running.</summary>
	public readonly struct KingdomWorkReading
	{
		/// <summary>The model's own id for this work. Stable across a save.</summary>
		public readonly int WorkId;

		/// <summary>Which ground it stands on.</summary>
		public readonly string ZoneId;

		/// <summary>The catalogue key it was raised from.</summary>
		public readonly string DesignKey;

		/// <summary>Wear, as a percentage of sound.</summary>
		public readonly int ConditionPercent;

		/// <summary>Hands set on it.</summary>
		public readonly int CrewAssigned;

		/// <summary>What kind of run-state it carries.</summary>
		public readonly KingdomWorkClass Class;

		/// <summary>Growth stage for a growing ground; unread for every other class.</summary>
		public readonly int Stage;

		/// <summary>Progress for a producer or refiner; charge for a power work.</summary>
		public readonly int Progress;

		/// <summary>The next breakpoint, never a countdown.</summary>
		public readonly long NextTick;

		/// <summary>Builds a work reading.</summary>
		public KingdomWorkReading(int WorkId, string ZoneId, string DesignKey, int ConditionPercent,
			int CrewAssigned, KingdomWorkClass Class, int Stage, int Progress, long NextTick)
		{
			this.WorkId = WorkId;
			this.ZoneId = ZoneId;
			this.DesignKey = DesignKey;
			this.ConditionPercent = ConditionPercent;
			this.CrewAssigned = CrewAssigned;
			this.Class = Class;
			this.Stage = Stage;
			this.Progress = Progress;
			this.NextTick = NextTick;
		}
	}
}
