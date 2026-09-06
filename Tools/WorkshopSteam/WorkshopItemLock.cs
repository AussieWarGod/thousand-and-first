using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace ThousandAndFirst.WorkshopSteam
{
    /// <summary>Windows-only, thread-affine exclusion for the two compiled-in Workshop items.
    /// Acquire waits at most 5,000 kernel-wait milliseconds; it never reads or writes release state.
    /// A held lease proves exclusion only, not authentication, retry, submission or finalization rights.
    /// Abandoned reports an observed WAIT_ABANDONED, not durable crash history: a named object can
    /// disappear when its last handle closes. The registry must independently retain uncertainty.</summary>
    public sealed class WorkshopItemLock : IDisposable
    {
        public const ulong StagingItem = 3796495680UL;
        public const ulong PublicItem = 3794797472UL;
        private const uint WaitMilliseconds = 5000, Acquired = 0, AbandonedWait = 0x80, TimedOut = 0x102;
        private static readonly object ProcessGate = new object();
        private static readonly HashSet<ulong> Reserved = new HashSet<ulong>();
        private readonly object gate = new object();
        private readonly Thread ownerThread;
        private readonly uint ownerNativeThread;
        private IntPtr handle;
        private bool disposed;
        private Exception cleanupFailure;

        public ulong Item { get; }
        public bool Abandoned { get; }

        private WorkshopItemLock(ulong item, IntPtr native, bool abandoned, uint thread)
        {
            Item = item; handle = native; Abandoned = abandoned;
            ownerThread = Thread.CurrentThread; ownerNativeThread = thread;
        }

        /// <summary>Rejects unknown items before any native call. Local contenders/reentry refuse
        /// immediately; another process may hold the fixed mutex for the bounded wait. Timeout,
        /// native errors and cleanup errors throw. Abandonment returns a held lease with its signal set.
        /// Keep this lease on the acquiring thread; never carry ownership across an async continuation.</summary>
        public static WorkshopItemLock Acquire(ulong item)
        {
            ValidateItem(item);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("Workshop item locks require Windows.");
            uint thread = GetCurrentThreadId();
            lock (ProcessGate)
                if (!Reserved.Add(item))
                    throw new InvalidOperationException("This process already owns, awaits, or has failed cleanup for this item.");
            IntPtr native = IntPtr.Zero;
            bool owned = false;
            try
            {
                string name = item == StagingItem ? @"Global\taf-workshop-item-3796495680"
                    : @"Global\taf-workshop-item-3794797472";
                native = CreateMutexW(IntPtr.Zero, false, name);
                if (native == IntPtr.Zero) throw NativeError("CreateMutexW");
                uint result = WaitForSingleObject(native, WaitMilliseconds);
                owned = result == Acquired || result == AbandonedWait;
                if (result == TimedOut) throw new TimeoutException("Workshop item mutex acquisition timed out after 5000ms.");
                if (!owned)
                {
                    if (result == uint.MaxValue) throw NativeError("WaitForSingleObject");
                    throw new InvalidOperationException("Unexpected Workshop mutex wait result: " + result);
                }
                return new WorkshopItemLock(item, native, result == AbandonedWait, thread);
            }
            catch (Exception error)
            {
                Exception cleanup = ReleaseAndClose(native, owned);
                if (cleanup == null) { lock (ProcessGate) Reserved.Remove(item); }
                if (cleanup != null) throw new AggregateException("Workshop item acquisition and cleanup failed.", error, cleanup);
                throw;
            }
        }

        /// <summary>True only on the owning thread while this lease remains live. Not release rights.</summary>
        public bool IsHeld
        {
            get { lock (gate) return !disposed && cleanupFailure == null && OnOwnerThread(); }
        }

        /// <summary>Checks exact item, live lease and acquiring thread; throws on any mismatch.
        /// This does not interpret or clear Abandoned and must not substitute for release policy.</summary>
        public void RequireHeld(ulong expectedItem)
        {
            ValidateItem(expectedItem);
            lock (gate)
            {
                if (expectedItem != Item) throw new InvalidOperationException("Workshop item lease identity differs.");
                if (cleanupFailure != null) throw new InvalidOperationException("Workshop item cleanup previously failed.", cleanupFailure);
                if (disposed) throw new ObjectDisposedException(nameof(WorkshopItemLock));
                RequireOwnerThread();
            }
        }

        /// <summary>Owner-thread-only release, then close; repeated successful disposal is harmless.
        /// Wrong-thread disposal refuses without changing ownership. Any native cleanup failure is
        /// surfaced and permanently blocks local reacquisition; repeated disposal reports that failure.
        /// No finalizer releases thread-owned authority from the unrelated finalizer thread.</summary>
        public void Dispose()
        {
            lock (gate)
            {
                RequireOwnerThread();
                if (cleanupFailure != null) throw new InvalidOperationException("Workshop item cleanup previously failed.", cleanupFailure);
                if (disposed) return;
                disposed = true;
                cleanupFailure = ReleaseAndClose(handle, true);
                handle = IntPtr.Zero;
                if (cleanupFailure != null) throw new InvalidOperationException("Workshop item mutex cleanup failed.", cleanupFailure);
                lock (ProcessGate) Reserved.Remove(Item);
            }
        }

        private bool OnOwnerThread()
        { return ReferenceEquals(Thread.CurrentThread, ownerThread) && GetCurrentThreadId() == ownerNativeThread; }

        private void RequireOwnerThread()
        {
            if (!OnOwnerThread()) throw new InvalidOperationException("Workshop item lease belongs to another thread.");
        }

        private static void ValidateItem(ulong item)
        {
            if (item != StagingItem && item != PublicItem)
                throw new ArgumentOutOfRangeException(nameof(item), "Unknown Workshop item; native access refused.");
        }

        private static Exception ReleaseAndClose(IntPtr native, bool owned)
        {
            if (native == IntPtr.Zero) return null;
            Exception release = null, close = null;
            try { if (owned && !ReleaseMutex(native)) release = NativeError("ReleaseMutex"); }
            catch (Exception error) { release = error; }
            try { if (!CloseHandle(native)) close = NativeError("CloseHandle"); }
            catch (Exception error) { close = error; }
            if (release != null && close != null) return new AggregateException(release, close);
            return release ?? close;
        }

        private static Win32Exception NativeError(string operation)
        { return new Win32Exception(Marshal.GetLastWin32Error(), operation + " failed for Workshop item mutex."); }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateMutexW(IntPtr attributes, [MarshalAs(UnmanagedType.Bool)] bool initialOwner, string name);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseMutex(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern uint GetCurrentThreadId();
    }
}
