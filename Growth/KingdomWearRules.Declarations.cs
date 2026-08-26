using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public enum KingdomWearSinkDisposition
	{
		None = 0,
		Pending = 1,
		Attempting = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}

	/// <summary>Durable attended-pass phases. Values are save-facing object properties.</summary>
	public enum KingdomWearPassPhase
	{
		None = 0,
		Bound = 1,
		StreakIntent = 2,
		StreakDone = 3,
		HardIncident = 4,
		HardDone = 5,
		TemperIncident = 6,
		TemperDone = 7,
		Quarantined = 8
	}

	public enum KingdomWearPassAction
	{
		Start = 0,
		Resume = 1,
		AlreadyApplied = 2,
		Quarantine = 3
	}

	/// <summary>One damage incident's exact mutation and telling phases.</summary>
	public enum KingdomWearIncidentPhase
	{
		None = 0,
		Bound = 1,
		MutationIntent = 2,
		Mutated = 3,
		ChronicleDone = 4,
		MessageIntent = 5,
		MessageDone = 6,
		Complete = 7,
		Quarantined = 8
	}

	/// <summary>One storage-loss incident's exact mutation and telling phases.</summary>
	public enum KingdomWearLeakPhase
	{
		None = 0,
		Bound = 1,
		MutationIntent = 2,
		Mutated = 3,
		ChronicleDone = 4,
		LedgerIntent = 5,
		LedgerDone = 6,
		MessageIntent = 7,
		MessageDone = 8,
		Complete = 9,
		Quarantined = 10
	}

	public enum KingdomWearMutationAction
	{
		Wait = 0,
		Apply = 1,
		Confirm = 2,
		Quarantine = 3
	}

	public enum KingdomWearClockAction
	{
		Plant = 0,
		Wait = 1,
		Advance = 2,
		Quarantine = 3
	}

}
