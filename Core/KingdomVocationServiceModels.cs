using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	/// <summary>Single non-yielding publication boundary supplied by active governance.</summary>
	internal interface IKingdomVocationServicePublication
	{
		bool TryPublish(Func<bool> publish);
	}

	public enum KingdomVocationServiceKind : byte
	{
		None = 0,
		RouteBrief = 1,
		SanctuaryTitle = 2,
		ProvenanceReading = 3
	}

	/// <summary>The exact authority behind one bounded D12 service.</summary>
	public enum KingdomVocationServiceAuthority : byte
	{
		None = 0,
		PolityRoute = 1,
		BuiltShelter = 2,
		ArtifactRecognition = 3
	}

	[Serializable]
	public sealed class KingdomVocationServiceRequest
	{
		public string SettlementId;
		public string Vocation;
		public string SourceReceiptId;
		public string SourceDescription;
		public string ResultText;
		public string SinkReceiptId;
		public KingdomVocationServiceKind Kind;
		public int InputUnits;
		public long CadenceOrdinal;
		public long RequestedTick;
		public string Digest;
	}

	[Serializable]
	public sealed class KingdomVocationServiceReceipt
	{
		public const int LegacyVersion = 1;
		public const int PriorVersion = 2;
		public const int CurrentVersion = 3;
		public int Version = CurrentVersion;
		public string ServiceId;
		public KingdomVocationServiceRequest Request;
		public string Verb;
		public string OutputText;
		public int OutputUnits;
		public long CompletedTick;
	}

	[Serializable]
	public sealed class KingdomVocationServiceBook
	{
		public long Revision;
		public List<KingdomVocationServiceReceipt> Rows =
			new List<KingdomVocationServiceReceipt>();
	}

	/// <summary>Whether a D12 report can honestly offer explicit service.</summary>
	public enum KingdomVocationServiceOfferState : byte
	{
		Unavailable = 0,
		Available = 1,
		Neutral = 2
	}

	/// <summary>Immutable exact source fact created only by its owning runtime.</summary>
	internal sealed class KingdomVocationServiceSource
	{
		internal string SettlementId { get; }
		internal string Vocation { get; }
		internal KingdomVocationServiceKind Kind { get; }
		internal KingdomVocationServiceAuthority Authority { get; }
		internal string ReceiptId { get; }
		internal string Description { get; }
		internal string ResultText { get; }

		internal KingdomVocationServiceSource(string settlementId, string vocation,
			KingdomVocationServiceKind kind, KingdomVocationServiceAuthority authority,
			string receiptId, string description, string resultText)
		{
			SettlementId = settlementId;
			Vocation = vocation;
			Kind = kind;
			Authority = authority;
			ReceiptId = receiptId;
			Description = description;
			ResultText = resultText;
		}
	}

	/// <summary>
	/// D12 presentation. Zero units means no item, water, stat, or value is promised.
	/// </summary>
	public sealed class KingdomVocationServiceOffer
	{
		public KingdomVocationServiceOfferState State { get; }
		public string SettlementId { get; }
		public string Vocation { get; }
		public KingdomVocationServiceKind Kind { get; }
		public KingdomVocationServiceAuthority Authority { get; }
		public string Verb { get; }
		public string SourceAuthority { get; }
		public string SourceReceiptId { get; }
		public string SourceDescription { get; }
		internal string ResultText { get; }
		public string Sink { get; }
		public string Cadence { get; }
		public string Closure { get; }
		public string Report { get; }
		public int InputUnits => 0;
		public int OutputUnits => 0;
		public bool MutatesSource => false;
		public string UnavailableCause { get; }
		public string Remedy { get; }

		internal KingdomVocationServiceOffer(KingdomVocationServiceOfferState state,
			string settlementId, string vocation, KingdomVocationServiceKind kind,
			KingdomVocationServiceAuthority authority, string verb,
			string sourceAuthority, string sourceReceiptId, string sourceDescription,
			string resultText, string sink, string cadence, string closure, string report,
			string unavailableCause, string remedy)
		{
			State = state;
			SettlementId = settlementId;
			Vocation = vocation;
			Kind = kind;
			Authority = authority;
			Verb = verb;
			SourceAuthority = sourceAuthority;
			SourceReceiptId = sourceReceiptId;
			SourceDescription = sourceDescription;
			ResultText = resultText;
			Sink = sink;
			Cadence = cadence;
			Closure = closure;
			Report = report;
			UnavailableCause = unavailableCause;
			Remedy = remedy;
		}
	}

	/// <summary>Current C18 disposition of one exact available vocation source.</summary>
	public enum KingdomVocationServiceActionState : byte
	{
		Available = 0,
		AlreadyRecorded = 1,
		CapacityClosed = 2
	}

	/// <summary>Immutable pre-choice capacity and retry disclosure.</summary>
	public sealed class KingdomVocationServiceStatus
	{
		public KingdomVocationServiceActionState State { get; }
		public int SeriesCount { get; }
		public int RealmCount { get; }
		public string ExistingReceiptText { get; }

		internal KingdomVocationServiceStatus(KingdomVocationServiceActionState state,
			int seriesCount, int realmCount, string existingReceiptText)
		{
			State = state;
			SeriesCount = seriesCount;
			RealmCount = realmCount;
			ExistingReceiptText = existingReceiptText;
		}
	}

}
