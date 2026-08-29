using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Treaty
{
	public enum PactPhase : byte { None, Proposed, Ratified, Active, Fulfilled, Breached, Dissolved }
	public enum PactWitnessStatus : byte
	{ None, Projected, WitnessLost, WitnessStolen, WitnessDamaged, DuplicateInert }
	public enum PactWitnessEventKind : byte
	{ WitnessLost=1, WitnessStolen=2, WitnessDamaged=3, DuplicateInert=4, DeliberateBreach=5 }
	public enum KingdomTreatyStoreState : byte { Healthy=0, Quarantined=1, FutureOpaque=2 }

	[Serializable] public sealed class PactWitnessEvent
	{
		public string EventId, PactId, ProjectionId, ActorId, Cause;
		public PactWitnessEventKind Kind; public long Tick;
	}
	[Serializable] public sealed class KingdomTreatyRecord
	{
		public const int CurrentVersion=1; public int Version=1;
		public string PactId, PartyA, PartyB, RitualLiquid, SignatureA, SignatureB;
		public PactPhase Phase; public readonly List<string> Clauses=new List<string>();
		public readonly List<string> Obligations=new List<string>(), Favors=new List<string>();
		public long ProposedTick, StartTick=-1, ExpiryTick=-1, Revision;
		public string ProjectionId, ProjectionLocator, BreachActorId, BreachCause;
		public PactWitnessStatus WitnessStatus; public bool EffectsSuspended;
		public readonly List<string> AppliedEffectIds=new List<string>();
		public readonly List<PactWitnessEvent> WitnessEvents=new List<PactWitnessEvent>();
	}
	[Serializable] public sealed class KingdomTreatyLedger
	{
		public const int MaxPacts=16; public long Revision; public bool Quarantined;
		public KingdomTreatyStoreState StoreState;
		public string Fault; public byte[] OpaquePayload;
		public readonly List<KingdomTreatyRecord> Pacts=new List<KingdomTreatyRecord>();
	}
}
