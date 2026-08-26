using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>Frozen durable sidecar state. Every mutation in <see cref="KingdomBehaviourRules"/>
	/// returns another instance; failed rows leave this instance untouched.</summary>
	internal sealed class KingdomBehaviourState
	{
		private readonly KingdomResourceReading[] resources;
		private readonly KingdomBehaviourJobRow[] jobs;
		private readonly KingdomExtensionNetworkReading[] networks;
		private readonly KingdomWorkBehaviourReading[] works;

		internal static readonly KingdomBehaviourState Empty = new KingdomBehaviourState(null, null, null, null);

		internal KingdomBehaviourState(KingdomResourceReading[] resources,
			KingdomBehaviourJobRow[] jobs, KingdomExtensionNetworkReading[] networks,
			KingdomWorkBehaviourReading[] works)
		{
			this.resources = Copy(resources);
			this.jobs = Copy(jobs);
			this.networks = Copy(networks);
			this.works = Copy(works);
		}

		internal int ResourceCount { get { return resources.Length; } }
		internal int JobCount { get { return jobs.Length; } }
		internal int NetworkCount { get { return networks.Length; } }
		internal int WorkCount { get { return works.Length; } }

		internal bool TryResource(int index, out KingdomResourceReading row)
		{
			row = default(KingdomResourceReading);
			if (index < 0 || index >= resources.Length) return false;
			row = resources[index]; return true;
		}

		internal bool TryJob(int index, out KingdomBehaviourJobRow row)
		{
			row = null;
			if (index < 0 || index >= jobs.Length) return false;
			row = jobs[index]; return true;
		}

		internal bool TryNetwork(int index, out KingdomExtensionNetworkReading row)
		{
			row = default(KingdomExtensionNetworkReading);
			if (index < 0 || index >= networks.Length) return false;
			row = networks[index]; return true;
		}

		internal bool TryWork(int index, out KingdomWorkBehaviourReading row)
		{
			row = default(KingdomWorkBehaviourReading);
			if (index < 0 || index >= works.Length) return false;
			row = works[index]; return true;
		}

		internal KingdomResourceReading[] Resources() { return Copy(resources); }
		internal KingdomBehaviourJobRow[] Jobs() { return Copy(jobs); }
		internal KingdomExtensionNetworkReading[] Networks() { return Copy(networks); }
		internal KingdomWorkBehaviourReading[] Works() { return Copy(works); }

		internal KingdomBehaviourReading Reading()
		{
			KingdomExtensionJobReading[] jobReadings = new KingdomExtensionJobReading[jobs.Length];
			for (int i = 0; i < jobs.Length; i++) jobReadings[i] = jobs[i].Reading();
			return new KingdomBehaviourReading(resources, jobReadings, networks, works);
		}

		private static T[] Copy<T>(T[] source)
		{
			if (source == null || source.Length == 0) return new T[0];
			T[] copy = new T[source.Length]; Array.Copy(source, copy, source.Length); return copy;
		}
	}
}
