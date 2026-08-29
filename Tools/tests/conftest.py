"""Session-wide guard: no test in this directory may write into the repository tree.

The previous guard was `setUpModule`/`tearDownModule`, which is MODULE-wide — four other modules run
after its teardown, so a write from any of them was outside the window that was supposed to catch it.
A guard that only watches one module is a guard with a documented blind spot.

Scoped to a session fixture so it brackets the whole run regardless of module order or selection.
"""

from __future__ import annotations

import pytest

import conftest_guard


@pytest.fixture(scope="session", autouse=True)
def repository_is_left_unchanged():
    """A control that can damage what it audits is not a control."""
    before = conftest_guard.sweep()
    yield
    conftest_guard.assert_unchanged(before)
