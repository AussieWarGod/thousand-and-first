using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Frozen sidecar projection shared by resource, job/carrier, network and work lanes.
	/// Arrays are copied at construction and exposed only through bounded <c>Try*</c> methods.</summary>
	public sealed class KingdomBehaviourReading
	{
		private readonly KingdomResourceReading[] resources;
		private readonly KingdomExtensionJobReading[] jobs;
		private readonly KingdomExtensionNetworkReading[] networks;
		private readonly KingdomWorkBehaviourReading[] works;

		/// <summary>Builds a frozen sidecar projection. Null arrays become empty.</summary>
		public KingdomBehaviourReading(KingdomResourceReading[] Resources,
			KingdomExtensionJobReading[] Jobs, KingdomExtensionNetworkReading[] Networks,
			KingdomWorkBehaviourReading[] Works)
		{
			resources = Copy(Resources);
			jobs = Copy(Jobs);
			networks = Copy(Networks);
			works = Copy(Works);
		}

		/// <summary>Resource row count.</summary>
		public int ResourceCount { get { return resources.Length; } }
		/// <summary>Job row count, including bounded terminal receipts.</summary>
		public int JobCount { get { return jobs.Length; } }
		/// <summary>Network-state row count.</summary>
		public int NetworkCount { get { return networks.Length; } }
		/// <summary>Work-behaviour row count.</summary>
		public int WorkCount { get { return works.Length; } }

		/// <summary>Reads one resource; false out of range.</summary>
		public bool TryResource(int Index, out KingdomResourceReading Resource)
		{
			Resource = default(KingdomResourceReading);
			if (Index < 0 || Index >= resources.Length) return false;
			Resource = resources[Index]; return true;
		}

		/// <summary>Reads one job; false out of range.</summary>
		public bool TryJob(int Index, out KingdomExtensionJobReading Job)
		{
			Job = default(KingdomExtensionJobReading);
			if (Index < 0 || Index >= jobs.Length) return false;
			Job = jobs[Index]; return true;
		}

		/// <summary>Reads one network; false out of range.</summary>
		public bool TryNetwork(int Index, out KingdomExtensionNetworkReading Network)
		{
			Network = default(KingdomExtensionNetworkReading);
			if (Index < 0 || Index >= networks.Length) return false;
			Network = networks[Index]; return true;
		}

		/// <summary>Reads one work state; false out of range.</summary>
		public bool TryWork(int Index, out KingdomWorkBehaviourReading Work)
		{
			Work = default(KingdomWorkBehaviourReading);
			if (Index < 0 || Index >= works.Length) return false;
			Work = works[Index]; return true;
		}

		private static T[] Copy<T>(T[] source)
		{
			if (source == null || source.Length == 0) return new T[0];
			T[] copy = new T[source.Length]; Array.Copy(source, copy, source.Length); return copy;
		}
	}
}
