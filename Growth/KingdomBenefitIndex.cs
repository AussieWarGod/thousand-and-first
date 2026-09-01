using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>One active-zone, one-survey reading of physical providers. Catalogue values
	/// enter only through designation caps.</summary>
	public sealed partial class KingdomBenefitIndex
	{
		private sealed class Aggregate
		{
			internal KingdomBenefitReading Reading;
			internal readonly Dictionary<string, int> Amounts =
				new Dictionary<string, int>(StringComparer.Ordinal);
			internal readonly List<string> Tags = new List<string>();
			internal GameObject Root;
			internal Zone InitialRootZone;
			internal int InitialRootX;
			internal int InitialRootY;
			internal bool ShellRead;
			internal bool ShellValid;
			internal bool AccessRead;
			internal HashSet<long> Reachable;
			internal readonly List<ProviderEvaluation> Pending =
				new List<ProviderEvaluation>();
		}

		private readonly List<KingdomBenefitReading> Rows = new List<KingdomBenefitReading>();
		private readonly List<KingdomBenefitInspection> AllInspections =
			new List<KingdomBenefitInspection>();
		private readonly List<KingdomBenefitInspectionOrderRow> InspectionOrderRows =
			new List<KingdomBenefitInspectionOrderRow>();
		private readonly Dictionary<string, Aggregate> ByIdentity =
			new Dictionary<string, Aggregate>(StringComparer.Ordinal);
		private readonly Dictionary<string, Aggregate> ByRoot =
			new Dictionary<string, Aggregate>(StringComparer.Ordinal);
		private readonly List<ProviderObjectBatch> AdmittedProviderBatches =
			new List<ProviderObjectBatch>();

		public IReadOnlyList<KingdomBenefitReading> Readings
		{
			get
			{
				List<KingdomBenefitReading> copy = new List<KingdomBenefitReading>();
				for (int i = 0; i < Rows.Count; i++) copy.Add(Copy(Rows[i]));
				return copy.AsReadOnly();
			}
		}

		public IReadOnlyList<KingdomBenefitInspection> Inspections
		{
			get
			{
				List<KingdomBenefitInspection> copy = new List<KingdomBenefitInspection>();
				for (int i = 0; i < AllInspections.Count; i++)
					copy.Add(Copy(AllInspections[i]));
				return copy.AsReadOnly();
			}
		}

		public int Amount(string DesignationIdentity, string Kind)
		{
			if (!ByIdentity.TryGetValue(DesignationIdentity ?? "", out Aggregate row)) return 0;
			string kind = Fold(Kind);
			for (int i = 0; i < row.Reading.Carries.Count; i++)
				if (row.Reading.Carries[i].Kind == kind) return row.Reading.Carries[i].Amount;
			return 0;
		}

		public int Total(string Kind)
		{
			string kind = Fold(Kind); int total = 0;
			for (int i = 0; i < Rows.Count; i++)
				for (int c = 0; c < Rows[i].Carries.Count; c++)
					if (Rows[i].Carries[c].Kind == kind)
						total = SaturatingAdd(total, Rows[i].Carries[c].Amount);
			return total;
		}

		public string[] Tags(string DesignationIdentity)
		{
			if (!ByIdentity.TryGetValue(DesignationIdentity ?? "", out Aggregate row))
				return new string[0];
			return row.Reading.Provides.ToArray();
		}

		/// <summary>Returns one root's effective amount. Root identity is the exact current
		/// designation root, not a catalogue key or plot rectangle.</summary>
		public int AmountForRoot(string RootId, string Kind)
		{
			if (!ByRoot.TryGetValue(RootId ?? "", out Aggregate row)) return 0;
			string kind = Fold(Kind);
			for (int i = 0; i < row.Reading.Carries.Count; i++)
				if (row.Reading.Carries[i].Kind == kind) return row.Reading.Carries[i].Amount;
			return 0;
		}

		public string[] TagsForRoot(string RootId)
		{
			return ByRoot.TryGetValue(RootId ?? "", out Aggregate row)
				? row.Reading.Provides.ToArray() : new string[0];
		}

		public KingdomBenefitReading ReadingForRoot(string RootId)
		{
			return ByRoot.TryGetValue(RootId ?? "", out Aggregate row)
				? Copy(row.Reading) : null;
		}

		internal KingdomBenefitReading ExactReading(string Identity)
		{
			return ByIdentity.TryGetValue(Identity ?? "", out Aggregate row) ? row.Reading : null;
		}

		private void Initialize(KingdomDesignationIndex Designations, Zone Z)
		{
			for (int i = 0; i < Designations.ExactDesignations.Count; i++)
			{
				KingdomBenefitDesignation designation = Designations.ExactDesignations[i];
				Aggregate aggregate = new Aggregate { Reading = new KingdomBenefitReading {
					Designation = designation } };
				Designations.TryExactRoot(Z, designation, out aggregate.Root);
				Cell rootCell = aggregate.Root?.CurrentCell;
				aggregate.InitialRootZone = rootCell?.ParentZone;
				aggregate.InitialRootX = rootCell?.X ?? -1;
				aggregate.InitialRootY = rootCell?.Y ?? -1;
				ByIdentity.Add(designation.Identity, aggregate);
				ByRoot.Add(designation.RootId, aggregate);
				Rows.Add(aggregate.Reading);
			}
		}

		private void FinalizeRows()
		{
			foreach (KeyValuePair<string, Aggregate> pair in ByIdentity)
			{
				List<KindAmount> physical = new List<KindAmount>();
				foreach (KeyValuePair<string, int> amount in pair.Value.Amounts)
					physical.Add(new KindAmount(amount.Key, amount.Value));
				pair.Value.Reading.Carries = KingdomBenefitEmbodimentRules.Clamp(
					pair.Value.Reading.Designation.Caps, physical);
				pair.Value.Reading.Provides = new List<string>(
					KingdomBenefitEmbodimentRules.AcceptedTags(
						pair.Value.Reading.Designation.AcceptedTags, pair.Value.Tags));
			}
			Rows.Sort((a, b) => string.CompareOrdinal(
				a.Designation.Identity, b.Designation.Identity));
		}

		private static string Fold(string Value) => (Value ?? "").Trim().ToLowerInvariant();

		private static KingdomBenefitInspection Copy(KingdomBenefitInspection Source)
		{
			KingdomBenefitInspection copy = new KingdomBenefitInspection {
				ProviderIdentity = Source.ProviderIdentity, ProviderKey = Source.ProviderKey,
				DesignationIdentity = Source.DesignationIdentity, Fault = Source.Fault,
				Detail = Source.Detail, OperationPercent = Source.OperationPercent,
				LimitedByDesignation = Source.LimitedByDesignation,
				OutsideDesignationContract = Source.OutsideDesignationContract,
				SaturatedByDesignation = Source.SaturatedByDesignation };
			copy.Offered.AddRange(Source.Offered); copy.Credited.AddRange(Source.Credited);
			copy.Tags.AddRange(Source.Tags); copy.CreditedTags.AddRange(Source.CreditedTags);
			return copy;
		}

		private static KingdomBenefitReading Copy(KingdomBenefitReading Source)
		{
			KingdomBenefitReading copy = new KingdomBenefitReading {
				Designation = KingdomDesignationRules.Copy(Source.Designation) };
			copy.Carries.AddRange(Source.Carries); copy.Provides.AddRange(Source.Provides);
			for (int i = 0; i < Source.Providers.Count; i++) copy.Providers.Add(Copy(Source.Providers[i]));
			return copy;
		}
		private static int SaturatingAdd(int A, int B)
		{
			long value = (long)A + B;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}
	}
}
