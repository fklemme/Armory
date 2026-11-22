using System;
using System.Collections.Generic;
using System.IO;
using Armory.Compatibility;
using RimWorld;
using UnityEngine;
using Verse;

namespace Armory
{
    /// <summary>
    /// Persistent, rolling diagnostic log. Player.log is overwritten on every launch and
    /// becomes huge with a full mod list, so support problems need a small file that
    /// survives the session. One file is written per session under ArmoryLogs next to the
    /// save data; the newest five sessions are kept.
    /// </summary>
    public static class ArmoryDiagnostics
    {
        private const int KeepFiles = 5;
        private const long MaxBytesPerFile = 4L * 1024L * 1024L;

        private static readonly object Gate = new object();
        private static string folderPath;
        private static string sessionPath;
        private static bool disabled;
        private static bool headerWritten;

        public static bool Enabled
        {
            get
            {
                return !disabled && ArmoryMod.Settings != null &&
                       ArmoryMod.Settings.diagnosticLogging;
            }
        }

        public static string LogFolderPath
        {
            get
            {
                if (folderPath == null)
                {
                    folderPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "ArmoryLogs");
                }
                return folderPath;
            }
        }

        public static string CurrentWorldName
        {
            get
            {
                try
                {
                    return Find.World == null || Find.World.info == null
                        ? "none"
                        : Find.World.info.name;
                }
                catch (Exception)
                {
                    return "none";
                }
            }
        }

        public static void StartSession()
        {
            if (!Enabled)
            {
                return;
            }
            lock (Gate)
            {
                EnsureSessionFile();
            }
        }

        public static void Write(string category, string message)
        {
            if (!Enabled || string.IsNullOrEmpty(message))
            {
                return;
            }
            lock (Gate)
            {
                try
                {
                    EnsureSessionFile();
                    if (sessionPath == null)
                    {
                        return;
                    }

                    FileInfo file = new FileInfo(sessionPath);
                    if (file.Exists && file.Length >= MaxBytesPerFile)
                    {
                        return;
                    }

                    string tick = "-";
                    if (Find.TickManager != null)
                    {
                        tick = Find.TickManager.TicksGame.ToString();
                    }
                    File.AppendAllText(sessionPath, string.Format(
                        "{0} tick={1} [{2}] {3}{4}",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        tick,
                        category,
                        message,
                        Environment.NewLine));
                }
                catch (Exception exception)
                {
                    disabled = true;
                    Log.Warning("[Armory] persistent diagnostic log disabled after a write " +
                                "failure: " + exception.GetType().Name + ": " + exception.Message);
                }
            }
        }

        public static void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(LogFolderPath);
                Application.OpenURL(LogFolderPath);
            }
            catch (Exception exception)
            {
                Messages.Message("Could not open the Armory log folder: " + exception.Message,
                    MessageTypeDefOf.RejectInput, false);
            }
        }

        private static void EnsureSessionFile()
        {
            if (sessionPath != null)
            {
                WriteHeader();
                return;
            }

            try
            {
                Directory.CreateDirectory(LogFolderPath);
                PruneOldFiles();
                sessionPath = Path.Combine(LogFolderPath, string.Format(
                    "Armory-{0}-{1}.log",
                    DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"),
                    System.Diagnostics.Process.GetCurrentProcess().Id));
                WriteHeader();
            }
            catch (Exception exception)
            {
                disabled = true;
                Log.Warning("[Armory] persistent diagnostic log disabled after startup " +
                            "failure: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void WriteHeader()
        {
            if (headerWritten || sessionPath == null)
            {
                return;
            }
            headerWritten = true;

            string modVersion = "unknown";
            try
            {
                ModMetaData meta = ArmoryMod.Instance == null
                    ? null
                    : ArmoryMod.Instance.Content.ModMetaData;
                if (meta != null)
                {
                    modVersion = meta.ModVersion;
                }
            }
            catch (Exception)
            {
                // The version is diagnostic only; never fail the session for it.
            }

            string worldName = CurrentWorldName;

            ArmorySettings settings = ArmoryMod.Settings;
            string settingSummary = settings == null
                ? "not loaded"
                : string.Format(
                    "auto={0}, sidearms={1}, replace={2}, align={3}, upgrade={4}%, cooldown={5}, swap={6}, manual={7}, respectSS={8}",
                    settings.autoEquipEnabled,
                    settings.manageSidearms,
                    settings.replaceWorseSidearms,
                    settings.alignSimpleSidearms,
                    (int)(settings.upgradeThreshold * 100f),
                    settings.plannerIntervalTicks,
                    settings.swapCooldownTicks,
                    settings.respectManualEquip,
                    settings.respectSimpleSidearms);

            File.AppendAllText(sessionPath, string.Format(
                "=== Armory {0} diagnostic log ==={1}" +
                "RimWorld {2}{1}" +
                "Simple Sidearms: {3}{1}" +
                "World: {4}{1}" +
                "Settings: {5}{1}" +
                "Started: {6}{1}{1}",
                modVersion,
                Environment.NewLine,
                VersionControl.CurrentVersionStringWithRev,
                SimpleSidearmsAdapter.Active ? "active" : "inactive",
                worldName,
                settingSummary,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        }

        private static void PruneOldFiles()
        {
            string[] files = Directory.GetFiles(LogFolderPath, "Armory-*.log");
            if (files.Length < KeepFiles)
            {
                return;
            }

            List<FileInfo> ordered = new List<FileInfo>();
            for (int i = 0; i < files.Length; i++)
            {
                ordered.Add(new FileInfo(files[i]));
            }
            ordered.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = KeepFiles - 1; i < ordered.Count; i++)
            {
                try
                {
                    ordered[i].Delete();
                }
                catch (Exception)
                {
                    // A locked old log is harmless; pruning runs again next session.
                }
            }
        }
    }
}
