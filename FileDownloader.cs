using System;
using System.IO;
using System.Net;
using System.Threading;

namespace MinecraftServerSetup
{
    public class FileDownloader
    {
        Thread thread;
        string source, dest;
        object threadLock = new object();
        ProgressInfo progress = new ProgressInfo();

        public FileDownloader(string fileWebSource, string fileLocalDest)
        {
            source = fileWebSource;
            dest = fileLocalDest;
        }

        public bool IsDownloading()
        {
            return thread.ThreadState == ThreadState.Running;
        }

        public AsyncProgressResult DownloadAsync()
        {
            thread = new Thread( new ThreadStart(() => Download()) );

            progress.Reset();
            thread.Start();

            return new AsyncProgressResult(ref progress);
        }

        public void Download()
        {
            WebRequest request;
            WebResponse response;
            Stream responseStream;
            FileStream fileOut;

            progress.Reset();
            long len = 1;
            long pos = 0;


            try
            {
                request = WebRequest.Create(source);
                response = request.GetResponse();
                len = response.ContentLength;
                responseStream = response.GetResponseStream();
            }
            catch (Exception e)
            {
                throw new IOException(string.Format("Failed to download the file at: {0}\n- Message:\n{1}",
                        source,
                        e.ToString()));
            }

            try
            {
                fileOut = File.OpenWrite(dest);
            }
            catch (Exception e)
            {
                throw new IOException(string.Format("Failed to create/open file: {0}\n- Message:\n{1}",
                        dest,
                        e.ToString()));
            }
            byte[] buffer = new byte[4096];
            int numRead = 0;
            while ( (numRead = responseStream.Read(buffer, 0, 4096)) > 0)
            {
                fileOut.Write(buffer, 0, numRead);

                pos += numRead;
                Progress = (int)(pos / (float)len * 100.0f);
            }

            responseStream.Close();

            fileOut.Flush();
            fileOut.Close();
        }

        public int Progress
        {
            get
            {
                lock (progress)
                {
                    return (int)progress.progress;
                }
            }
            private set
            {
                lock (threadLock)
                {
                    progress.SetProgressOutOf100(value);
                }
            }
        }

        public bool IsCompleted
        {
            get
            {
                return Progress == 100 && !IsDownloading();
            }
        }
    }
}
