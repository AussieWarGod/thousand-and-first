using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		// --- The engine side of one stockpile deposit ----------------------------------------
		//
		// Everything here is a single engine operation and a reading taken straight afterwards.
		// The law about what may be counted, what may be destroyed and when the delivery stops
		// lives in KingdomDepositEngine, which knows nothing about GameObjects and can therefore
		// be driven against a handler that carries the bundle off mid-callback.

		/// <summary>
		/// One store, one material, and the observations the survey needs while a delivery runs
		/// through it. A host is made for a single fill and thrown away with it: the destination
		/// is re-proved on every reading rather than remembered.
		/// </summary>
		internal sealed class StockpileDepositHost : IKingdomDepositHost
		{
			private readonly Zone Z;

			private readonly GameObject Container;

			private readonly string Blueprint;

			internal StockpileDepositHost(Zone Z, GameObject Container, string Blueprint)
			{
				this.Z = Z;
				this.Container = Container;
				this.Blueprint = Blueprint;
			}

			/// <summary>The once-only saying, kept on the store itself so it survives a restart
			/// and is taken back by the next delivery that lands in it proved.</summary>
			public bool CustodyAnnounced
			{
				get
				{
					return GameObject.Validate(Container) && Container.GetIntProperty(
						KingdomRules.StockpileCustodyAnnouncedProperty) == 1;
				}
				set
				{
					if (!GameObject.Validate(Container))
					{
						return;
					}
					Container.SetIntProperty(KingdomRules.StockpileCustodyAnnouncedProperty,
						value ? 1 : 0, RemoveIfZero: true);
				}
			}

			public int RoomNow()
			{
				return DepositRoomNow(Container);
			}

			public int HeldNow()
			{
				return DepositHeldNow(Container);
			}

			public object Create()
			{
				return GameObject.Create(Blueprint);
			}

			public bool Stacks(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				return item != null && item.HasPart("Stacker");
			}

			public void Stamp(object Bundle, int Count)
			{
				GameObject item = Bundle as GameObject;
				if (item != null)
				{
					item.Count = Count;
				}
			}

			public int CountOf(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				return (item != null) ? item.Count : 0;
			}

			public bool Alive(object Bundle)
			{
				return GameObject.Validate(Bundle as GameObject);
			}

			/// <summary>A bundle in no inventory and in no cell reached nobody at all, and is the
			/// only kind this delivery may ever destroy.</summary>
			public bool Ownerless(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				return item != null && item.InInventory == null && item.CurrentCell == null;
			}

			public void Discard(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				if (GameObject.Validate(item))
				{
					item.Obliterate();
				}
			}

			/// <summary>A deposit must never merge into an exact stack another durable receipt
			/// owns. NoStack keeps both identities observable across engine callbacks.</summary>
			public object Insert(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				GameObject accepted = null;
				try { accepted = Container.Inventory.AddObject(item, null,
					Silent: true, NoStack: true); }
				catch
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, Container);
					KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
					throw;
				}
				KingdomSurvey.ObserveChangedInActive(Z, Container);
				KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
				return accepted;
			}

			public bool Landed(object Bundle, object Accepted, int Batch)
			{
				return DepositLanded(Container, Bundle as GameObject, Accepted as GameObject,
					Blueprint, Batch);
			}

			/// <summary>Said once, in the founder's own words: a bundle went somewhere the
			/// keepers cannot account for, so the rest of the delivery is held rather than
			/// made a second time.</summary>
			public void AnnounceUncertainCustody()
			{
				string store = GameObject.Validate(Container)
					? Container.ShortDisplayName : "stockpile";
				MessageQueue.AddPlayerMessage("{{K|A bundle bound for the " + store
					+ " ended up somewhere the keepers cannot account for; the rest of the load"
					+ " is held rather than made a second time.}}");
				KingdomLog.Log("materials: deposit custody unproved, blueprint=" + Blueprint
					+ " room=" + RoomNow() + " held=" + HeldNow());
			}
		}
	}
}
