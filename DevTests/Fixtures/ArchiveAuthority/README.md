# Historical settlement authority fixture

`base-v18.bin` was produced by the actual settlement codec and archive authority methods
from commit `7d331fe8a77b630c8245889d36f811d1baf84059`, not the current writer.
It is 50,767 bytes; SHA-256:
`5f3f7074309e62ce61365276a44ac7a7f094c1c4ceccd38b9400a5e558dc2193`.

The bounded envelope contains magic `0x41563138`, source commit, length-prefixed v18
settlement payload, and three historical hash strings. The expected authority hash is
`b96cc4a1c95408fbf4c7448fc2d25b788ce2f294a7b9412561e455071c5e5d2b`.

Original producer and failing consumer evidence is retained at
`/tmp/taf-archive-authority-version.SI524N/README.md`: producer passed; pre-fix consumer
failed preservation for Intent, Attempting and Settled while three controls passed.
The original bytes and failed logs are unchanged.

`KingdomArchiveExplicitBasisTests` runs in the standard Taf suite. It checks the fixed
artifact/hash, explicit historical projection, current resave, mutation refusal, ownership,
unsupported versions and selector behavior. The fixture does not execute the engine
realm envelope reader, live TAG1 graph, full callback or historical whole-save loading.
No test rewrites this artifact or computes a replacement expected historical hash.
