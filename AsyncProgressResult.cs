using System;
using System.Collections.Generic;
using System.Threading;

namespace MinecraftServerSetup
{
    public enum ProgressInfoState: byte
    {
        Error, Running, Complete
    }
    public class ProgressInfo
    {
        public byte state = 0;
        public float progress = 0;
        public List<Exception> errors = new List<Exception>();

        

        public void Reset()
        {
            this.errors.Clear();
            this.SetProgressOutOf100(0.0f);
            this.SetState(ProgressInfoState.Running);
        }

        public byte SetState(ProgressInfoState state)
        {
            return this.state = (byte)state;
        }

        public float SetProgressOutOf100(float progress)
        {
            this.progress = progress;
            if (this.progress >= 100.0f)
                this.SetState(ProgressInfoState.Complete);
            return this.progress;
        }

        public bool HasError()
        {
            return errors.Count > 0;
        }

        public void AddError(Exception err)
        {
            this.errors.Add(err);
            this.SetState(ProgressInfoState.Error);
        }
    }

    public class AsyncProgressResult : IAsyncResult
    {
        ProgressInfo progress;
        ManualResetEvent asyncWaitHandle;

        public AsyncProgressResult(ref ProgressInfo progress)
        {
            this.progress = progress;
        }

        public ProgressInfo AsyncState
        {
            get { return progress; }
        }
        object IAsyncResult.AsyncState
        {
            get
            {
                return AsyncState;
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (asyncWaitHandle == null)
                {
                    bool done = IsCompleted;
                    ManualResetEvent mre = new ManualResetEvent(done);
                    if (Interlocked.CompareExchange(ref asyncWaitHandle,
                        mre, null) != null)
                    {
                        mre.Close();
                    }

                    else
                    {
                        if (!done && IsCompleted)
                        {
                            asyncWaitHandle.Set();
                        }
                    }
                }
                return asyncWaitHandle;
            }
        }

        public bool CompletedSynchronously
        {
            get { return false; }
        }

        public bool IsCompleted
        {
            get { return Thread.VolatileRead(ref progress.state) == (byte)ProgressInfoState.Complete; }
        }
    }
}
