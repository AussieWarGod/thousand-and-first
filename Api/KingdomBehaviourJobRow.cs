using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>Durable extension job row. Plans are copied into this row at opening; no extension
	/// callback is required for completion or recovery.</summary>
	internal sealed class KingdomBehaviourJobRow
	{
		private readonly KingdomExtensionLeg[] legs;
		private readonly KingdomResourceChange[] completion;

		internal readonly string Key;
		internal readonly string CarrierKey;
		internal readonly string CarrierBlueprint;
		internal readonly int WalkTicksPerCell;
		internal readonly string CargoResourceKey;
		internal readonly int CargoAmount;
		internal readonly long StartTick;
		internal readonly long DueTick;
		internal readonly KingdomExtensionJobStatus Status;

		internal KingdomBehaviourJobRow(string key, string carrierKey, string carrierBlueprint,
			int walkTicksPerCell, string cargoResourceKey, int cargoAmount, long startTick,
			long dueTick, KingdomExtensionJobStatus status, KingdomExtensionLeg[] legs,
			KingdomResourceChange[] completion)
		{
			Key = key ?? "";
			CarrierKey = carrierKey ?? "";
			CarrierBlueprint = carrierBlueprint ?? "";
			WalkTicksPerCell = walkTicksPerCell;
			CargoResourceKey = cargoResourceKey ?? "";
			CargoAmount = cargoAmount;
			StartTick = startTick;
			DueTick = dueTick;
			Status = status;
			this.legs = Copy(legs);
			this.completion = Copy(completion);
		}

		internal int LegCount { get { return legs.Length; } }
		internal int CompletionCount { get { return completion.Length; } }

		internal bool TryLeg(int index, out KingdomExtensionLeg leg)
		{
			leg = default(KingdomExtensionLeg);
			if (index < 0 || index >= legs.Length) return false;
			leg = legs[index]; return true;
		}

		internal bool TryCompletion(int index, out KingdomResourceChange change)
		{
			change = default(KingdomResourceChange);
			if (index < 0 || index >= completion.Length) return false;
			change = completion[index]; return true;
		}

		internal KingdomBehaviourJobRow WithStatus(KingdomExtensionJobStatus status)
		{
			return new KingdomBehaviourJobRow(Key, CarrierKey, CarrierBlueprint, WalkTicksPerCell,
				CargoResourceKey, CargoAmount, StartTick, DueTick, status, legs, completion);
		}

		internal KingdomExtensionJobReading Reading()
		{
			return new KingdomExtensionJobReading(Key, CarrierKey, CarrierBlueprint,
				CargoResourceKey, CargoAmount, StartTick, DueTick, Status);
		}

		private static KingdomExtensionLeg[] Copy(KingdomExtensionLeg[] source)
		{
			if (source == null || source.Length == 0) return new KingdomExtensionLeg[0];
			KingdomExtensionLeg[] copy = new KingdomExtensionLeg[source.Length];
			Array.Copy(source, copy, source.Length); return copy;
		}

		private static KingdomResourceChange[] Copy(KingdomResourceChange[] source)
		{
			if (source == null || source.Length == 0) return new KingdomResourceChange[0];
			KingdomResourceChange[] copy = new KingdomResourceChange[source.Length];
			Array.Copy(source, copy, source.Length); return copy;
		}
	}
}
