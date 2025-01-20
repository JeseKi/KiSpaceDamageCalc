using System;
using System.IO;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using Terraria.Localization;
using Terraria.Chat;
using Terraria.ID;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

using static KiSpaceDamageCalc.KiSpaceDamageCalc;
using static KiSpaceDamageCalc.Systems.MainSystem;

namespace KiSpaceDamageCalc
{
    public enum LogLevel
    {
        INFO,
        DEBUG,
        WARNING,
        ERROR
    }

    public class LogTimer
    {
        public string LoggerID { get; set; }
        public int LogInterval { get; set; }
        public int Timer { get; set; }
        public bool CanLog { 
            get{
                if (Timer >= LogInterval)
                {
                    Timer = 0;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }

    public static class KiLogger
    {
        public static int logTimer;
        public static bool canLog;
        private static string _logPath;
        private static string _warningLogPath;
        private static string _counterPath;
        private static string _cacheDirectory;
        private static string _cacheLogPath;
        private static string _cacheWarningLogPath;

        private static string LogPath => _logPath ??= Path.Combine(ModRoot, "logs", "KiSpace_log.log");
        private static string WarningLogPath => _warningLogPath ??= Path.Combine(ModRoot, "logs", "WARNING.log");
        private static string CounterPath => _counterPath ??= Path.Combine(ModRoot, "logs", "log_counter.txt");
        private static string CacheDirectory => _cacheDirectory ??= Path.Combine(ModRoot, "logs", "cache");
        private static string CacheLogPath => _cacheLogPath ??= Path.Combine(CacheDirectory, "_KiSpace_log.log");
        private static string CacheWarningLogPath => _cacheWarningLogPath ??= Path.Combine(CacheDirectory, "_WARNING.log");
        private static List<LogTimer> timeLoggers = new List<LogTimer>();

        static KiLogger()
        {
            InitializeLogs();
        }

        // 用于对 logIndex 生成进行互斥
        private static readonly object indexLock = new object();

        // 用于对实际写日志进行互斥
        private static readonly object fileLock = new object(); 

        /// <summary>
        /// 获取下一个唯一的日志序号
        /// </summary>
        /// <returns>递增后的日志ID，从0开始</returns>
        private static int GetNextLogIndex()
        {
            lock(indexLock)
            {
                // 从0开始计数
                int currentIndex = 0;

                int retryCount = 0;
                const int MAX_RETRIES = 3;
                const int RETRY_DELAY_MS = 10;

                while (retryCount < MAX_RETRIES)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(CounterPath));

                        // 使用 FileShare.None 保证“同一时刻”只有1个线程能读写此文件
                        // 当然，这里还有 lock(indexLock) 的双保险
                        using (var fs = new FileStream(CounterPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        {
                            // 读取当前索引
                            byte[] buffer = new byte[32];
                            int bytesRead = fs.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                string indexStr = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                                if (!string.IsNullOrEmpty(indexStr) && int.TryParse(indexStr, out int parsedIndex))
                                {
                                    currentIndex = parsedIndex + 1;
                                }
                            }

                            // 将最新值写回
                            fs.Position = 0;
                            fs.SetLength(0);  // 清空文件
                            byte[] newIndexBytes = Encoding.UTF8.GetBytes(currentIndex.ToString());
                            fs.Write(newIndexBytes, 0, newIndexBytes.Length);
                            fs.Flush();

                            return currentIndex;
                        }
                    }
                    catch (IOException)
                    {
                        retryCount++;
                        if (retryCount < MAX_RETRIES)
                        {
                            Thread.Sleep(RETRY_DELAY_MS);
                            continue;
                        }
                    }
                }

                // 如果多次重试都失败，就退而求其次
                return (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            }
        }
        private static int logIndex => GetNextLogIndex();

        public static string netIndex => Guid.NewGuid().ToString();
        private const int MAX_LINES = 10000;  // 单个日志文件最大行数
        private const int MAX_FILES = 5;      // 最大保留文件数
        private static int currentLines = 0;   // 当前日志文件的行数

        public static void Log(string message, LogLevel level = LogLevel.INFO)
        {
            // 因为要写文件，写主日志和警告日志，这里也要做互斥
            lock(fileLock)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string formattedMessage = $"[{logIndex:D8}] [{timestamp}] [{level}] {message}";

                    // 写主日志
                    using (var fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(fs))
                    {
                        writer.WriteLine(formattedMessage);
                        writer.Flush();
                    }

                    // 如果是 ERROR 级别，写入警告日志
                    if (level == LogLevel.ERROR)
                    {
                        string warningPath = Path.Combine(Main.SavePath, "Mods", "KiSpace", "WARNING.log");
                        try
                        {
                            using (var fs = new FileStream(warningPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                            using (var writer = new StreamWriter(fs))
                            {
                                writer.WriteLine(formattedMessage);
                                if (message.Contains("Exception") || message.Contains("Error"))
                                {
                                    writer.WriteLine("Stack Trace: " + Environment.StackTrace);
                                }
                                writer.Flush();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to write to warning log: {ex.Message}");
                        }
                    }

                    // 轮转、行数计数等操作也在这个锁里做
                    currentLines++;
                    if (currentLines >= MAX_LINES)
                    {
                        RotateLogFiles();
                    }
                }
                catch (Exception ex)
                {
                    // 写失败就写警告日志
                    // 这里也要 lock，但由于上面已经在 lock(fileLock) 里，这里就不再重入
                    try
                    {
                        string warningPath = Path.Combine(Main.SavePath, "Mods", "KiSpace", "WARNING.log");
                        using (var fs = new FileStream(warningPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        using (var writer = new StreamWriter(fs))
                        {
                            writer.WriteLine($"[ERROR] Failed to write log: {ex.Message}");
                            writer.WriteLine($"Stack Trace: {ex.StackTrace}");
                            writer.Flush();
                        }
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine($"Critical logging failure: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 超过一定的时间才能进行日志记录
        /// </summary>
        /// <param name="message"></param>
        /// <param name="color"></param>
        /// <param name="logInterval"></param>
        /// <param name="logInGame"></param>
        /// <param name="level"></param>
        /// <param name="callerFilePath"></param>
        /// <param name="callerLineNumber"></param>
        /// <param name="callerMemberName"></param>
        public static void LogWithTimer(
            string message, 
            Color? color = null,
            int logInterval = 60,
            bool logInGame = true,
            bool logToFile = false,
            bool logOnMutiMode = false,
            int netMode = -1,
            LogLevel level = LogLevel.INFO,
            object instanceId = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = "")
        {
            string instancePart = instanceId != null ? $":{instanceId.GetHashCode()}" : "";
            string loggerID = $"{callerFilePath}:{callerLineNumber}:{callerMemberName}{instancePart}";
            LogTimer timeLogger = timeLoggers.Find(t => t.LoggerID == loggerID);
            Color actualColor = color ?? Color.White;

            if (timeLogger == null)
            {
                timeLogger = new LogTimer
                {
                    LoggerID = loggerID,
                    LogInterval = logInterval,
                    Timer = 0
                };
                timeLoggers.Add(timeLogger);
            }
            else if (!timeLogger.CanLog) return;

            if (logOnMutiMode)
                LogOnMutiMode(message, actualColor, netMode, logToFile, null, null, true, string.Empty, true, level, callerFilePath, callerLineNumber, callerMemberName);
            else if (logInGame)
                Main.NewText(message, actualColor);

            if (logToFile && !logOnMutiMode) 
                Log(message, level);
        }

        public static void UpdateLogTimer()
        {
            foreach (var timeLogger in timeLoggers)
            {
                timeLogger.Timer++;
            }
        }

        /// <summary>
        /// 多人中的日志记录，用于确定多个通信属于同一会话。
        /// 在使用的时候，需要传入一个packet，若本次为转发，则需要传入一个不为空的NetID作为源会话ID。
        /// 在接收的时候，需要先在外部手动让reader读取ID并传入进<see cref="LogOnMutiMode"/>的NetID参数中；转发同理。
        /// </summary>
        /// <param name="_NetID"></param>
        /// <param name="msg"></param>
        /// <param name="color"></param>
        /// <param name="netMode"></param>
        /// <param name="logToFile"></param>
        /// <param name="reader"></param>
        /// <param name="packet"></param>
        /// <param name="logServerTick"></param>
        /// <param name="NetID">本次会话的ID，若为起始则设置为空，若为转发则需要进行设置该转发的源会话ID</param>
        /// <param name="callerFilePath"></param>
        /// <param name="callerLineNumber"></param>
        /// <param name="callerMemberName"></param>
        public static void LogOnMutiModeStart(
            string msg,
            ModPacket packet,
            Color? color = null,
            int netMode = -1,
            bool logToFile = false,
            BinaryReader reader = null,
            bool logServerTick = true,
            string NetID = "",
            bool logCodePosition = true,
            LogLevel level = LogLevel.DEBUG,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = ""
            )
        {
            string _NetID;
            if (string.IsNullOrEmpty(NetID))
            {
                _NetID = netIndex;
                packet.Write(_NetID);
                msg = $"[SESSION_START: {_NetID}] {msg}";
            }
            else
            {
                _NetID = NetID;
                packet.Write(_NetID);
                msg = $"[SESSION: {_NetID}] {msg}";
            }
            LogOnMutiMode(msg, color, netMode, logToFile, reader, packet, logServerTick, _NetID, logCodePosition, level, callerFilePath, callerLineNumber, callerMemberName);
        }

        /// <summary>
        /// 多人中的日志记录，若不需要记录同一个Session，而是仅作为单次的通信记录，请不要传入NetID，而是传入packet和reader，NetID会自动发送和读取。
        /// </summary>
        /// <param name="msg">消息</param>
        /// <param name="color">游戏中显示的颜色</param>
        /// <param name="netMode">在什么模式下打印</param>
        /// <param name="logToFile">是否记录到文件中</param>
        /// <param name="reader">通信时的读取包</param>
        /// <param name="packet">通信时的发送包</param>
        public static void LogOnMutiMode(
            string msg,
            Color? color = null,
            int netMode = -1,
            bool logToFile = false,
            BinaryReader reader = null,
            ModPacket packet = null,
            bool logServerTick = true,
            string NetID = "",
            bool logCodePosition = true,
            LogLevel level = LogLevel.DEBUG,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0,
            [CallerMemberName] string callerMemberName = ""
            )
        {
            // 处理颜色
            Color actualColor = color ?? Color.White;
            string netModeStr = string.Empty;
            bool canLog = false;
            string fileName = Path.GetFileName(callerFilePath);

            // 读取消息内容
            if (reader != null)
            {
                string netID = reader.ReadString();
                if (netID == NetID) msg = $"[SESSION: {netID}] {msg}";
                msg = $"R[{netID}] {msg}";
            }
            else if (packet != null)
            {
                string netID = NetID == "" ? netIndex : NetID;
                msg = $"S[{netID}] {msg}";
                packet.Write(netID);
            }

            // 添加服务器Tick信息
            if (logServerTick)
            {
                msg = $"[{ServerTick}] {msg}";
            }

            if (logCodePosition) msg = $"[{fileName}:{callerLineNumber} ({callerMemberName})] {msg}";

            if (netMode == KiNetmodeID.SinglePlayer)
            {
                netModeStr = "[单人] ";
            }
            else if (netMode == KiNetmodeID.MultiplayerClient)
            {
                netModeStr = $"[客户端{Main.myPlayer}] ";
            }
            else if (netMode == KiNetmodeID.Server)
            {
                netModeStr = "[服务端] ";
            }
            else if (netMode == KiNetmodeID.MultiplayerMode)
            {
                switch (Main.netMode)
                {
                    case KiNetmodeID.MultiplayerClient:
                        netModeStr = $"[客户端{Main.myPlayer}] ";
                        canLog = true;
                        break;
                    case KiNetmodeID.Server:
                        netModeStr = "[服务端] ";
                        canLog = true;
                        break;
                }
            }
            else if (netMode == KiNetmodeID.Any || netMode == -1)
            {
                switch (Main.netMode)
                {
                    case KiNetmodeID.MultiplayerClient:
                        netModeStr = $"[客户端{Main.myPlayer}] ";
                        break;
                    case KiNetmodeID.Server:
                        netModeStr = "[服务端] ";
                        break;
                    case KiNetmodeID.SinglePlayer:
                        netModeStr = "[单人] ";
                        break;
                }
                canLog = true;
            }
            else
            {
                netModeStr = $"[未知模式{netMode}] ";
            }

            if (netMode == Main.netMode)
            {
                canLog = true;
            }
            
            if (!canLog) return;
            msg = netModeStr + msg;
            NetworkText message = NetworkText.FromLiteral(msg);
            if (logToFile) Log(msg, level);
            ChatHelper.BroadcastChatMessage(message, actualColor);
        }

        private static void RotateLogFiles()
        {
            try
            {
                string oldestLog = LogPath.Replace(".log", $"_{MAX_FILES}.log");
                if (File.Exists(oldestLog))
                {
                    File.Delete(oldestLog);
                }

                for (int i = MAX_FILES - 1; i >= 1; i--)
                {
                    string currentFile = LogPath.Replace(".log", $"_{i}.log");
                    string newFile = LogPath.Replace(".log", $"_{i + 1}.log");
                    if (File.Exists(currentFile))
                    {
                        File.Move(currentFile, newFile);
                    }
                }

                if (File.Exists(LogPath))
                {
                    File.Move(LogPath, LogPath.Replace(".log", "_1.log"));
                }

                currentLines = 0;
            }
            catch
            {
                // 如果轮转失败，忽略错误
            }
        }


        private static void InitializeLogs()
        {
            try
            {
                // 确保所有必要的目录都存在
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                Directory.CreateDirectory(Path.GetDirectoryName(WarningLogPath));
                Directory.CreateDirectory(CacheDirectory);

                // 清空所有日志文件
                File.WriteAllText(LogPath, string.Empty);
                File.WriteAllText(WarningLogPath, string.Empty);
                File.WriteAllText(CacheLogPath, string.Empty);
                File.WriteAllText(CacheWarningLogPath, string.Empty);
                File.WriteAllText(CounterPath, string.Empty);
                currentLines = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize logs: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }
}