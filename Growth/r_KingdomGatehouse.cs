using System;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>
	/// Legacy stateless projection hook. This shipped with no serialized fields and must remain
	/// zero-field forever: engine part fields are serialized by positional reflection. Durable
	/// v1 identity and phase live in named root properties. Callback custody lives only in the
	/// separately attached <see cref="r_KingdomGatehouseProjectionV2"/> or the temporary
	/// <see cref="r_KingdomGatehouseProjectionV1Pending"/> migration carrier.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomGatehouse : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID;
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			KingdomGatehouse.MaterializeFromEnteredCell(ParentObject, E.Cell);
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// V2-only callback-cut custody. These six fields are the complete positional save layout;
	/// never append, remove, reorder, or change their types. The part is attached in code only
	/// after an exact v2 form receipt is decoded, so historical v1 roots never deserialize it.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomGatehouseProjectionV2 : IPart
	{
		public GameObject SatelliteCustody0;
		public GameObject SatelliteCustody1;
		public GameObject SatelliteCustody2;
		public GameObject SatelliteCustody3;
		public GameObject SatelliteCustody4;
		public GameObject SatelliteCustody5;

		internal GameObject ProjectionCustody(int Index)
		{
			switch (Index)
			{
			case 0: return SatelliteCustody0;
			case 1: return SatelliteCustody1;
			case 2: return SatelliteCustody2;
			case 3: return SatelliteCustody3;
			case 4: return SatelliteCustody4;
			case 5: return SatelliteCustody5;
			default: return null;
			}
		}

		internal bool SetProjectionCustody(int Index, GameObject Value)
		{
			switch (Index)
			{
			case 0: SatelliteCustody0 = Value; break;
			case 1: SatelliteCustody1 = Value; break;
			case 2: SatelliteCustody2 = Value; break;
			case 3: SatelliteCustody3 = Value; break;
			case 4: SatelliteCustody4 = Value; break;
			case 5: SatelliteCustody5 = Value; break;
			default: return false;
			}
			return ReferenceEquals(ProjectionCustody(Index), Value);
		}
	}

	/// <summary>
	/// Temporary completion custody for a v1 scaffold/job that was already paid before v2 form
	/// receipts existed. Its six fields are its complete positional layout forever. New v1 work
	/// cannot be commissioned; this part is attached only while an exact saved v1 payload lands
	/// its engine-assigned satellite identities, then removed before the completed schema commits.
	/// Historical completed v1 roots therefore remain the original zero-companion shape.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomGatehouseProjectionV1Pending : IPart
	{
		public GameObject SatelliteCustody0;
		public GameObject SatelliteCustody1;
		public GameObject SatelliteCustody2;
		public GameObject SatelliteCustody3;
		public GameObject SatelliteCustody4;
		public GameObject SatelliteCustody5;

		internal GameObject ProjectionCustody(int Index)
		{
			switch (Index)
			{
			case 0: return SatelliteCustody0;
			case 1: return SatelliteCustody1;
			case 2: return SatelliteCustody2;
			case 3: return SatelliteCustody3;
			case 4: return SatelliteCustody4;
			case 5: return SatelliteCustody5;
			default: return null;
			}
		}

		internal bool SetProjectionCustody(int Index, GameObject Value)
		{
			switch (Index)
			{
			case 0: SatelliteCustody0 = Value; break;
			case 1: SatelliteCustody1 = Value; break;
			case 2: SatelliteCustody2 = Value; break;
			case 3: SatelliteCustody3 = Value; break;
			case 4: SatelliteCustody4 = Value; break;
			case 5: SatelliteCustody5 = Value; break;
			default: return false;
			}
			return ReferenceEquals(ProjectionCustody(Index), Value);
		}
	}
}
