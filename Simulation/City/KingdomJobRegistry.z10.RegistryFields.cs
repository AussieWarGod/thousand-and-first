using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The realm's open jobs, in the shape a save can hold them.
	/// <para>
	/// The same carrier/rules pairing <see cref="KingdomBindingRegistry"/> has, for the same reason
	/// (&sect;1.3): a named-field reader must assign fields and the rules layer must not. Legs are
	/// flattened into their own columns and each job says how many of them are its own, because a
	/// jagged list of lists is not something a named-field writer can hold.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class KingdomJobRegistry
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>The realm's job id counter. Never reused, never drawn: a job id is the ordinal
		/// its delivery's draws hang off (&sect;2.4), and a seeded id would make which carrier walks
		/// in depend on how many other things had been rolled first.</summary>
		public int JobCounter;

		public List<int> JobIds = new List<int>();

		public List<int> Kinds = new List<int>();

		public List<int> Cargos = new List<int>();

		public List<int> CargoAmounts = new List<int>();

		public List<string> SourceZoneIds = new List<string>();

		public List<string> DestZoneIds = new List<string>();

		public List<long> StartTicks = new List<long>();

		public List<int> WalkTicksPerCell = new List<int>();

		public List<int> Statuses = new List<int>();

		public List<int> OriginCodes = new List<int>();

		public List<int> DepositLegIndexes = new List<int>();

		// ---- Named-person mission payload -----------------------------------------------
		// Additive named-field columns. A save from before expeditions has all seven absent;
		// Normalize pads that whole legacy envelope with neutral values before taking the
		// shortest row count. A partially present current envelope remains malformed and is
		// truncated instead of guessing mission authority.

		public List<int> SubjectIds = new List<int>();

		public List<string> SubjectNames = new List<string>();

		public List<string> TargetNames = new List<string>();

		public List<long> DueTicks = new List<long>();

		public List<int> WaterCosts = new List<int>();

		public List<int> ProvisionCosts = new List<int>();

		public List<int> OutcomeCodes = new List<int>();

		// ---- Final expedition publication receipt -----------------------------------------
		// One additive envelope. All four columns are absent in historical saves; Normalize
		// pads only that exact absence and refuses partially present current authority.

		public List<int> ExpeditionDeedDispositions = new List<int>();

		public List<string> ExpeditionDeedPolityIds = new List<string>();

		public List<string> ExpeditionDeedCauseRefs = new List<string>();

		public List<string> ExpeditionDeedFigureRefs = new List<string>();

		// ---- Exact central-delivery payload --------------------------------------------
		// Additive v4 named columns. Endpoint ids are stable hashes used by the sparse
		// distance matrix; full engine object ids bind physical debit and receipt exactly.

		public List<int> DeliverySourceEndpointIds = new List<int>();

		public List<string> DeliverySourceObjectIds = new List<string>();

		public List<int> DeliverySourceXs = new List<int>();

		public List<int> DeliverySourceYs = new List<int>();

		public List<int> DeliveryTargetEndpointIds = new List<int>();

		public List<string> DeliveryTargetObjectIds = new List<string>();

		public List<int> DeliveryTargetXs = new List<int>();

		public List<int> DeliveryTargetYs = new List<int>();

		public List<long> DeliverySourceBeforeAmounts = new List<long>();

		public List<int> DeliveryTripIds = new List<int>();

		public List<int> DeliveryStopOrdinals = new List<int>();

		public List<int> DeliveryPhases = new List<int>();

		public List<int> DeliveryCargoAuthorityKinds = new List<int>();

		public List<string> DeliveryOwnerOperationIds = new List<string>();

		public List<int> DeliveryOwnerManifestVersions = new List<int>();

		public List<string> DeliveryOwnerManifestDigests = new List<string>();

		public List<long> DeliveryOwnerManifestRevisions = new List<long>();

		public List<int> DeliveryManifestSourceStarts = new List<int>();

		public List<int> DeliveryManifestSourceCounts = new List<int>();

		public List<long> DeliveryTargetBeforeAmounts = new List<long>();

		public List<int> DeliveryTargetReceiptStates = new List<int>();

		public List<int> LegCounts = new List<int>();

		// ---- Legs, flattened in job order -----------------------------------------------------

		public List<string> LegZoneIds = new List<string>();

		public List<int> LegEnterX = new List<int>();

		public List<int> LegEnterY = new List<int>();

		public List<int> LegExitX = new List<int>();

		public List<int> LegExitY = new List<int>();

		public List<int> LegLengths = new List<int>();

		public List<long> LegDepartTicks = new List<long>();

		public List<long> LegArriveTicks = new List<long>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomJobRegistry));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomJobRegistry));
			Normalize();
		}
#endif

		public int Count => JobIds.Count;
	}
}
