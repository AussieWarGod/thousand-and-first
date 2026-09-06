#!/usr/bin/env python3
"""Check receipt-bound bytes in a caller-selected standard Workshop content folder.

The folder name is not Steam authority. This observes neither subscriptions nor
current server/client state, and matching cached bytes do not prove a new transfer.
No SDK, network, download, .acf access, source writes, or attempt reconciliation.
"""
from __future__ import annotations

import argparse
from contextlib import ExitStack
import json
import os
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
if __package__:
    from . import workshop_upload_plan as upload
else:
    import workshop_upload_plan as upload


def verify_installed_bytes(installed_root: str | Path, receipt: str | Path,
                           receipt_sha: str, mode: str, expected_item: str,
                           expected_version: str) -> dict:
    upload.require(isinstance(receipt_sha, str) and re.fullmatch(r"[0-9a-f]{64}", receipt_sha) is not None,
                   "approved receipt SHA must be exactly 64 lowercase hex characters")
    upload.require(os.name == "posix" and hasattr(os, "O_NOFOLLOW"),
                   "requires POSIX no-follow descriptor support")
    root, receipt_path = upload._path(installed_root), upload._path(receipt)
    upload.require(isinstance(expected_item, str) and root.parts[-5:] ==
                   ("steamapps", "workshop", "content", str(upload.APP_ID), expected_item),
                   "installed root must end in steamapps/workshop/content/333640/<expected item>")
    upload.require(root not in receipt_path.parents, "receipt must be external to installed content")
    with ExitStack() as stack:
        anchors = {}
        fd = upload._anchor(receipt_path, False, stack, anchors)
        approved = upload._read(fd, upload.MAX_RECEIPT_BYTES)
        upload.require(approved[1] == receipt_sha, "approved receipt SHA mismatch before package verification")
        plan = upload.build_plan(root, receipt_path, mode, expected_item, expected_version)
        upload.require(plan["receiptSHA"] == receipt_sha, "approved receipt SHA mismatch in verified plan")
        upload.require(approved == upload._read(fd, upload.MAX_RECEIPT_BYTES),
                       "approved receipt changed during package verification")
        after = {}
        upload._anchor(receipt_path, False, stack, after)
        upload.require(anchors == after, "approved receipt path authority changed")
    return {
        "schema": "taf-workshop-installed-bytes-v1", "status": "installed_bytes_match",
        "item": plan["targetItem"], "version": plan["version"], "receiptSHA": plan["receiptSHA"],
        "installedRoot": plan["contentPath"], "files": len(plan["files"]),
        "bytes": sum(row["size"] for row in plan["files"]),
        "subscriptionVerified": False, "freshTransferVerified": False, "delivered": False,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--installed-root", required=True)
    parser.add_argument("--receipt", required=True)
    parser.add_argument("--receipt-sha", required=True)
    parser.add_argument("--mode", choices=("test", "alpha"), required=True)
    parser.add_argument("--expected-item", required=True)
    parser.add_argument("--expected-version", required=True)
    args = parser.parse_args(argv)
    try:
        result = verify_installed_bytes(args.installed_root, args.receipt, args.receipt_sha,
                                        args.mode, args.expected_item, args.expected_version)
    except (upload.ValidationError, OSError, UnicodeError) as error:
        print(f"Workshop installed bytes refused: {error}", file=sys.stderr)
        return 2
    print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
