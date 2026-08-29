using System;
using System.Collections.Generic;
using System.IO;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// Realm-scoped semantic polity authority. Runtime factions, actors, dialogue, map marks, and
	/// encounter objects are projections with separate receipts; none is accepted as polity truth.
	/// </summary>
	[Serializable]
	public sealed class KingdomPolityLedger
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomPolityRules.CurrentFormatVersion;
		public KingdomPolitySchemaState SchemaState = KingdomPolitySchemaState.Compatible;
		public string SchemaFault;
		public int MigratedFromVersion;
		public string RealmId;
		public bool IdentityBound;
		public long Revision;
		public KingdomPolityOptions Options = new KingdomPolityOptions();
		public List<KingdomPolityRecord> Polities = new List<KingdomPolityRecord>();
		public List<KingdomPolityRelation> Relations = new List<KingdomPolityRelation>();
		public List<KingdomPolityProfileRevision> Profiles =
			new List<KingdomPolityProfileRevision>();
		public List<KingdomPolityRouteRecord> Routes = new List<KingdomPolityRouteRecord>();
		public List<KingdomPolityGrievanceRecord> Grievances =
			new List<KingdomPolityGrievanceRecord>();
		public List<KingdomPolityFrontRecord> Fronts = new List<KingdomPolityFrontRecord>();
		public List<KingdomPolityCohortPlan> Cohorts = new List<KingdomPolityCohortPlan>();
		public List<KingdomPolityNamedFigureRecord> NamedFigures =
			new List<KingdomPolityNamedFigureRecord>();
		public List<KingdomPolityIncidentRecord> Incidents =
			new List<KingdomPolityIncidentRecord>();
		public List<KingdomPolityProjectionReceipt> Projections =
			new List<KingdomPolityProjectionReceipt>();
		public long FoldedCompactionCount;
		public string FoldedCompactionDigest;
		public List<KingdomPolityCompactionReceipt> Compactions =
			new List<KingdomPolityCompactionReceipt>();
		public int OpaqueWireVersion;
		public byte[] OpaqueFuturePayload;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			byte[] envelope = KingdomPolityCodec.EncodeEnvelope(this);
			Writer.Write(envelope.Length);
			Writer.Write(envelope, 0, envelope.Length);
		}

		public void Read(SerializationReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > KingdomPolityCodec.MaxEnvelopeBytes)
				throw new InvalidDataException("Polity envelope length exceeds hard bound.");
			byte[] envelope = Reader.ReadBytesDirect(length);
			if (envelope.Length != length) throw new EndOfStreamException("Truncated polity envelope.");
			CopyFrom(KingdomPolityCodec.DecodeEnvelopeRaw(envelope));
		}
#endif

		internal void CopyFrom(KingdomPolityLedger Source)
		{
			if (Source == null) throw new ArgumentNullException(nameof(Source));
			FormatVersion = Source.FormatVersion; SchemaState = Source.SchemaState;
			SchemaFault = Source.SchemaFault; MigratedFromVersion = Source.MigratedFromVersion;
			RealmId = Source.RealmId; IdentityBound = Source.IdentityBound; Revision = Source.Revision;
			Options = Source.Options; Polities = Source.Polities; Relations = Source.Relations;
			Profiles = Source.Profiles; Routes = Source.Routes; Grievances = Source.Grievances;
			Fronts = Source.Fronts; Cohorts = Source.Cohorts; NamedFigures = Source.NamedFigures;
			Incidents = Source.Incidents; Projections = Source.Projections;
			FoldedCompactionCount = Source.FoldedCompactionCount;
			FoldedCompactionDigest = Source.FoldedCompactionDigest;
			Compactions = Source.Compactions; OpaqueWireVersion = Source.OpaqueWireVersion;
			OpaqueFuturePayload = Source.OpaqueFuturePayload;
		}
	}

	public static partial class KingdomPolityRules { }
	public static partial class KingdomPolityCodec { }
}
