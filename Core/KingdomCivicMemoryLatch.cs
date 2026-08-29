namespace ThousandAndFirst
{
	/// <summary>
	/// A one-way switch. Once civic memory has failed to read what was on disk, this is thrown,
	/// and nothing in this mod can throw it back.
	/// <para>
	/// It is a class rather than a field because of how the last one of these died. C17's save
	/// veto was correct, tested, and useless: <c>LoadFailed</c> was an ordinary mutable field, so
	/// <c>ReportLoadFailure()</c> &mdash; sitting in a different file, doing nothing but telling
	/// the founder what had happened &mdash; could clear it, and the veto quietly expired the
	/// moment the warning was shown. The lesson is not "be careful with that field". The lesson
	/// is that a latch anybody can assign will eventually be assigned by somebody solving a
	/// different problem.
	/// </para>
	/// <para>
	/// So the state lives here, private, behind a method that only ever sets it. There is no
	/// <c>Clear</c>, no <c>Reset</c>, no <c>Dismiss</c>, no <c>Acknowledge</c>, and no setter,
	/// public or private, anywhere in the tree; the only reachable transition is false to true.
	/// Diagnosis and reporting can read <see cref="Tripped"/> and print <see cref="Reason"/> as
	/// often as they like, and neither costs anything, because reading is all they can do. A
	/// session that latches this stays latched until it ends.
	/// </para>
	/// </summary>
	public sealed class KingdomCivicMemoryLatch
	{
		private bool Thrown;
		private string Cause;

		/// <summary>Whether the latch has been thrown. Read-only by construction.</summary>
		public bool Tripped => Thrown;

		/// <summary>Why, in the founder's words. Empty while the latch stands open.</summary>
		public string Reason => Cause ?? "";

		/// <summary>
		/// Throws the latch. The first cause is the one kept: a later, vaguer failure downstream
		/// of the first must not paper over the reason the trouble actually started.
		/// </summary>
		public void Trip(string Cause)
		{
			if (Thrown) return;
			Thrown = true;
			this.Cause = string.IsNullOrEmpty(Cause)
				? "civic memory could not be read from this save" : Cause;
		}
	}
}
