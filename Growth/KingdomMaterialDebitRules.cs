using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Exclusive use of one physical stockpile object in a material debit.</summary>
	public enum KingdomMaterialDebitSourceKind : byte
	{
		None = 0,
		Material = 1,
		Exotic = 2,
		BitStock = 3
	}

	/// <summary>The externally observable terminal result of one material receipt.</summary>
	public enum KingdomMaterialDebitOutcome : byte
	{
		InvalidReservation = 0,
		Reserved = 1,
		ExactCommit = 2,
		CleanRefusal = 3,
		RecoverablePartial = 4,
		IrreversiblePartial = 5,
		CompensatedExact = 6,
		Cancelled = 7
	}

	/// <summary>Why a material receipt did not reach the requested phase.</summary>
	public enum KingdomMaterialDebitFault : byte
	{
		None = 0,
		InvalidStock = 1,
		InvalidCost = 2,
		InvalidSources = 3,
		InsufficientMaterials = 4,
		InsufficientBits = 5,
		InsufficientExotics = 6,
		SourceChanged = 7,
		OperationRefused = 8,
		OperationMismatch = 9,
		Exception = 10,
		CompensationUnsafe = 11,
		CompensationFailed = 12,
		Busy = 13,
		WrongPhase = 14
	}

	/// <summary>
	/// One composite claim against the stockpiles. Each tally is copied on construction, so a
	/// caller cannot alter a reserved price by retaining and editing its original tally.
	/// </summary>
	public sealed class KingdomMaterialDebitCost
	{
		public readonly KingdomMaterialTally Materials;
		public readonly KingdomBitTally Bits;
		public readonly KingdomExoticTally Exotics;

		public KingdomMaterialDebitCost(
			KingdomMaterialTally Materials = null,
			KingdomBitTally Bits = null,
			KingdomExoticTally Exotics = null)
		{
			this.Materials = (Materials == null) ? new KingdomMaterialTally() : Materials.Copy();
			this.Bits = (Bits == null) ? new KingdomBitTally() : Bits.Copy();
			this.Exotics = (Exotics == null) ? new KingdomExoticTally() : Exotics.Copy();
		}

		public bool IsEmpty => Materials.IsEmpty() && Bits.IsEmpty() && Exotics.IsEmpty();

		public KingdomMaterialDebitCost Copy()
		{
			return new KingdomMaterialDebitCost(Materials, Bits, Exotics);
		}

		/// <summary>
		/// Stable primitive encoding for a durable construction or lab job. A live receipt contains
		/// engine references and is deliberately not serializable; this claim is what crosses a save.
		/// </summary>
		public string ToClaimString()
		{
			StringBuilder text = new StringBuilder("v1|m:");
			AppendMaterial(text, Materials);
			text.Append("|b:");
			AppendBits(text, Bits);
			text.Append("|e:");
			AppendExotics(text, Exotics);
			return text.ToString();
		}

		public static bool TryParseClaim(string Text, out KingdomMaterialDebitCost Cost)
		{
			Cost = null;
			if (string.IsNullOrEmpty(Text))
			{
				return false;
			}
			string[] fields = Text.Split('|');
			if (fields.Length != 4 || fields[0] != "v1" || !fields[1].StartsWith("m:", StringComparison.Ordinal)
				|| !fields[2].StartsWith("b:", StringComparison.Ordinal)
				|| !fields[3].StartsWith("e:", StringComparison.Ordinal))
			{
				return false;
			}
			int[] material;
			int[] bits;
			int[] exotics;
			if (!TryParseVector(fields[1].Substring(2), KingdomMaterialRules.MaterialCount, out material)
				|| !TryParseVector(fields[2].Substring(2), KingdomMaterialRules.BitTierCount, out bits)
				|| !TryParseVector(fields[3].Substring(2), KingdomMaterialRules.ExoticCount, out exotics))
			{
				return false;
			}
			KingdomMaterialTally materialTally = new KingdomMaterialTally();
			KingdomBitTally bitTally = new KingdomBitTally();
			KingdomExoticTally exoticTally = new KingdomExoticTally();
			for (int i = 0; i < material.Length; i++)
			{
				materialTally.Set((KingdomMaterial)i, material[i]);
			}
			for (int i = 0; i < bits.Length; i++)
			{
				bitTally.Set(i, bits[i]);
			}
			for (int i = 0; i < exotics.Length; i++)
			{
				exoticTally.Set((KingdomExotic)i, exotics[i]);
			}
			Cost = new KingdomMaterialDebitCost(materialTally, bitTally, exoticTally);
			return true;
		}

		private static void AppendMaterial(StringBuilder Into, KingdomMaterialTally Tally)
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				if (i > 0) Into.Append(',');
				Into.Append(Tally.Get((KingdomMaterial)i).ToString(CultureInfo.InvariantCulture));
			}
		}

		private static void AppendBits(StringBuilder Into, KingdomBitTally Tally)
		{
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if (i > 0) Into.Append(',');
				Into.Append(Tally.Get(i).ToString(CultureInfo.InvariantCulture));
			}
		}

		private static void AppendExotics(StringBuilder Into, KingdomExoticTally Tally)
		{
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				if (i > 0) Into.Append(',');
				Into.Append(Tally.Get((KingdomExotic)i).ToString(CultureInfo.InvariantCulture));
			}
		}

		private static bool TryParseVector(string Text, int Count, out int[] Values)
		{
			Values = null;
			string[] terms = Text.Split(',');
			if (terms.Length != Count)
			{
				return false;
			}
			Values = new int[Count];
			for (int i = 0; i < terms.Length; i++)
			{
				int value;
				if (!int.TryParse(terms[i], NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0)
				{
					Values = null;
					return false;
				}
				Values[i] = value;
			}
			return true;
		}
	}

	/// <summary>Engine-free reading of one unique physical source.</summary>
	public sealed class KingdomMaterialDebitSource
	{
		public readonly int Source;
		public readonly KingdomMaterialDebitSourceKind Kind;
		public readonly int KindIndex;
		public readonly int Count;
		public readonly KingdomBitTally UnitBits;

		public KingdomMaterialDebitSource(int Source, KingdomMaterialDebitSourceKind Kind,
			int KindIndex, int Count, KingdomBitTally UnitBits = null)
		{
			this.Source = Source;
			this.Kind = Kind;
			this.KindIndex = KindIndex;
			this.Count = Count;
			this.UnitBits = (UnitBits == null) ? new KingdomBitTally() : UnitBits.Copy();
		}
	}

	/// <summary>One allocation from one exclusive physical source.</summary>
	public sealed class KingdomMaterialDebitStep
	{
		public readonly int Source;
		public readonly KingdomMaterialDebitSourceKind Kind;
		public readonly int KindIndex;
		public readonly int Original;
		public readonly int Taken;
		public readonly KingdomBitTally UnitBits;

		public int Remaining => Original - Taken;

		public bool NeedsFinalization => Taken == Original;

		public KingdomMaterialDebitStep(int Source, KingdomMaterialDebitSourceKind Kind,
			int KindIndex, int Original, int Taken, KingdomBitTally UnitBits = null)
		{
			this.Source = Source;
			this.Kind = Kind;
			this.KindIndex = KindIndex;
			this.Original = Original;
			this.Taken = Taken;
			this.UnitBits = (UnitBits == null) ? new KingdomBitTally() : UnitBits.Copy();
		}
	}

	/// <summary>Read-only deterministic allocation of one composite price.</summary>
	public sealed class KingdomMaterialDebitPlan
	{
		public readonly KingdomMaterialDebitCost Requested;
		public readonly List<KingdomMaterialDebitStep> Steps;

		public KingdomMaterialDebitPlan(KingdomMaterialDebitCost Requested,
			List<KingdomMaterialDebitStep> Steps)
		{
			this.Requested = (Requested == null) ? new KingdomMaterialDebitCost() : Requested.Copy();
			this.Steps = (Steps == null)
				? new List<KingdomMaterialDebitStep>()
				: new List<KingdomMaterialDebitStep>(Steps);
		}
	}

	/// <summary>
	/// Immutable report a durable job can inspect. <see cref="Spent"/> is the part of the requested
	/// price actually answered; <see cref="Lost"/> is the full physical value removed, including
	/// surplus bits. <see cref="Outstanding"/> is safe to retry only when
	/// <see cref="MeasurementExact"/> is true; otherwise callback damage must be quarantined.
	/// </summary>
	public sealed class KingdomMaterialDebitResult
	{
		public readonly KingdomMaterialDebitOutcome Outcome;
		public readonly KingdomMaterialDebitFault Fault;
		public readonly KingdomMaterialDebitCost Requested;
		public readonly KingdomMaterialDebitCost Spent;
		public readonly KingdomMaterialDebitCost Outstanding;
		public readonly KingdomMaterialDebitCost Lost;
		public readonly int FinalizedSources;
		public readonly string Failure;
		public readonly bool MeasurementExact;

		public KingdomBitTally LostBitYield => Lost.Bits.Copy();

		public bool Exact => Outcome == KingdomMaterialDebitOutcome.ExactCommit;

		public bool Clean => Outcome == KingdomMaterialDebitOutcome.CleanRefusal
			|| Outcome == KingdomMaterialDebitOutcome.CompensatedExact
			|| Outcome == KingdomMaterialDebitOutcome.Cancelled;

		public bool Partial => Outcome == KingdomMaterialDebitOutcome.RecoverablePartial
			|| Outcome == KingdomMaterialDebitOutcome.IrreversiblePartial;

		internal KingdomMaterialDebitResult(KingdomMaterialDebitOutcome Outcome,
			KingdomMaterialDebitFault Fault, KingdomMaterialDebitCost Requested,
			KingdomMaterialDebitCost Spent, KingdomMaterialDebitCost Outstanding,
			KingdomMaterialDebitCost Lost, int FinalizedSources, string Failure,
			bool MeasurementExact = true)
		{
			this.Outcome = Outcome;
			this.Fault = Fault;
			this.Requested = (Requested == null) ? new KingdomMaterialDebitCost() : Requested.Copy();
			this.Spent = (Spent == null) ? new KingdomMaterialDebitCost() : Spent.Copy();
			this.Outstanding = (Outstanding == null) ? new KingdomMaterialDebitCost() : Outstanding.Copy();
			this.Lost = (Lost == null) ? new KingdomMaterialDebitCost() : Lost.Copy();
			this.FinalizedSources = (FinalizedSources > 0) ? FinalizedSources : 0;
			this.Failure = Failure;
			this.MeasurementExact = MeasurementExact;
		}
	}

	/// <summary>Pure planning, accounting and phase laws for the live material receipt.</summary>
	public static class KingdomMaterialDebitRules
	{
		public static bool TryPlan(KingdomMaterialDebitCost Cost,
			IList<KingdomMaterialDebitSource> Sources,
			out KingdomMaterialDebitPlan Plan,
			out KingdomMaterialDebitFault Fault)
		{
			Plan = null;
			Fault = KingdomMaterialDebitFault.None;
			if (Cost == null)
			{
				Fault = KingdomMaterialDebitFault.InvalidCost;
				return false;
			}
			if (Sources == null)
			{
				Fault = KingdomMaterialDebitFault.InvalidSources;
				return false;
			}

			List<KingdomMaterialDebitSource> unique = UniqueValidSources(Sources);
			List<KingdomMaterialDebitStep> steps = new List<KingdomMaterialDebitStep>();
			if (!PlanMaterials(Cost.Materials, unique, steps))
			{
				Fault = KingdomMaterialDebitFault.InsufficientMaterials;
				return false;
			}
			if (!PlanExotics(Cost.Exotics, unique, steps))
			{
				Fault = KingdomMaterialDebitFault.InsufficientExotics;
				return false;
			}
			if (!PlanBits(Cost.Bits, unique, steps))
			{
				Fault = KingdomMaterialDebitFault.InsufficientBits;
				return false;
			}
			Plan = new KingdomMaterialDebitPlan(Cost, steps);
			return true;
		}

		/// <summary>
		/// Computes exact requested credit and full physical loss from measured removals. The arrays
		/// are observations, not commands; malformed rows fail closed into an irreversible result.
		/// </summary>
		public static KingdomMaterialDebitResult Classify(
			KingdomMaterialDebitPlan Plan,
			IList<int> Removed,
			IList<bool> SameSurvivingSource,
			KingdomMaterialDebitFault Fault,
			string Failure)
		{
			List<bool> exact = new List<bool>();
			if (Plan != null && Removed != null && SameSurvivingSource != null &&
				Removed.Count == Plan.Steps.Count && SameSurvivingSource.Count == Plan.Steps.Count)
			{
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					exact.Add(SameSurvivingSource[i] || Removed[i] == Plan.Steps[i].Original);
				}
			}
			return Classify(Plan, Removed, SameSurvivingSource, exact, Fault, Failure);
		}

		/// <summary>Classifies persisted exact removals separately from any callback-damaged row.</summary>
		public static KingdomMaterialDebitResult Classify(
			KingdomMaterialDebitPlan Plan,
			IList<int> Removed,
			IList<bool> SameSurvivingSource,
			IList<bool> ExactObservation,
			KingdomMaterialDebitFault Fault,
			string Failure)
		{
			if (Plan == null || Removed == null || SameSurvivingSource == null
				|| ExactObservation == null || Removed.Count != Plan.Steps.Count
				|| SameSurvivingSource.Count != Plan.Steps.Count
				|| ExactObservation.Count != Plan.Steps.Count)
			{
				KingdomMaterialDebitCost empty = new KingdomMaterialDebitCost();
				return new KingdomMaterialDebitResult(KingdomMaterialDebitOutcome.InvalidReservation,
					KingdomMaterialDebitFault.InvalidSources, Plan?.Requested, empty,
					Plan?.Requested, empty, 0, Failure, false);
			}

			KingdomMaterialTally lostMaterials = new KingdomMaterialTally();
			KingdomBitTally lostBits = new KingdomBitTally();
			KingdomExoticTally lostExotics = new KingdomExoticTally();
			bool exact = true;
			bool measurementExact = true;
			bool any = false;
			bool recoverable = true;
			int finalized = 0;
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = Plan.Steps[i];
				int removed = Removed[i];
				if (!ExactObservation[i])
				{
					exact = false;
					measurementExact = false;
					recoverable = false;
				}
				if (removed < 0 || removed > step.Original)
				{
					removed = (removed < 0) ? 0 : step.Original;
					exact = false;
					measurementExact = false;
					recoverable = false;
				}
				exact &= removed == step.Taken;
				if (removed < step.Original && !SameSurvivingSource[i])
				{
					exact = false;
					measurementExact = false;
					recoverable = false;
				}
				if (removed <= 0)
				{
					continue;
				}
				any = true;
				if (removed == step.Original)
				{
					finalized++;
					recoverable = false;
				}
				AddLost(step, removed, lostMaterials, lostBits, lostExotics);
			}

			KingdomMaterialDebitCost requested = Plan.Requested;
			KingdomMaterialDebitCost lost = new KingdomMaterialDebitCost(lostMaterials, lostBits, lostExotics);
			KingdomMaterialDebitCost spent = Credit(requested, lost);
			KingdomMaterialDebitCost outstanding = Subtract(requested, spent);
			KingdomMaterialDebitOutcome outcome;
			if (exact && outstanding.IsEmpty)
			{
				outcome = KingdomMaterialDebitOutcome.ExactCommit;
				Fault = KingdomMaterialDebitFault.None;
				Failure = null;
			}
			else if (!any && measurementExact)
			{
				outcome = KingdomMaterialDebitOutcome.CleanRefusal;
			}
			else
			{
				outcome = any && recoverable
					? KingdomMaterialDebitOutcome.RecoverablePartial
					: KingdomMaterialDebitOutcome.IrreversiblePartial;
			}
			return new KingdomMaterialDebitResult(outcome, Fault, requested, spent,
				outstanding, lost, finalized, Failure, measurementExact);
		}

		public static bool CanCompensate(KingdomMaterialDebitPlan Plan,
			IList<int> Removed, IList<int> CurrentCounts, IList<bool> SameSurvivingSource)
		{
			if (Plan == null || Removed == null || CurrentCounts == null || SameSurvivingSource == null
				|| Removed.Count != Plan.Steps.Count || CurrentCounts.Count != Plan.Steps.Count
				|| SameSurvivingSource.Count != Plan.Steps.Count)
			{
				return false;
			}
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = Plan.Steps[i];
				int removed = Removed[i];
				if (removed < 0 || removed >= step.Original || !SameSurvivingSource[i]
					|| CurrentCounts[i] != step.Original - removed)
				{
					return false;
				}
			}
			return true;
		}

		internal static KingdomMaterialDebitResult EmptyResult(KingdomMaterialDebitOutcome Outcome,
			KingdomMaterialDebitFault Fault, KingdomMaterialDebitCost Requested, string Failure)
		{
			KingdomMaterialDebitCost request = Requested ?? new KingdomMaterialDebitCost();
			return new KingdomMaterialDebitResult(Outcome, Fault, request,
				new KingdomMaterialDebitCost(), request, new KingdomMaterialDebitCost(), 0, Failure,
				Outcome != KingdomMaterialDebitOutcome.InvalidReservation);
		}

		internal static KingdomMaterialDebitCost Credit(KingdomMaterialDebitCost Requested,
			KingdomMaterialDebitCost Lost)
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				materials.Set(kind, Math.Min(Requested.Materials.Get(kind), Lost.Materials.Get(kind)));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				bits.Set(i, Math.Min(Requested.Bits.Get(i), Lost.Bits.Get(i)));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				exotics.Set(kind, Math.Min(Requested.Exotics.Get(kind), Lost.Exotics.Get(kind)));
			}
			return new KingdomMaterialDebitCost(materials, bits, exotics);
		}

		internal static KingdomMaterialDebitCost Subtract(KingdomMaterialDebitCost Whole,
			KingdomMaterialDebitCost Part)
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial kind = (KingdomMaterial)i;
				materials.Set(kind, Whole.Materials.Get(kind) - Part.Materials.Get(kind));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				bits.Set(i, Whole.Bits.Get(i) - Part.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				KingdomExotic kind = (KingdomExotic)i;
				exotics.Set(kind, Whole.Exotics.Get(kind) - Part.Exotics.Get(kind));
			}
			return new KingdomMaterialDebitCost(materials, bits, exotics);
		}

		private static List<KingdomMaterialDebitSource> UniqueValidSources(
			IList<KingdomMaterialDebitSource> Sources)
		{
			List<KingdomMaterialDebitSource> unique = new List<KingdomMaterialDebitSource>();
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < Sources.Count; i++)
			{
				KingdomMaterialDebitSource source = Sources[i];
				if (source == null || source.Source < 0 || source.Count <= 0 || !seen.Add(source.Source))
				{
					continue;
				}
				if (!SourceShapeValid(source))
				{
					continue;
				}
				unique.Add(source);
			}
			return unique;
		}

		private static bool SourceShapeValid(KingdomMaterialDebitSource Source)
		{
			switch (Source.Kind)
			{
			case KingdomMaterialDebitSourceKind.Material:
				return Source.KindIndex >= 0 && Source.KindIndex < KingdomMaterialRules.MaterialCount;
			case KingdomMaterialDebitSourceKind.Exotic:
				return Source.KindIndex >= 0 && Source.KindIndex < KingdomMaterialRules.ExoticCount;
			case KingdomMaterialDebitSourceKind.BitStock:
				return !Source.UnitBits.IsEmpty();
			default:
				return false;
			}
		}

		private static bool PlanMaterials(KingdomMaterialTally Cost,
			List<KingdomMaterialDebitSource> Sources, List<KingdomMaterialDebitStep> Steps)
		{
			for (int kind = 0; kind < KingdomMaterialRules.MaterialCount; kind++)
			{
				int remaining = Cost.Get((KingdomMaterial)kind);
				for (int i = 0; i < Sources.Count && remaining > 0; i++)
				{
					KingdomMaterialDebitSource source = Sources[i];
					if (source.Kind != KingdomMaterialDebitSourceKind.Material || source.KindIndex != kind)
					{
						continue;
					}
					int take = Math.Min(source.Count, remaining);
					Steps.Add(new KingdomMaterialDebitStep(source.Source, source.Kind,
						source.KindIndex, source.Count, take));
					remaining -= take;
				}
				if (remaining > 0) return false;
			}
			return true;
		}

		private static bool PlanExotics(KingdomExoticTally Cost,
			List<KingdomMaterialDebitSource> Sources, List<KingdomMaterialDebitStep> Steps)
		{
			for (int kind = 0; kind < KingdomMaterialRules.ExoticCount; kind++)
			{
				int remaining = Cost.Get((KingdomExotic)kind);
				for (int i = 0; i < Sources.Count && remaining > 0; i++)
				{
					KingdomMaterialDebitSource source = Sources[i];
					if (source.Kind != KingdomMaterialDebitSourceKind.Exotic || source.KindIndex != kind)
					{
						continue;
					}
					int take = Math.Min(source.Count, remaining);
					Steps.Add(new KingdomMaterialDebitStep(source.Source, source.Kind,
						source.KindIndex, source.Count, take));
					remaining -= take;
				}
				if (remaining > 0) return false;
			}
			return true;
		}

		private static bool PlanBits(KingdomBitTally Cost,
			List<KingdomMaterialDebitSource> Sources, List<KingdomMaterialDebitStep> Steps)
		{
			KingdomBitTally owed = Cost.Copy();
			List<KingdomMaterialDebitSource> bits = new List<KingdomMaterialDebitSource>();
			for (int i = 0; i < Sources.Count; i++)
			{
				if (Sources[i].Kind == KingdomMaterialDebitSourceKind.BitStock)
				{
					bits.Add(Sources[i]);
				}
			}
			bits.Sort(delegate(KingdomMaterialDebitSource A, KingdomMaterialDebitSource B)
			{
				long a = BitWorth(A.UnitBits);
				long b = BitWorth(B.UnitBits);
				int compare = a.CompareTo(b);
				return (compare != 0) ? compare : A.Source.CompareTo(B.Source);
			});
			for (int i = 0; i < bits.Count && !owed.IsEmpty(); i++)
			{
				KingdomMaterialDebitSource source = bits[i];
				int take = UnitsWanted(owed, source.UnitBits, source.Count);
				if (take <= 0)
				{
					continue;
				}
				Steps.Add(new KingdomMaterialDebitStep(source.Source, source.Kind,
					0, source.Count, take, source.UnitBits));
				for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
				{
					long yielded = (long)source.UnitBits.Get(tier) * take;
					int reduction = (yielded >= owed.Get(tier)) ? owed.Get(tier) : (int)yielded;
					owed.Add(tier, -reduction);
				}
			}
			return owed.IsEmpty();
		}

		private static int UnitsWanted(KingdomBitTally Owed, KingdomBitTally Unit, int Available)
		{
			long wanted = 0L;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				int owed = Owed.Get(i);
				int per = Unit.Get(i);
				if (owed <= 0 || per <= 0)
				{
					continue;
				}
				long units = ((long)owed + per - 1L) / per;
				if (units > wanted) wanted = units;
			}
			if (wanted <= 0L) return 0;
			return (wanted >= Available) ? Available : (int)wanted;
		}

		private static long BitWorth(KingdomBitTally Worth)
		{
			long total = 0L;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				total += (long)Worth.Get(i) * (i + 1L);
			}
			return total;
		}

		private static void AddLost(KingdomMaterialDebitStep Step, int Removed,
			KingdomMaterialTally Materials, KingdomBitTally Bits, KingdomExoticTally Exotics)
		{
			switch (Step.Kind)
			{
			case KingdomMaterialDebitSourceKind.Material:
				Materials.Add((KingdomMaterial)Step.KindIndex, Removed);
				break;
			case KingdomMaterialDebitSourceKind.Exotic:
				Exotics.Add((KingdomExotic)Step.KindIndex, Removed);
				break;
			case KingdomMaterialDebitSourceKind.BitStock:
				for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				{
					long amount = (long)Step.UnitBits.Get(i) * Removed;
					Bits.Add(i, amount >= int.MaxValue ? int.MaxValue : (int)amount);
				}
				break;
			}
		}
	}
}
