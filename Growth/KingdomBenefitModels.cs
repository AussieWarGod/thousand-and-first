using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	[Flags]
	public enum KingdomBenefitCellUse : byte
	{
		None = 0,
		Plot = 1,
		Building = 2,
		Covered = 4,
		Interior = 8,
		Yard = 16,
		Network = 32,
		/// <summary>Verified public/inter-zone circulation seed. It grants no scope by itself.</summary>
		Ingress = 64
	}

	public enum KingdomBenefitScope : byte
	{
		Building = 1,
		Covered = 2,
		Interior = 3,
		Plot = 4,
		Yard = 5,
		Container = 6,
		Network = 7,
		Habitable = 8
	}

	/// <summary>Exact physical cover at one designated cell. Soft cover admits sky;
	/// walled, natural, and observed enclosures establish darkness above ground.</summary>
	public enum KingdomBenefitCover : byte
	{
		Open = 0,
		Soft = 1,
		Walled = 2,
		Natural = 3,
		ObservedEnclosure = 4
	}

	public enum KingdomBenefitOperation : byte
	{
		Present = 1,
		Staffed = 2,
		Powered = 3,
		Filled = 4,
		Sown = 5,
		Custom = 6
	}

	public enum KingdomBenefitFault : byte
	{
		None = 0,
		MalformedProvider = 1,
		MissingIdentity = 2,
		MissingDesignation = 3,
		OutsideDesignation = 4,
		AmbiguousDesignation = 5,
		ForeignCustody = 6,
		UnprovedReceipt = 7,
		WrongScope = 8,
		Inoperable = 9,
		ProviderCap = 10,
		SourceFault = 11,
		DuplicateIdentity = 12,
		StaleAssignment = 13,
		UnsupportedOperation = 14,
		WaterFoodCustody = 15,
		ObservationLimit = 16,
		UnacceptedBenefit = 17
	}

	public sealed class KingdomBenefitProviderDeclaration
	{
		public string Key;
		public string NetworkKey;
		public KingdomBenefitScope Scope;
		public KingdomBenefitOperation Operation;
		public List<KindAmount> Carries = new List<KindAmount>();
		public List<string> Provides = new List<string>();
	}

	public struct KingdomBenefitCell
	{
		public readonly int X;
		public readonly int Y;
		public readonly KingdomBenefitCellUse Use;
		public readonly KingdomBenefitCover Cover;
		public readonly string NetworkKey;

		public KingdomBenefitCell(int X, int Y, KingdomBenefitCellUse Use)
			: this(X, Y, Use,
				(Use & KingdomBenefitCellUse.Covered) != 0
					? KingdomBenefitCover.ObservedEnclosure : KingdomBenefitCover.Open, null)
		{
		}

		public KingdomBenefitCell(int X, int Y, KingdomBenefitCellUse Use, string NetworkKey)
			: this(X, Y, Use,
				(Use & KingdomBenefitCellUse.Covered) != 0
					? KingdomBenefitCover.ObservedEnclosure : KingdomBenefitCover.Open, NetworkKey)
		{
		}

		public KingdomBenefitCell(int X, int Y, KingdomBenefitCellUse Use,
			KingdomBenefitCover Cover, string NetworkKey = null)
		{
			this.X = X;
			this.Y = Y;
			this.Use = Use;
			this.Cover = Cover;
			this.NetworkKey = NetworkKey;
		}
	}

	/// <summary>Normalized exact-cell designation. Sources may be authored architecture,
	/// persisted TAF adoption, Hearthpyre homes, or another mod. The evaluator sees one shape.</summary>
	public sealed class KingdomBenefitDesignation
	{
		public string ProviderId;
		public string ProviderVersion;
		public string Identity;
		public string Revision;
		public string ZoneId;
		public string RootId;
		public string BuildingKey;
		public string LotId;
		public List<KindAmount> Caps = new List<KindAmount>();
		public List<string> AcceptedTags = new List<string>();
		public List<KingdomBenefitCell> Cells = new List<KingdomBenefitCell>();
	}

	public sealed class KingdomBenefitInspection
	{
		public string ProviderIdentity;
		public string ProviderKey;
		public string DesignationIdentity;
		public KingdomBenefitFault Fault;
		public string Detail;
		/// <summary>Current operating percentage after presence, staffing, power, fill,
		/// sowing, or a provider's custom predicate. Zero means no operating contribution.</summary>
		public int OperationPercent;
		/// <summary>True when otherwise eligible supply was refused because this exact
		/// designation did not accept it or its current cap was already full.</summary>
		public bool LimitedByDesignation;
		/// <summary>True when some otherwise live offer is not part of this building role's
		/// accepted amount/tag contract.</summary>
		public bool OutsideDesignationContract;
		/// <summary>True when some accepted live offer finds its amount cap or singleton quality
		/// already supplied.</summary>
		public bool SaturatedByDesignation;
		public List<KindAmount> Offered = new List<KindAmount>();
		public List<KindAmount> Credited = new List<KindAmount>();
		public List<string> Tags = new List<string>();
		public List<string> CreditedTags = new List<string>();
	}

	/// <summary>One building's effective physical result. Catalogue values are retained only as
	/// caps on the designation and never copied into these lists as supply.</summary>
	public sealed class KingdomBenefitReading
	{
		public KingdomBenefitDesignation Designation;
		public List<KindAmount> Carries = new List<KindAmount>();
		public List<string> Provides = new List<string>();
		public List<KingdomBenefitInspection> Providers =
			new List<KingdomBenefitInspection>();
	}
}
