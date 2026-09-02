using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// The realm's one carry-sign haul in flight: materials already swept from their origin,
	/// waiting to be poured into the destination settlement's stockpiles the next time it
	/// activates and the haul is due. Held on <see cref="KingdomSystem"/> directly, realm-level
	/// like <c>KingdomSystem.Manifest</c> — a haul is addressed to an immutable settlement id;
	/// the carried name is prose only, so it survives renames and every seat swap untouched.
	/// </summary>
	[Serializable]
	public class KingdomCarryHaul
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>Zone the sign was planted in, kept for the chronicle and for nothing the
		/// resolver reads back.</summary>
		public string OriginZoneID;

		public int OriginX;

		public int OriginY;

		/// <summary>Immutable destination authority. The name below is prose only.</summary>
		public string DestinationSettlementId;

		/// <summary>The settlement's frozen display name, used only in prose.</summary>
		public string DestinationSettlementName;

		public long PlantedTick;

		/// <summary>Absolute tick the haul is ready to resolve. No expiry beyond this — absence
		/// never punishes; a haul left unresolved simply waits for the next attended pass of its
		/// destination, exactly as a raid warning waits out an absent founder.</summary>
		public long DueTick;

		public int Mud;

		public int Brush;

		public int Timber;

		public int Stone;

		public int Marble;

		public int Scrap;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomCarryHaul));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomCarryHaul));
		}
#endif
	}
}
