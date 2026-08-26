using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One extension job proposal. Opening debits <see cref="CargoAmount"/> immediately;
	/// reaching the computed end of <see cref="Legs"/> applies <see cref="CompletionChanges"/>
	/// once. <see cref="Key"/> identifies one logical job forever; while its open or recent
	/// terminal row is retained, retries are idempotent. Retired keys must not be recycled.</summary>
	public sealed class KingdomJobPlan
	{
		private readonly KingdomExtensionLeg[] legs;
		private readonly KingdomResourceChange[] completionChanges;

		/// <summary>Owner-local logical-job key. Never reuse it for another job.</summary>
		public readonly string Key;
		/// <summary>Owner-local carrier key.</summary>
		public readonly string CarrierKey;
		/// <summary>Owner-local cargo resource key.</summary>
		public readonly string CargoResourceKey;
		/// <summary>Units reserved when the job opens.</summary>
		public readonly int CargoAmount;
		/// <summary>Exact model tick at which the proposal opens.</summary>
		public readonly long StartTick;

		/// <summary>Builds a frozen proposal. Arrays are copied immediately.</summary>
		public KingdomJobPlan(string Key, string CarrierKey, string CargoResourceKey,
			int CargoAmount, long StartTick, KingdomExtensionLeg[] Legs,
			KingdomResourceChange[] CompletionChanges)
		{
			this.Key = Key;
			this.CarrierKey = CarrierKey;
			this.CargoResourceKey = CargoResourceKey;
			this.CargoAmount = CargoAmount;
			this.StartTick = StartTick;
			legs = Copy(Legs);
			completionChanges = Copy(CompletionChanges);
		}

		/// <summary>Number of itinerary legs.</summary>
		public int LegCount { get { return legs.Length; } }
		/// <summary>Number of changes applied together at completion.</summary>
		public int CompletionChangeCount { get { return completionChanges.Length; } }

		/// <summary>Reads one copied leg; false out of range.</summary>
		public bool TryLeg(int Index, out KingdomExtensionLeg Leg)
		{
			Leg = default(KingdomExtensionLeg);
			if (Index < 0 || Index >= legs.Length) return false;
			Leg = legs[Index];
			return true;
		}

		/// <summary>Reads one copied completion change; false out of range.</summary>
		public bool TryCompletionChange(int Index, out KingdomResourceChange Change)
		{
			Change = default(KingdomResourceChange);
			if (Index < 0 || Index >= completionChanges.Length) return false;
			Change = completionChanges[Index];
			return true;
		}

		private static KingdomExtensionLeg[] Copy(KingdomExtensionLeg[] source)
		{
			if (source == null || source.Length == 0) return new KingdomExtensionLeg[0];
			KingdomExtensionLeg[] copy = new KingdomExtensionLeg[source.Length];
			Array.Copy(source, copy, source.Length);
			return copy;
		}

		private static KingdomResourceChange[] Copy(KingdomResourceChange[] source)
		{
			if (source == null || source.Length == 0) return new KingdomResourceChange[0];
			KingdomResourceChange[] copy = new KingdomResourceChange[source.Length];
			Array.Copy(source, copy, source.Length);
			return copy;
		}
	}
}
