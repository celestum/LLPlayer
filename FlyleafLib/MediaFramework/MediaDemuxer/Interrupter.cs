﻿using System.Diagnostics;

using static FlyleafLib.Logger;

namespace FlyleafLib.MediaFramework.MediaDemuxer;

public unsafe class Interrupter
{
    public int          ForceInterrupt  { get; set; }
    public Requester    Requester       { get; private set; }
    public int          Interrupted     { get; private set; }
    public bool         Timedout        { get; private set; }

    Demuxer demuxer;
    Stopwatch sw = new();
    internal AVIOInterruptCB_callback interruptClbk;
    long curTimeoutMs;

    internal int ShouldInterrupt(void* opaque)
    {
        if (demuxer.Status == Status.Stopping)
        {
            if (CanDebug) demuxer.Log.Debug($"{Requester} Interrupt (Stopping) !!!");

            return Interrupted = 1;
        }

        if (demuxer.Config.AllowTimeouts && sw.ElapsedMilliseconds > curTimeoutMs)
        {
            if (Timedout)
                return Interrupted = 1;

            if (CanWarn) demuxer.Log.Warn($"{Requester} Timeout !!!! {sw.ElapsedMilliseconds} ms");

            Timedout    = true;
            Interrupted = 1;
            demuxer.OnTimedOut();

            return Interrupted;
        }

        if (Requester == Requester.Close)
            return 0;

        if (ForceInterrupt != 0 && demuxer.allowReadInterrupts)
        {
            if (CanTrace) demuxer.Log.Trace($"{Requester} Interrupt !!!");

            return Interrupted = 1;
        }

        return Interrupted = 0;
    }

    public Interrupter(Demuxer demuxer)
    {
        this.demuxer    = demuxer;
        interruptClbk   = ShouldInterrupt;
    }

    public void ReadRequest()
    {
        Requester   = Requester.Read;

        if (!demuxer.Config.AllowTimeouts)
            return;

        Timedout    = false;
        curTimeoutMs= demuxer.IsLive ? demuxer.Config.readLiveTimeoutMs: demuxer.Config.readTimeoutMs;
        sw.Restart();
    }

    public void SeekRequest()
    {
        Requester   = Requester.Seek;

        if (!demuxer.Config.AllowTimeouts)
            return;

        Timedout    = false;
        curTimeoutMs= demuxer.Config.seekTimeoutMs;
        sw.Restart();
    }

    public void OpenRequest()
    {
        Requester   = Requester.Open;

        if (!demuxer.Config.AllowTimeouts)
            return;

        Timedout    = false;
        curTimeoutMs= demuxer.Config.openTimeoutMs;
        sw.Restart();
    }

    public void CloseRequest()
    {
        Requester   = Requester.Close;

        if (!demuxer.Config.AllowTimeouts)
            return;

        Timedout    = false;
        curTimeoutMs= demuxer.Config.closeTimeoutMs;
        sw.Restart();
    }
}

public enum Requester
{
    Close,
    Open,
    Read,
    Seek
}
