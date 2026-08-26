using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomRaidLedger
	{
		public const int CurrentVersion = 3;
		public int Version = CurrentVersion;
		public long StateRevision;
		public long ScheduleRevision;
		public List<KingdomRaidGrievance> Grievances = new List<KingdomRaidGrievance>();
		public List<KingdomRaidIncident> Incidents = new List<KingdomRaidIncident>();
		public string ActiveIncidentId;
		public bool LegacyEvidenceArchived;
		public int LegacyRaidState;
		public string LegacyFaction;
		public long LegacyDueTick;
		public long LegacyLastTick;
		public int LegacyTimesDeferred;
		// Version 2+ ledgers are framed. A newer framed body stays byte-exact and read-only so an
		// older TAF build can save unrelated realm state without destroying future raid authority.
		public byte[] OpaqueFuturePayload;
	}
}
