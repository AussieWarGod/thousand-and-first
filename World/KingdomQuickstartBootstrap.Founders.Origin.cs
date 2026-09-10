using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The GameObject side of one founder's origin accounting: the only place in the quickstart
	/// that knows both what a body is and what a settlement tally is.
	/// <para>
	/// Everything the transaction decides lives above this file, in
	/// <see cref="KingdomFounderOriginEngine"/>, which cannot name an engine type at all. This is
	/// the adapter, and it is deliberately dull: two property reads, two property writes, one
	/// dictionary read and one dictionary write, none of which dispatch.
	/// </para>
	/// </summary>
	public static partial class KingdomQuickstartBootstrap
	{
		private sealed class FounderOriginHost : IKingdomFounderOriginHost
		{
			private readonly GameObject Body;
			private readonly Dictionary<string, int> Tally;

			internal FounderOriginHost(GameObject Body, Dictionary<string, int> Tally,
				string CityId)
			{
				this.Body = Body;
				this.Tally = Tally;
				this.CityId = CityId ?? "";
			}

			public string BodyId
			{
				get { return Body == null ? "" : (Body.IDIfAssigned ?? ""); }
			}

			public string CityId { get; private set; }

			public bool HasReceipt()
			{
				return Body != null
					&& Body.HasStringProperty(KingdomFounderOriginCodec.ReceiptProperty);
			}

			public string RawReceipt()
			{
				return Body == null ? null
					: Body.GetStringProperty(KingdomFounderOriginCodec.ReceiptProperty, null);
			}

			public void WriteReceipt(string Wire)
			{
				// GameObject.SetStringProperty is a plain dictionary assignment on the pinned
				// 2.0.211.51 decompile (XRL/World/GameObject.cs:4951): no event, no handler, no
				// chance for anything to run between this write and the next.
				if (Body != null)
					Body.SetStringProperty(KingdomFounderOriginCodec.ReceiptProperty, Wire);
			}

			public bool HasOrigin()
			{
				return Body != null && Body.HasStringProperty(OriginProperty);
			}

			public string RawOrigin()
			{
				return Body == null ? null : Body.GetStringProperty(OriginProperty, null);
			}

			public void WriteOrigin(string Origin)
			{
				if (Body != null) Body.SetStringProperty(OriginProperty, Origin);
			}

			public bool TryTally(string Profile, out int Count)
			{
				Count = 0;
				return Tally != null && Profile != null && Tally.TryGetValue(Profile, out Count);
			}

			public void WriteTally(string Profile, int Count)
			{
				if (Tally != null && Profile != null) Tally[Profile] = Count;
			}
		}

		/// <summary>The shared origin label ordinary arrivals also write. Named once, here.
		/// </summary>
		private const string OriginProperty = "KingdomOrigin";

		/// <summary>
		/// Counts one founder into this settlement's origin tally, exactly once, ever, by ADDING
		/// one to whatever the tally already holds.
		/// <para>
		/// It never sets the tally to a count of founders. Ordinary arrivals raise the same
		/// dictionary inside their own before/after protocol, so five citizens who were already
		/// here plus four founders is nine, and a founder fix that took a maximum would silently
		/// swallow the five.
		/// </para>
		/// </summary>
		private static KingdomFounderOriginOutcome AccountFounderOrigin(KingdomSystem System,
			GameObject Body, KingdomQuickstartReceipt Receipt, out string Reason)
		{
			Reason = "";
			if (System == null || Body == null)
			{
				Reason = "the founder origin accounting had no settlement or body";
				return KingdomFounderOriginOutcome.Quarantined;
			}
			return KingdomFounderOriginEngine.Account(new FounderOriginHost(Body,
				System.OriginCounts, System.SettlementIdentityFirstClaimedZone),
				Receipt.ProfileKey, out Reason);
		}
	}
}
