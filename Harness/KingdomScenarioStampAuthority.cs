using System;

using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Stamp presence and publication: the one authority that writes durable scenario provenance.
	/// <para>
	/// There is exactly ONE write path - <see cref="TryWriteProvenance"/>. The opening stamp in
	/// KingdomScenarioRealizer.TryStamp and the measured republication below both call it, so
	/// neither can quietly become the lenient one. The previous split kept two encode/write/readback
	/// sequences in two files and called that "one authority"; two copies that agree today are not
	/// one authority, they are a divergence waiting for its first edit.
	/// </para>
	/// </summary>
	internal static partial class KingdomScenarioRealizer
	{
		/// <summary>
		/// Republishes the stamp with the key set this run actually measured.
		/// <para>
		/// The opening stamp carries <c>KeySetDigest=null</c>, because nothing has been compared
		/// yet. Signing an in-memory measured copy and writing a TSV left the durable record saying
		/// no comparison happened, so after the popup closed - or after any save and reload -
		/// status told an operator the truth of the pre-run state, not of the run. The governing
		/// requirement is that sc1 provenance carries the compared digest, so it is published here
		/// through the SAME exact write/readback authority that wrote the original.
		/// </para>
		/// <para>
		/// A torn or failed publication stays visibly non-green and licenses nothing: the
		/// transaction is already committed and the marker still refuses every replay.
		/// </para>
		/// </summary>
		internal static bool TryPublishMeasured(KingdomScenarioProvenance Measured,
			out string Failure)
		{
			Failure = null;
			if (Measured == null)
				return Refuse("there is no measured stamp to publish", out Failure);
			if (!KingdomScenarioRules.ValidDigest(Measured.KeySetDigest))
				return Refuse("the measured key-set digest is malformed; refusing to publish it",
					out Failure);
			string detail;
			if (Shape(out detail) != KingdomScenarioStampShape.Readable)
				return Refuse("this game's scenario stamp is not readable ("
					+ (detail ?? "unknown fault") + "); refusing to republish over it", out Failure);
			if (!TryWriteProvenance(Measured, out Failure)) return false;
			if (Shape(out detail) != KingdomScenarioStampShape.Readable)
				return Refuse("the measured scenario stamp did not read back as a readable pair; "
					+ "the durable record is torn and this run is not green", out Failure);
			return true;
		}

		/// <summary>
		/// The ONE durable provenance write: encode, write, prove the exact text read back.
		/// <para>
		/// The PAIR's shape is deliberately the caller's business. The opening stamp writes the
		/// presence marker after this returns, so the pair is legitimately not yet Readable here;
		/// the measured republication proves Readable both before and after. Putting the pair check
		/// inside would make the first lawful write refuse itself.
		/// </para>
		/// </summary>
		internal static bool TryWriteProvenance(KingdomScenarioProvenance Record, out string Failure)
		{
			Failure = null;
			string wire = KingdomScenarioProvenanceRules.Encode(Record);
			if (string.IsNullOrEmpty(wire))
				return Refuse("the scenario stamp could not be encoded", out Failure);
			The.Game.SetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, wire);
			if (!KingdomScenarioDurableState.ProvesExactText(
				KingdomScenarioProvenanceRules.ProvenanceState, wire))
				return Refuse("the scenario stamp did not read back exactly", out Failure);
			return true;
		}

		/// <summary>Raw key-presence shape of the stamp pair, before any decode is attempted.</summary>
		private static KingdomScenarioStampShape Shape(out string Detail)
		{
			return KingdomScenarioStateShape.Stamp(
				KingdomScenarioDurableState.Observe(
					KingdomScenarioProvenanceRules.ProvenanceState),
				KingdomScenarioDurableState.Observe(StampedState), out Detail);
		}

		/// <summary>
		/// Raw stamp presence. Absent is the only case a caller may treat as ordinary play; a
		/// present-but-torn stamp is an explicit refusal, never a fall-through.
		/// </summary>
		internal static KingdomScenarioStampShape Presence(
			out KingdomScenarioProvenance Record, out string Failure)
		{
			Record = null;
			KingdomScenarioStampShape shape = Shape(out Failure);
			if (shape != KingdomScenarioStampShape.Readable) return shape;
			string raw = The.Game.GetStringGameState(
				KingdomScenarioProvenanceRules.ProvenanceState);
			if (KingdomScenarioProvenanceRules.TryDecode(raw, out Record, out Failure))
				return KingdomScenarioStampShape.Readable;
			return KingdomScenarioStampShape.PresentUnreadable;
		}
	}
}
