using System;

namespace ThousandAndFirst
{

	/// <summary>
	/// Shared typed CAS witness. LastOperationId is the proof which disambiguates equal scalar
	/// values produced by different lanes. ActiveOperationId is a persisted exclusive lease.
	/// Rows are never evicted; hitting the bounded cap refuses new work.
	/// </summary>
	[Serializable]
	public sealed class KingdomLifecycleResourceRevision
	{
		public KingdomLifecycleResourceKind Kind;
		public string ScopeId;
		public string SubjectId;
		public string Key;
		public long Revision;
		public string ActiveOperationId;
		public string LastOperationId;
	}
}
