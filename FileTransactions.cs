using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace MinecraftServerSetup
{
    public class FileTransactionScheduler
    {
        Queue<FileTransactionEntry> schedule;
        ProgressInfo progress;


        public FileTransactionScheduler()
        {
            schedule = new Queue<FileTransactionEntry>();
            progress = new ProgressInfo();
        }

        public IReadOnlyCollection<Exception> Errors { get { return progress.errors as IReadOnlyCollection<Exception>; } }


        public AsyncProgressResult BeginTransations()
        {
            progress.Reset();

            Thread th = new Thread(new ThreadStart(DoTransactions));
            th.Start();

            return new AsyncProgressResult(ref progress);
        }

        public void DoTransactions()
        {
            long len = GetTotalSize();
            long moved = 0;

            lock (progress)
                progress.Reset();

            while(schedule.Count > 0)
            {
                var item = schedule.Dequeue();
                try
                {
                    item.DoTransaction();
                    moved += item.GetSize();
                    lock (progress)
                        progress.SetProgressOutOf100(moved / (float)len * 100);
                }
                catch (Exception e)
                {
                    lock (progress)
                        progress.AddError(e);
                }
            }

            lock(progress)
                progress.SetProgressOutOf100(100.0f);
        }

        public long GetTotalSize()
        {
            long size = 0;
            foreach(var item in schedule)
            {
                size += item.GetSize();
            }
            return size;
        }

        public void ScheduleFileMove(string source, string dest, bool optional = false)
        {
            schedule.Enqueue(new FileTransactionEntry { Source = source, Destination = dest, Transaction = TransactionType.Move, Optional = optional });
        }
        public void ScheduleFileMove(FileInfo source, string dest, bool optional = false)
        {
            ScheduleFileMove(new FileInfo(source.FullName), dest, optional);
        }

        public void ScheduleFileCopy(string source, string dest, bool optional = false)
        {
            schedule.Enqueue(new FileTransactionEntry { Source = source, Destination = dest, Transaction = TransactionType.Copy, Optional = optional });
        }
        public void ScheduleFileCopy(FileInfo source, string dest, bool optional = false)
        {
            ScheduleFileCopy(new FileInfo(source.FullName), dest, optional);
        }


        public void ScheduleFileDownload(string source, string dest)
        {
            schedule.Enqueue(new FileTransactionEntry { Source = source, Destination = dest, Transaction = TransactionType.Download });
        }

        public void ScheduleDirectoryMove(string source, string dest, bool optional = false)
        {
            schedule.Enqueue(new FileTransactionEntry { Source = source, Destination = dest, Transaction = TransactionType.Move, Optional = optional });
        }
        public void ScheduleDirectoryMove(DirectoryInfo source, string dest, bool optional = false)
        {
            ScheduleDirectoryMove(new DirectoryInfo(source.FullName), dest, optional);
        }

        public void ScheduleDirectoryCopy(string source, string dest, bool optional = false)
        {
            schedule.Enqueue(new FileTransactionEntry { Source = source, Destination = dest, Transaction = TransactionType.Copy, Optional = optional });
        }
        public void ScheduleDirectoryCopy(DirectoryInfo source, string dest, bool optional = false)
        {
            ScheduleDirectoryCopy(new DirectoryInfo(source.FullName), dest, optional);
        }


        public void ScheduleFileDelete(string source, bool optional = false)
        {
            schedule.Enqueue(new FileTransactionEntry { Source = source, Transaction = TransactionType.Delete, Optional = optional });
        }
        public void ScheduleFileDelete(FileInfo source, bool optional = false)
        {
            ScheduleFileDelete(source.FullName, optional);
        }

        public void ScheduleDirectoryDelete(string source, bool optional = false)
        {
            schedule.Enqueue(new FileTransactionEntry { Source = source, Transaction = TransactionType.Delete, Optional = optional });
        }
        public void ScheduleDirectoryDelete(DirectoryInfo source, bool optional = false)
        {
            ScheduleDirectoryDelete(source.FullName, optional);
        }
    }

    enum TransactionType
    {
        Error, Copy, Move, Download, Delete
    }
    class FileTransactionEntry
    {
        long size = -1;

        public FileTransactionEntry()
        {
        }

        public string Source { get; set; }
        public string Destination { get; set; }
        public bool IsDirectory { get { return Directory.Exists(Source); } }
        public TransactionType Transaction { get; set; }
        public bool Optional { get; set; }


        public void DoTransaction()
        {
            switch(Transaction)
            {
                case TransactionType.Copy:
                    DoCopy();
                    break;
                case TransactionType.Move:
                    DoMove();
                    break;
                case TransactionType.Download:
                    DoDownload();
                    break;
                case TransactionType.Delete:
                    DoDelete();
                    break;
                default:
                    throw new InvalidOperationException("Transaction type not set to a valid value");
            }
        }

        public void DoDelete()
        {
            if (IsDirectory)
                DeleteDirectory();
            else
                DeleteFile();
        }

        public void DeleteDirectory()
        {
            var dir = SourceAsDirectory();
            
            if (Optional && !dir.Exists)
                return;

            dir.Delete(true);
        }

        public void DeleteFile()
        {
            var file = SourceAsFile();
            
            if (Optional && !file.Exists)
                return;

            file.Delete();
        }

        public void DoDownload()
        {
            if (!IsDirectory)
                DownloadFile();
        }

        public void DownloadFile()
        {
            var download = new FileDownloader(Source, Destination);
            download.Download();
        }

        public void DoMove()
        {
            if (IsDirectory)
                MoveDirectory();
            else
                MoveFile();
        }

        void MoveFile()
        {
            var file = SourceAsFile();

            if (Optional && !file.Exists)
                return;

            file.MoveTo(Destination);
        }

        void MoveDirectory()
        {
            var dir = SourceAsDirectory();
            
            if (Optional && !dir.Exists)
                return;

            dir.MoveTo(Destination);
        }

        public void DoCopy()
        {
            if (IsDirectory)
                CopyDirectory();
            else
                CopyFile();
        }

        void CopyFile()
        {
            var file = SourceAsFile();

            if (Optional && !file.Exists)
                return;

            file.CopyTo(Destination);
        }

        void CopyDirectory()
        {
            var dir = SourceAsDirectory();

            if (Optional && !dir.Exists)
                return;

            dir.CopyTo(Destination);
        }

        public long GetSize()
        {
            if(Transaction == TransactionType.Download)
                return GetDownloadSize();

            if (IsDirectory)
                return GetDirectorySize();

            if (size > -1)
                return size;
            var file = SourceAsFile();

            if (!file.Exists)
                return 0;

            return size = file.Length;
        }

        long GetDirectorySize()
        {
            if (size != -1)
                return size;

            size = 0;

            IEnumerable<FileInfo> files;
            IEnumerable<DirectoryInfo> dirs;

            var allSubDirs = new Stack<DirectoryInfo>();
            allSubDirs.Push(SourceAsDirectory());

            while(allSubDirs.Count > 0)
            {
                var dir = allSubDirs.Pop();
                files = dir.GetFiles();
                foreach (var f in files)
                    size += f.Length;

                dirs = dir.GetDirectories();
                foreach (var d in dirs)
                    allSubDirs.Push(d);
            }

            return size;
        }

        long GetDownloadSize()
        {
            if (size > -1)
                return size;

            var web = new WebClient();
            web.OpenRead(Source);

            if (!long.TryParse(web.ResponseHeaders.Get("Content-Length"), out size))
                size = 0;
            web.Dispose();

            return size;
        }

        public FileInfo SourceAsFile()
        {
            return new FileInfo(Source);
        }
        public DirectoryInfo SourceAsDirectory()
        {
            return new DirectoryInfo(Source);
        }
    }
}
