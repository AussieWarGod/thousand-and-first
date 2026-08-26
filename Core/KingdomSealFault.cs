using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Why a seal file was refused. Every refusal is named, because a seal is refused whole or
	/// accepted whole and the founder is owed the reason either way.
	/// </summary>
	internal enum KingdomSealFault
	{
		None = 0,
		/// <summary>Nothing there, or nothing but whitespace.</summary>
		Empty,
		/// <summary>The first line is not this format's name.</summary>
		NotASeal,
		/// <summary>A schema this build does not read. The version gate.</summary>
		UnsupportedSchema,
		/// <summary>A framing line is missing or malformed.</summary>
		MalformedFraming,
		/// <summary>The payload is not the length the framing declared.</summary>
		LengthMismatch,
		/// <summary>The payload does not hash to the digest the framing declared.</summary>
		ChecksumMismatch,
		/// <summary>Bytes past the declared payload.</summary>
		TrailingData,
		/// <summary>Larger than any honest seal.</summary>
		TooLarge,
		/// <summary>The payload is not this format's strict subset of JSON.</summary>
		Malformed,
		/// <summary>The same key twice.</summary>
		DuplicateKey,
		/// <summary>A key this schema does not define.</summary>
		UnknownKey,
		/// <summary>A key this schema requires, absent.</summary>
		MissingKey,
		/// <summary>A key present with the wrong shape.</summary>
		WrongKind,
		/// <summary>A number, string, or list past its declared bound.</summary>
		OutOfBounds,
		/// <summary>No digest provider. Never worked around.</summary>
		DigestUnavailable
	}

	/// <summary>
	/// The kind a value in a seal payload has. Deliberately four and no more: bounded primitives
	/// and lists of them, which is the whole of what may cross runs.
	/// <para>
	/// <see cref="EmptyList"/> exists because <c>[]</c> cannot say whether it wanted numbers or
	/// text, and inventing an answer at parse time is how a reader starts disagreeing with a
	/// writer. It answers to both accessors and to neither type.
	/// </para>
}
