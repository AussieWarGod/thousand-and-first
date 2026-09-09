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
		// be driven against a handler that carries the bundle off, vetoes its destruction, or
		// merges it away mid-callback.

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

			/// <summary>The saying, remembered here as well as on the store. A store a handler
			/// destroyed mid-fill can no longer carry a property, and this at least keeps one
			/// fill from saying the same thing twice about it.</summary>
			private bool Spoken;

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
					return GameObject.Validate(Container)
						? Container.GetIntProperty(
							KingdomRules.StockpileCustodyAnnouncedProperty) == 1
						: Spoken;
				}
				set
				{
					Spoken = value;
					if (GameObject.Validate(Container))
					{
						Container.SetIntProperty(KingdomRules.StockpileCustodyAnnouncedProperty,
							value ? 1 : 0, RemoveIfZero: true);
					}
				}
			}

			public int RoomNow()
			{
				return DepositRoomNow(Container);
			}

			public int MaterialHeldNow()
			{
				return DepositMaterialHeldNow(Container, Blueprint);
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

			/// <summary>Nobody is holding it: no cell, and no holder of any of the three kinds the
			/// engine keeps separately. Equipping and implanting both CLEAR the inventory and the
			/// cell, so reading those two alone would call an equipped bundle ownerless and licence
			/// the delivery to destroy something a creature is wearing.</summary>
			public bool HeldByNobody(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				return GameObject.Validate(item) && item.Holder == null
					&& item.CurrentCell == null;
			}

			/// <summary>Destruction is vetoable, and a veto handler may move the body before it
			/// refuses, so the body is read again afterwards. Only a bundle that is provably gone
			/// counts as withdrawn.</summary>
			public bool Discard(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				if (!GameObject.Validate(item))
				{
					return false;
				}
				bool gone = item.Obliterate(null, Silent: true);
				return gone && !GameObject.Validate(item);
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

			/// <summary>
			/// Said once, in the founder's own words: a bundle went somewhere the keepers cannot
			/// account for, so the rest of the delivery is held rather than made a second time.
			/// <para>
			/// The log line goes first and is built out of raw strings only, because everything
			/// below it can run somebody else's code: a display name is assembled by handlers,
			/// and the room and hold readings walk objects and ask each one its count. A
			/// diagnostic must never be the reason a delivery loses the units it proved.
			/// </para>
			/// </summary>
			public void AnnounceUncertainCustody()
			{
				KingdomLog.Log("materials: deposit custody unproved, blueprint=" + Blueprint
					+ " store=" + StoreLabel());
				MessageQueue.AddPlayerMessage("{{K|A bundle bound for the " + StoreName()
					+ " ended up somewhere the keepers cannot account for; the rest of the load"
					+ " is held rather than made a second time.}}");
			}

			/// <summary>The store's blueprint id, which is a plain field and reaches nobody.
			/// </summary>
			private string StoreLabel()
			{
				return GameObject.Validate(Container) ? (Container.Blueprint ?? "?") : "gone";
			}

			/// <summary>The store's name as the founder reads it, and its blueprint id when the
			/// name cannot be had: <c>ShortDisplayName</c> is assembled by display handlers, and
			/// one of them throwing may not cost the delivery its accounting.</summary>
			private string StoreName()
			{
				if (!GameObject.Validate(Container))
				{
					return "stockpile";
				}
				try { return Container.ShortDisplayName; }
				catch { return Container.Blueprint ?? "stockpile"; }
			}
		}
	}
}
