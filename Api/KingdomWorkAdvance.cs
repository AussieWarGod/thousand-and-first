using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One work-behaviour result. Resource changes commit atomically with the new run
	/// state; physical debts remain explicit until the attended projection lands them.</summary>
	public sealed class KingdomWorkAdvance
	{
		private readonly KingdomResourceChange[] changes;
		private readonly KingdomMaterialisation[] materialisations;

		/// <summary>Existing city work row this result advances.</summary>
		public readonly int WorkId;
		/// <summary>Owner-local stable behaviour key.</summary>
		public readonly string BehaviourKey;
		/// <summary>Opaque, extension-owned bounded run state.</summary>
		public readonly long NextState;
		/// <summary>Next absolute breakpoint. Must be strictly later than the pass tick, preventing
		/// repeated check-ins at one model tick from replaying the same advance.</summary>
		public readonly long NextTick;

		/// <summary>Builds a frozen work result. Arrays are copied immediately.</summary>
		public KingdomWorkAdvance(int WorkId, string BehaviourKey, long NextState, long NextTick,
			KingdomResourceChange[] Changes, KingdomMaterialisation[] Materialisations)
		{
			this.WorkId = WorkId;
			this.BehaviourKey = BehaviourKey;
			this.NextState = NextState;
			this.NextTick = NextTick;
			changes = Copy(Changes);
			materialisations = Copy(Materialisations);
		}

		/// <summary>Atomic resource-change count.</summary>
		public int ChangeCount { get { return changes.Length; } }
		/// <summary>Physical-debt count.</summary>
		public int MaterialisationCount { get { return materialisations.Length; } }

		/// <summary>Reads one copied resource change; false out of range.</summary>
		public bool TryChange(int Index, out KingdomResourceChange Change)
		{
			Change = default(KingdomResourceChange);
			if (Index < 0 || Index >= changes.Length) return false;
			Change = changes[Index];
			return true;
		}

		/// <summary>Reads one copied materialisation debt; false out of range.</summary>
		public bool TryMaterialisation(int Index, out KingdomMaterialisation Materialisation)
		{
			Materialisation = default(KingdomMaterialisation);
			if (Index < 0 || Index >= materialisations.Length) return false;
			Materialisation = materialisations[Index];
			return true;
		}

		private static KingdomResourceChange[] Copy(KingdomResourceChange[] source)
		{
			if (source == null || source.Length == 0) return new KingdomResourceChange[0];
			KingdomResourceChange[] copy = new KingdomResourceChange[source.Length];
			Array.Copy(source, copy, source.Length);
			return copy;
		}

		private static KingdomMaterialisation[] Copy(KingdomMaterialisation[] source)
		{
			if (source == null || source.Length == 0) return new KingdomMaterialisation[0];
			KingdomMaterialisation[] copy = new KingdomMaterialisation[source.Length];
			Array.Copy(source, copy, source.Length);
			return copy;
		}
	}
}
