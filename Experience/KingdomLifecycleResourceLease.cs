using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomLifecycleResourceLease
	{
		public string OperationId;
		public KingdomLifecycleResourceKind Kind;
		public string ScopeId;
		public string SubjectId;
		public string Key;
		public long Before;
		public long Delta;
		public long After;
		public long BeforeRevision;
		public long AfterRevision;
		public KingdomLifecycleLeaseState State;
	}
}
