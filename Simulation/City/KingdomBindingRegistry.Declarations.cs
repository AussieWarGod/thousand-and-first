using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What a binding key names. LIVING-CITY-ARCHITECTURE &sect;3.8: one registry answers for both,
	/// because both are things this mod <b>mints</b>, and anything we mint we can duplicate.
	/// </summary>
	public enum KingdomBindingKind : byte
	{
		/// <summary>The key is a <c>ResidentId</c>. A person, and &sect;8.3's law applies to them:
		/// materialisation may mint or move a body and may never remove one.</summary>
		Resident = 0,

		/// <summary>The key is a <c>JobId</c> &mdash; a delivery, a mend, a message. A transient is
		/// a RENDERING of a job, and jobs close.</summary>
		Transient = 1
	}

	/// <summary>
	/// Why a binding was evicted. LIVING-CITY-ARCHITECTURE &sect;3.8 keeps no second "closed" list,
	/// so absence from the registry is proof of closure &mdash; which makes the cause something the
	/// caller must state at the moment it evicts, and a reason that arrives later is a reason
	/// nobody wrote down.
	/// </summary>
	public enum KingdomUnbindCause : byte
	{
		/// <summary>Not a cause. Refused: an unbinding without a reason is how a settler
		/// disappears and nothing in the game says why.</summary>
		None = 0,

		/// <summary>The body was killed. The row reads <c>Dead</c> and the city holds a
		/// funeral.</summary>
		Death = 1,

		/// <summary>The person left the roll &mdash; emigration, exile, secession.</summary>
		Departure = 2,

		/// <summary>The body is elsewhere with the founder. The row reads <c>Abroad</c>: still on
		/// the roll, contributing no labour, and honestly reported as such.</summary>
		Abroad = 3,

		/// <summary>The job reached its completion tick and the model closed it. Transients
		/// only.</summary>
		JobClosed = 4,

		/// <summary>The realm let the whole city go. Every binding it held is evicted at
		/// once.</summary>
		Dissolved = 5,

		/// <summary>The resident took the charter and became the player. Their real body remains,
		/// but it is no longer a city-model view and must never be re-minted from the old row.</summary>
		Accession = 6,

		/// <summary>A transient reached an exact zone boundary or shaft endpoint. Its one live
		/// rendering is removed while the still-open job remains authority for the next zone to
		/// render. Never valid for residents and never closes the job.</summary>
		ZoneHandoff = 7
	}

	/// <summary>
	/// What the engine edge found when it resolved an existing binding's object. The one thing the
	/// pure rule cannot know for itself, so it is handed in.
	/// </summary>
	public enum KingdomBodyPresence : byte
	{
		/// <summary>There is no binding for this key at all.</summary>
		None = 0,

		/// <summary>The bound object resolves live in the zone being asked about.</summary>
		Here = 1,

		/// <summary>The bound object resolves live in another zone that is currently
		/// resident &mdash; in RAM, reachable, movable.</summary>
		Elsewhere = 2,

		/// <summary>The bound object does not resolve: its zone is on disk. <b>The frozen body is
		/// invisible; its binding is not.</b></summary>
		Frozen = 3
	}

	/// <summary>What check-before-mint answers. LIVING-CITY-ARCHITECTURE &sect;3.8.</summary>
	public enum KingdomBindingVerdict : byte
	{
		/// <summary>Nothing is bound to this key. Mint a body, and write the binding in the SAME
		/// copy-on-write publish as the debt decrement.</summary>
		Mint = 0,

		/// <summary>A body is already here. Move it. Do not mint.</summary>
		Move = 1,

		/// <summary>A body is live in another resident zone. A resident moves across; a transient
		/// does not, because a porter is a rendering of one job and one job has one road.</summary>
		MoveAcross = 2,

		/// <summary>Refuse. The debt stays owed. <b>An unresolvable binding is a refusal to mint,
		/// never a licence to mint</b> &mdash; that single line is the whole anti-duplication
		/// argument, and it holds across suspend, freeze, save, reload and crash.</summary>
		Refuse = 3
	}

	/// <summary>What the stale-transient sweep says about one object found in a thawed zone.</summary>
	public enum KingdomSweepVerdict : byte
	{
		/// <summary>The object carries no job id. Not ours to judge, and never touched.</summary>
		NotTransient = 0,

		/// <summary>The object carries a job id whose binding is still open. It is the rendering
		/// of a job that has not finished; leave it alone.</summary>
		Bound = 1,

		/// <summary>The object carries a job id with no open binding. The model closed the job
		/// while the ground was on disk, and the goods it is holding were already credited at the
		/// dated tick. This is the one instant they could exist twice.</summary>
		Stale = 2
	}

	/// <summary>
	/// One binding: which key, of which kind, in which ground, on which object, from when.
	/// <para>
	/// Twenty-nine declared bytes against the thirty-two LIVING-CITY-ARCHITECTURE &sect;0.0(c)
	/// budgets. The object reference is the engine's own persistent object <c>ID</c> string and not
	/// a live reference, which is not a compromise but the point: a live reference to a body in a
	/// frozen zone is exactly the thing that cannot survive the case &sect;3.8 was written for.
	/// </para>
	/// </summary>
	internal readonly struct KingdomBinding
	{
		internal readonly int BindingKey;

		internal readonly KingdomBindingKind Kind;

		internal readonly string ZoneId;

		internal readonly string ObjectId;

		internal readonly long MintedTick;

		internal KingdomBinding(int bindingKey, KingdomBindingKind kind, string zoneId, string objectId, long mintedTick)
		{
			BindingKey = bindingKey;
			Kind = kind;
			ZoneId = zoneId;
			ObjectId = objectId;
			MintedTick = mintedTick;
		}

		/// <summary>This binding in other ground, on whatever object stands there now. The minted
		/// tick does not move: a body that walked across a zone line is the same body, and
		/// redating it would lose the one fact the registry is for.</summary>
		internal KingdomBinding WithPlace(string zoneId, string objectId)
		{
			return new KingdomBinding(BindingKey, Kind, zoneId, objectId, MintedTick);
		}
	}

}
