using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public enum KingdomPolityCasOutcome : byte
	{
		Applied = 0,
		AlreadyApplied = 1,
		Conflict = 2,
		Refused = 3
	}

	[Serializable]
	public sealed class KingdomPolityPublicationResult
	{
		public KingdomPolityCasOutcome Outcome;
		public long SourceRevision;
		public long CommittedRevision;
		public string CurrentPolityId;
		public string ImportedPolityId;
		public string ProjectionId;
	}

	[Serializable]
	public sealed class KingdomPolityFactionProjectionView
	{
		public string PolityId;
		public string FactionId;
		public string ProjectionId;
		public string AppliedDigest;
		public KingdomPolityProjectionPhase Phase;
		public KingdomPolityLifecycle Lifecycle;
	}

	/// <summary>Bounded live-realm facts admitted by the one foundation publication.</summary>
	[Serializable]
	public sealed class KingdomPolityFoundationFacts
	{
		public string RealmId;
		public string FactionId;
		public string DisplayName;
		public string FounderName;
		public string SettlementId;
		public string Vocation;
		public string Style;
		public string Creed;
		public int Stage;
		/// <summary>Exact craft-derived equipment band; never inferred from growth stage.</summary>
		public int TechnologyBand;
		public int Population;
		public long FoundedTick;
		public List<string> OriginKeys = new List<string>();
		public List<string> CultureKeys = new List<string>();
		public List<string> SpeciesKeys = new List<string>();
		public List<string> IdentityKeys = new List<string>();
	}

	/// <summary>
	/// Safe copied facts from one committed promoted seal. Old realm, faction, settlement, game,
	/// and actor identifiers are deliberately absent; every new authority id is minted afresh.
	/// </summary>
	[Serializable]
	public sealed class KingdomPolityLegacySnapshot
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>
		/// Zero identifies a preserved pre-profile-provenance snapshot. Such a snapshot may still
		/// be imported as institutional history, but cannot manifest a guessed prior population.
		/// </summary>
		public int ProfileSchema;
		/// <summary>Exact technology from the committed source profile when ProfileSchema is current.</summary>
		public int TechnologyBand;
		/// <summary>Canonical resolver body keys only; never actor, object, or inventory identities.</summary>
		public List<string> CanonicalBodyKeys = new List<string>();
		/// <summary>Digest of projected phenotype only; no source authority identity is hashed.</summary>
		public string SourceProfileDigest;
		/// <summary>Self-commitment over the bounded canonical profile fields.</summary>
		public string ProfileProvenanceDigest;
		public string LegacyToken;
		public string LineageToken;
		public string FounderName;
		public string RealmName;
		public string SettlementName;
		public string Vocation;
		public string Style;
		public int Stage;
		public int Population;
		public int Defence;
		public int StoredWater;
		public int InheritedState;
		public List<string> RollNames = new List<string>();
		public List<string> OriginKeys = new List<string>();
		public List<int> OriginCounts = new List<int>();
		public List<string> CreedKeys = new List<string>();
		public List<int> CreedCounts = new List<int>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;
		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomPolityLegacySnapshot));
		}
		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomPolityLegacySnapshot)); Normalize();
		}
#endif

		public void Normalize()
		{
			CanonicalBodyKeys = CanonicalBodyKeys ?? new List<string>();
			RollNames = RollNames ?? new List<string>();
			OriginKeys = OriginKeys ?? new List<string>();
			OriginCounts = OriginCounts ?? new List<int>();
			CreedKeys = CreedKeys ?? new List<string>();
			CreedCounts = CreedCounts ?? new List<int>();
		}

		internal KingdomPolityLegacySnapshot Copy()
		{
			return new KingdomPolityLegacySnapshot
			{
				ProfileSchema = ProfileSchema, TechnologyBand = TechnologyBand,
				CanonicalBodyKeys = new List<string>(CanonicalBodyKeys),
				SourceProfileDigest = SourceProfileDigest,
				ProfileProvenanceDigest = ProfileProvenanceDigest,
				LegacyToken = LegacyToken, LineageToken = LineageToken,
				FounderName = FounderName, RealmName = RealmName,
				SettlementName = SettlementName, Vocation = Vocation, Style = Style,
				Stage = Stage, Population = Population, Defence = Defence,
				StoredWater = StoredWater, InheritedState = InheritedState,
				RollNames = new List<string>(RollNames), OriginKeys = new List<string>(OriginKeys),
				OriginCounts = new List<int>(OriginCounts), CreedKeys = new List<string>(CreedKeys),
				CreedCounts = new List<int>(CreedCounts)
			};
		}
	}

	/// <summary>Exact old authority plus safe legacy facts admitted at exile.</summary>
	[Serializable]
	public sealed class KingdomPolityRealmExileFacts
	{
		public string RealmId;
		public string FactionId;
		public long ClosedTick;
		public KingdomPolityLegacySnapshot Legacy;
	}

	/// <summary>
	/// Crash receipt for exile, rollback return, or refounding. Old ledger bytes are rollback-only
	/// escrow and are destroyed after refounding; <see cref="Legacy"/> cannot carry runtime ids.
	/// </summary>
	[Serializable]
	public sealed class KingdomPolityRealmTransition
#if !TAF_TESTS
		: IComposite
#endif
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public KingdomPolityRealmTransitionPhase Phase;
		public long Revision;
		public string TransitionId;
		public string CauseRef;
		public string OldRealmId;
		public string OldCurrentPolityId;
		public string OldCurrentFactionId;
		public string OldCurrentProjectionId;
		public string OldCurrentProjectionDigest;
		public string OldImportedPolityId;
		public string OldImportedFactionId;
		public string OldImportedProjectionId;
		public string OldImportedProjectionDigest;
		public bool OldImportedWasVisible;
		public long ClosedTick;
		public long SourceRevision;
		public long RetiredRevision;
		public long DetachedRevision;
		public long ReboundRevision;
		public string ReturnLedgerDigest;
		public string RetiredLedgerDigest;
		public byte[] ReturnLedgerEnvelope;
		public KingdomPolityLegacySnapshot Legacy;
		public string ReboundRealmId;
		public string ReboundPolityId;
		public string ReboundFactionId;
		public string Fault;

#if !TAF_TESTS
		public bool WantFieldReflection => false;
		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomPolityRealmTransition));
		}
		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomPolityRealmTransition)); Normalize();
		}
#endif

		public void Normalize()
		{
			if (Phase != KingdomPolityRealmTransitionPhase.None) Legacy?.Normalize();
			if (Phase == KingdomPolityRealmTransitionPhase.None) CopyFrom(new KingdomPolityRealmTransition());
		}

		internal void CopyFrom(KingdomPolityRealmTransition S)
		{
			Version = S.Version; Phase = S.Phase; Revision = S.Revision;
			TransitionId = S.TransitionId; CauseRef = S.CauseRef; OldRealmId = S.OldRealmId;
			OldCurrentPolityId = S.OldCurrentPolityId; OldCurrentFactionId = S.OldCurrentFactionId;
			OldCurrentProjectionId = S.OldCurrentProjectionId;
			OldCurrentProjectionDigest = S.OldCurrentProjectionDigest;
			OldImportedPolityId = S.OldImportedPolityId; OldImportedFactionId = S.OldImportedFactionId;
			OldImportedProjectionId = S.OldImportedProjectionId;
			OldImportedProjectionDigest = S.OldImportedProjectionDigest;
			OldImportedWasVisible = S.OldImportedWasVisible; ClosedTick = S.ClosedTick;
			SourceRevision = S.SourceRevision; RetiredRevision = S.RetiredRevision;
			DetachedRevision = S.DetachedRevision; ReboundRevision = S.ReboundRevision;
			ReturnLedgerDigest = S.ReturnLedgerDigest; RetiredLedgerDigest = S.RetiredLedgerDigest;
			ReturnLedgerEnvelope = S.ReturnLedgerEnvelope == null ? null :
				(byte[])S.ReturnLedgerEnvelope.Clone(); Legacy = S.Legacy?.Copy();
			ReboundRealmId = S.ReboundRealmId; ReboundPolityId = S.ReboundPolityId;
			ReboundFactionId = S.ReboundFactionId; Fault = S.Fault;
		}
	}
}
