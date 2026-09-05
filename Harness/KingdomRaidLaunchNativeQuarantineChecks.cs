using System;
using System.IO;
using System.Text;
using XRL.World;
using XRL.World.Parts;
using Checks = ThousandAndFirst.Harness.KingdomRaidLaunchNativeChecks;

namespace ThousandAndFirst.Harness
{
	/// <summary>CASE B1 only: the second real mint is handed a DIFFERENT, unprobed blueprint through
	/// the creation event's own ReplacementObject field. Everything here is asserted after the
	/// activation call returned. Nothing is removed: the first raider, the substitute and the
	/// abandoned correct-blueprint original are all retained as evidence, and custody of the
	/// substitute is recorded as unproved rather than claimed.
	/// <para>The same-blueprint replacement is a KNOWN OPEN defect
	/// (Raids/KingdomRaids.05.AttackLaunchAndResume.cs:22-23) and is not claimed here. The B2
	/// swallowed-throw case is a separate, unimplemented gate.</para></summary>
	internal static class KingdomRaidLaunchNativeQuarantineChecks
	{
		internal static void Verify(KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleOperation Op, r_TAF_RaidMintObservation[] Observations)
		{
			Checks.Check(Observations.Length == Checks.SubstituteAtSequence,
				"the probe did not observe exactly the mints up to and including the substituted one");
			for (int k = 0; k < Observations.Length; k++) Checks.VerifyMint(Observations[k], Op, k);
			r_TAF_RaidMintObservation first = Observations[0];
			r_TAF_RaidMintObservation second = Observations[Observations.Length - 1];
			KingdomLifecycleProjection refused = Op.Projections[1];
			Checks.Check(first.Substitute == null && first.SubstituteBlueprint == null,
				"the first mint was substituted; only the second may be");
			Checks.Check(second.Substitute != null && string.Equals(second.SubstituteBlueprint,
				Checks.SubstituteBlueprint, StringComparison.Ordinal),
				"the second mint was not substituted through the creation event");
			Checks.Check(GameObject.Validate(second.Original),
				"the abandoned correct-blueprint original was invalidated; it must be retained as evidence");
			Checks.Check(second.Original != null
				&& !ReferenceEquals(second.Original, second.Substitute)
				&& string.Equals(second.Original.Blueprint, refused.Blueprint, StringComparison.Ordinal),
				"the abandoned correct-blueprint original was not recorded");
			Checks.Check(!string.Equals(second.Substitute.Blueprint, refused.Blueprint,
				StringComparison.Ordinal),
				"the substitute carries the frozen blueprint; that is the OPEN same-blueprint case");
			VerifyQuarantine(Fixture, Op);
			VerifyFirstRaider(Fixture, Op);
			VerifySubstitute(Fixture, refused, second.Substitute);
			VerifyWire(Fixture, Op);
		}

		/// <summary>The exact fixed fault the mint loop raises for a blueprint mismatch, retained on
		/// the book (Raids/KingdomRaids.09.*.cs:76-82).</summary>
		private static void VerifyQuarantine(KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleOperation Op)
		{
			Checks.Check(Op.Phase == KingdomLifecyclePhase.Quarantined,
				"the wrong-blueprint substitution did not quarantine the operation");
			Checks.Check(string.Equals(Op.Fault, Checks.QuarantineFault, StringComparison.Ordinal),
				"the quarantine fault is not the fixed mint-loop text: " + (Op.Fault ?? "(none)"));
			Checks.Check(ReferenceEquals(Fixture.System.LifecycleBook.Raid, Op),
				"the quarantined operation was not retained as the book's raid authority");
		}

		/// <summary>The prior actor was already placed and proved before the refused mint, and it
		/// survives the refusal intact (ExactRaiderBody shape, 09.cs:111-125).</summary>
		private static void VerifyFirstRaider(KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleOperation Op)
		{
			Checks.Check(Op.Spawned == 1,
				"exactly one raider must be counted as spawned before the refusal");
			Checks.Check(Op.Projections[0].State == KingdomLifecyclePhysicalState.Proved,
				"the first projection was not Proved before the second mint was refused");
			Checks.Check(Op.Projections[1].State != KingdomLifecyclePhysicalState.Proved,
				"the refused projection was proved anyway");
			Checks.VerifyBody(Fixture, Op, Op.Projections[0], 0);
		}

		/// <summary>The quarantine fires BEFORE the identity stamp and PrepareRaiderBody
		/// (09.cs:79-84), so the substitute must be untouched. It is recorded, never destroyed:
		/// custody of an object this harness did not place is unproved.</summary>
		private static void VerifySubstitute(KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleProjection Refused, GameObject Substitute)
		{
			Checks.Check(GameObject.Validate(Substitute),
				"the substitute was invalidated; it is retained evidence");
			// GameObject.ID lazily stamps an identity on first access (07.cs:241-255);
			// IDIfAssigned never forces that. Nothing here, the probe, or PrepareRaiderBody (never
			// reached for the substitute) ever reads Substitute.ID, but an eager creation-time
			// stamp for some other blueprint cannot be ruled out from this evidence alone, so the
			// weaker always-sound claim is asserted and the actual value is recorded in the row.
			Checks.Check(!string.Equals(Substitute.IDIfAssigned, Refused.ObjectId,
				StringComparison.Ordinal),
				"the substitute was stamped with the refused projection identity: "
					+ (Substitute.IDIfAssigned ?? "(null)"));
			Checks.Check(Substitute.GetStringProperty(KingdomRaids.ProjectionMarkerProperty) == null
				&& Substitute.GetIntProperty("KingdomRaider") == 0
				&& Substitute.GetPart<NoXPGain>() == null
				&& r_TAF_RaidMintSnapshots.MarkerParts(Substitute) == 0,
				"the substitute was prepared as a raider");
			Checks.Check(Substitute.CurrentCell == null, "the substitute entered the zone");
			int ids, markers;
			r_TAF_RaidMintSnapshots.Census(Fixture.Zone, Refused, out ids, out markers);
			Checks.Check(ids == 0 && markers == 0,
				"the refused projection left a body or marker standing in the zone");
		}

		/// <summary>Model durability only. Codec write precedent
		/// Harness/KingdomRaids.NativeOutbox.cs:170-185; read-back precedent
		/// DevTests/KingdomGrowthLifecycleRulesTests.cs:3760-3770. This proves the quarantined phase
		/// and its fixed fault survive the lifecycle wire. It is NOT a save/load claim &mdash; no
		/// save is written or reloaded, and the report says save-load=untested.</summary>
		private static void VerifyWire(KingdomRaidLaunchNativeFixture Fixture,
			KingdomLifecycleOperation Op)
		{
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, Fixture.System.LifecycleBook);
				bytes = stream.ToArray();
			}
			KingdomLifecycleBook restored = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(bytes, false))
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), restored);
			Checks.Check(restored.Raid != null
				&& restored.Raid.Phase == KingdomLifecyclePhase.Quarantined
				&& string.Equals(restored.Raid.Fault, Checks.QuarantineFault, StringComparison.Ordinal)
				&& string.Equals(restored.Raid.Id, Op.Id, StringComparison.Ordinal)
				&& restored.Raid.Spawned == Op.Spawned
				&& restored.Raid.Projections.Count == Op.Projections.Count,
				"the quarantined operation did not survive a lifecycle wire round trip");
		}
	}
}
