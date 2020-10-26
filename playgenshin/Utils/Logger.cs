﻿// Copyright (C) 2016-2018 madalien.com
// This file is part of bnetlauncher.
//
// bnetlauncher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// bnetlauncher is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with bnetlauncher. If not, see <http://www.gnu.org/licenses/>.
//
//
// Contact:
// daf <daf@madalien.com>

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>
/// TODO:
/// - add old log cleanup/trimming code
/// - make code more robust?
/// - something i'm probably forgeting
/// </summary>

namespace bnetlauncher.Utils
{
    public static class Logger
    {

        /// <summary>
        /// File version information for bnetlauncher executable
        /// </summary>
        internal static FileVersionInfo VersionInfo
        {
            get
            {
                return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            }
        }

        internal static string DataPath
        {
            get
            {
                // For some insane reason FileDescription has the assembly name.
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), VersionInfo.CompanyName, VersionInfo.FileDescription);
            }
        }

        public static bool OutputToFile { get; set; }
        public static bool OutPutToConsole { get; set; }

        /// <summary>
        /// Readonly structure contained all the information for a single log entry
        /// </summary>
        private struct LogEntry
        {
            public readonly string message;
            public readonly Exception ex;
            public readonly string srcPath;
            public readonly string srcMember;
            public readonly int srcLine;
            public readonly string type;

            public LogEntry(string message, Exception ex, string srcPath, string srcMember, int srcLine, string type = "inf")
            {
                this.message = message;
                this.ex = ex;
                this.srcPath = srcPath;
                this.srcMember = srcMember;
                this.srcLine = srcLine;
                this.type = type;
            }
        }

        /// <summary>
        /// File in which the log file will be writen, one per day
        /// </summary>
        private static readonly string log_file = Path.Combine(DataPath,
            $"debug_{Process.GetCurrentProcess().StartTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");

        /// <summary>
        /// Log Entry colection that serves as a queue for the log worker.
        /// </summary>
        private static BlockingCollection<LogEntry> log_queue = new BlockingCollection<LogEntry>();

        /// <summary>
        /// Log worker thread global needed to wait for it to flush.
        /// </summary>
        private static Thread logger_worker;

        /// <summary>
        /// Checks which logs are enabled and starts the worker thread and hooks up the flush on exit.
        /// </summary>
#pragma warning disable CA1810 // Initialize reference type static fields inline
        static Logger()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            OutPutToConsole = Environment.UserInteractive;
            OutputToFile = File.Exists(Path.Combine(DataPath, "enablelog")) ||
                File.Exists(Path.Combine(DataPath, "enablelog.txt")) ||
                File.Exists(Path.Combine(DataPath, "enablelog.txt.txt"));

            // creates the worker thread as background so it doesn't keep the client open
            // when main thread exits.
            logger_worker = new Thread(LoggerWorker);
            logger_worker.IsBackground = true;
            logger_worker.Start();

            // Hooks into the process exit so we can flush the log_queue
            // using a lambda operator to discard the parameters that aren't needed.
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => FlushQueue();
        }

        /// <summary>
        /// Flushes the log entry queue by marking it as complete and waiting for all messages to be processed.
        /// This is automaticly called on ProcessExit.
        /// </summary>
        public static void FlushQueue()
        {
            log_queue.CompleteAdding();
            logger_worker.Join();
        }

        /// <summary>
        /// Writes an Information message to the log
        /// </summary>
        /// <param name="message">Message to write</param>
        public static void Information(string message,
            [CallerFilePath] string srcPath = "", [CallerMemberName] string srcMember = "", [CallerLineNumber] int srcLine = 0)
        {
            log_queue.Add(new LogEntry(message, null, srcPath, srcMember, srcLine, "INFO"));
        }

        /// <summary>
        /// Writes an Warning message to the log
        /// </summary>
        /// <param name="message">Message to write</param>
        /// <param name="ex">Optional Exception object to show after the log message</param>
        public static void Warning(string message, Exception ex = null,
            [CallerFilePath] string srcPath = "", [CallerMemberName] string srcMember = "", [CallerLineNumber] int srcLine = 0)
        {
            log_queue.Add(new LogEntry(message, ex, srcPath, srcMember, srcLine, "WARN"));
        }

        /// <summary>
        /// Writes an Error message to the log
        /// </summary>
        /// <param name="message">Message to write</param>
        /// <param name="ex">Optional Exception object to show after the log message</param>
        public static void Error(string message, Exception ex = null,
            [CallerFilePath] string srcPath = "", [CallerMemberName] string srcMember = "", [CallerLineNumber] int srcLine = 0)
        {
            log_queue.Add(new LogEntry(message, ex, srcPath, srcMember, srcLine, "ERROR"));
        }


        /// <summary>
        /// Working thread that consumes the log entry queue
        /// </summary>
        private static void LoggerWorker()
        {
            while (!log_queue.IsCompleted)
            {
                try
                {
                    // Tries to take another message from the queue
                    // and blocks if the queue is empty.
                    var msg = log_queue.Take();

                    if (OutPutToConsole)
                    {
                        // writes console message first because less likely to be delayed.
                        ConsoleWrite(msg);
                    }
                    if (OutputToFile)
                    {
                        FileWrite(msg);
                    }
                }
                catch (InvalidOperationException) { }
            }
        }

        /// <summary>
        /// Write a LogEntry to a log file.
        /// If the file is locked by another process it will keep retrying until successful write.
        /// </summary>
        /// <param name="le">LogEntry to write.</param>
        private static void FileWrite(LogEntry le)
        {
            var line = $"{DateTime.Now.ToString("HH:mm:ss.ffff", CultureInfo.InvariantCulture)}|{Process.GetCurrentProcess().Id}|" +
                $"{Path.GetFileNameWithoutExtension(le.srcPath)}.{le.srcMember}:{le.srcLine}|{le.type}|{le.message}";

            while (true) // endless retry loop
            {
                try
                {
                    using (var file = new StreamWriter(log_file, true))
                    {
                        file.WriteLine(line);
                        if (le.ex != null)
                        {
                            file.WriteLine(le.ex.ToString());
                        }
                    }
                    return; // if we reach here we're done
                }
                // failed to get access to the file so try again
                catch (IOException) { }
            }
        }

        /// <summary>
        /// Writes LogEntry in the console using colors
        /// </summary>
        /// <param name="le">LogEntry to write.</param>
        private static void ConsoleWrite(LogEntry le)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(DateTime.Now.ToString("HH:mm:ss.ffff", CultureInfo.InvariantCulture));

            Console.ResetColor(); Console.Write("|");

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(Process.GetCurrentProcess().Id);

            Console.ResetColor(); Console.Write("|");

            switch (le.type)
            {
                case "error":
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case "warn":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
            }

            Console.Write($"{Path.GetFileNameWithoutExtension(le.srcPath)}.{le.srcMember}:{le.srcLine}|{le.type}");

            Console.ResetColor(); Console.Write("|");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(le.message);

            if (le.ex != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(le.ex.ToString());
            }

            Console.ResetColor();
        }
    }
}
