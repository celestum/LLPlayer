﻿using System.Threading;

using static FlyleafLib.Logger;

namespace FlyleafLib.MediaFramework;

public abstract class RunThreadBase : NotifyPropertyChanged
{
    Status _Status = Status.Stopped;
    public Status               Status          {
        get => _Status;
        set
        {
            lock (lockStatus)
            {
                if (CanDebug && _Status != Status.QueueFull && value != Status.QueueFull && _Status != Status.QueueEmpty && value != Status.QueueEmpty)
                    Log.Debug($"{_Status} -> {value}");

                _Status = value;
            }
        }
    }
    public bool                 IsRunning       {
        get
        {
            bool ret = false;
            lock (lockStatus) ret = thread != null && thread.IsAlive && Status != Status.Paused;
            return ret;
        }
    }

    public bool                 CriticalArea    { get; protected set; }
    public bool                 Disposed        { get; protected set; } = true;
    public int                  UniqueId        { get; protected set; } = -1;
    public bool                 PauseOnQueueFull{ get; set; }

    protected Thread            thread;
    protected AutoResetEvent    threadARE       = new(false);
    protected string            threadName      {
        get => _threadName;
        set
        {
            _threadName = value;
            Log = new LogHandler(("[#" + UniqueId + "]").PadRight(8, ' ') + $" [{threadName}] ");
        }
    }
    string _threadName;

    internal LogHandler         Log;
    internal object             lockActions     = new();
    internal object             lockStatus      = new();

    public RunThreadBase(int uniqueId = -1)
        => UniqueId = uniqueId == -1 ? Utils.GetUniqueId() : uniqueId;

    public void Pause()
    {
        lock (lockActions)
        {
            lock (lockStatus)
            {
                PauseOnQueueFull = false;

                if (Disposed || thread == null || !thread.IsAlive || Status == Status.Stopping || Status == Status.Stopped || Status == Status.Ended || Status == Status.Pausing || Status == Status.Paused) return;
                Status = Status.Pausing;
            }
            while (Status == Status.Pausing) Thread.Sleep(5);
        }
    }
    public void Start()
    {
        lock (lockActions)
        {
            int retries = 1;
            while (thread != null && thread.IsAlive && CriticalArea)
            {
                Thread.Sleep(5); // use small steps to re-check CriticalArea (demuxer can have 0 packets again after processing the received ones)
                retries++;
                if (retries > 16)
                {
                    if (CanTrace) Log.Trace($"Start() exhausted");
                    return;
                }
            }

            lock (lockStatus)
            {
                if (Disposed) return;

                PauseOnQueueFull = false;

                if (Status == Status.Draining) while (Status != Status.Draining) Thread.Sleep(3);
                if (Status == Status.Stopping) while (Status != Status.Stopping) Thread.Sleep(3);
                if (Status == Status.Pausing)  while (Status != Status.Pausing)  Thread.Sleep(3);

                if (Status == Status.Ended) return;

                if (Status == Status.Paused)
                {
                    threadARE.Set();
                    while (Status == Status.Paused) Thread.Sleep(3);
                    return;
                }

                if (thread != null && thread.IsAlive) return; // might re-check CriticalArea

                thread = new Thread(() => Run());
                Status = Status.Running;

                thread.Name = $"[#{UniqueId}] [{threadName}]"; thread.IsBackground= true; thread.Start();
                while (!thread.IsAlive) { if (CanTrace) Log.Trace("Waiting thread to come up"); Thread.Sleep(3); }
            }
        }
    }
    public void Stop()
    {
        lock (lockActions)
        {
            lock (lockStatus)
            {
                PauseOnQueueFull = false;

                if (Disposed || thread == null || !thread.IsAlive || Status == Status.Stopping || Status == Status.Stopped || Status == Status.Ended) return;
                if (Status == Status.Pausing) while (Status != Status.Pausing) Thread.Sleep(3);
                Status = Status.Stopping;
                threadARE.Set();
            }

            while (Status == Status.Stopping && thread != null && thread.IsAlive) Thread.Sleep(5);
        }
    }

    protected void Run()
    {
        if (CanDebug) Log.Debug($"Thread started ({Status})");

        do
        {
            RunInternal();

            if (Status == Status.Pausing)
            {
                threadARE.Reset();
                Status = Status.Paused;
                threadARE.WaitOne();
                if (Status == Status.Paused)
                {
                    if (CanDebug) Log.Debug($"{_Status} -> {Status.Running}");
                    _Status = Status.Running;
                }
            }

        } while (Status == Status.Running);

        if (Status != Status.Ended) Status = Status.Stopped;

        if (CanDebug) Log.Debug($"Thread stopped ({Status})");
    }
    protected abstract void RunInternal();
}

public enum Status
{
    Opening,

    Stopping,
    Stopped,

    Pausing,
    Paused,

    Running,
    QueueFull,
    QueueEmpty,
    Draining,

    Ended
}
