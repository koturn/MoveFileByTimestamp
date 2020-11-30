using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace MoveFileByTimestamp
{
    public class Program
    {
        /// <summary>
        /// PNG、JPEGはzipによってほとんど圧縮出来ないため、合計サイズが2GB以内でなければ
        /// zip圧縮の対象としない。
        /// </summary>
        private const long MaxSumFileSize = 2L * 1024L * 1024L * 1024L;
        /// <summary>
        /// <para>デフォルトの時刻オフセット</para>
        /// <para>この時間分を前日に含める</para>
        /// <para>デフォルトでは05:00:00なので、2020/11/01 05:00:00 ~ 2020/11/02 04:59:59 を 2020/11/01 として扱う</para>
        /// </summary>
        private static readonly TimeSpan DefaultOffsetTs;
        /// <summary>
        /// デフォルトの対象ファイル拡張子
        /// </summary>
        private static string[] DefaultExts;

        static Program()
        {
            DefaultOffsetTs = new TimeSpan(5, 0, 0);
            DefaultExts = new[] { ".png", ".jpg", ".snip" };
        }

        static void Main(string[] args)
        {
            var ot = DefaultOffsetTs;
            var exts = DefaultExts;

            Directory.EnumerateFiles(".")
                .AsParallel()
                .Where(fileName => exts.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Select(fileName => (FileName: fileName, LastWriteTime: File.GetLastWriteTime(fileName)))
                .Select(tpl => (tpl.FileName, tpl.LastWriteTime, OffsetLastWriteTime: (tpl.LastWriteTime - ot).Date))
                .GroupBy(tpl => tpl.OffsetLastWriteTime)
                .ForAll(group =>
                {
                    var ymd = group.Key.ToString("yyyyMMdd");
                    Directory.CreateDirectory(ymd);

                    var dstDirPath = Path.Combine(
                        Path.GetDirectoryName(group.First().FileName),
                        ymd);
                    var nFiles = 0;
                    var sumFileSize = 0L;
                    foreach (var fileName in group.Select(tpl => tpl.FileName))
                    {
                        var dstFilePath = Path.Combine(
                            dstDirPath,
                            Path.GetFileName(fileName));
                        if (!File.Exists(dstFilePath))
                        {
                            File.Move(fileName, dstFilePath);
                        }
                        nFiles++;
                        sumFileSize += new FileInfo(dstFilePath).Length;
                    }

                    var zipFilePath = dstDirPath + ".zip";
                    if (sumFileSize < MaxSumFileSize)
                    {
                        if (File.Exists(zipFilePath))
                        {
                            File.Delete(zipFilePath);
                        }
                        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Create Zip archive: {zipFilePath} ({nFiles} files: {sumFileSize / 1024.0 / 1024.0:f3} MB) ...");
                        using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                        {
                            foreach (var fileName in Directory.EnumerateFiles(dstDirPath))
                            {
                                archive.CreateEntryFromFile(
                                    fileName,
                                    ymd + "/" + Path.GetFileName(fileName),
                                    CompressionLevel.Optimal);
                            }
                        }
                        var zipFileSize = new FileInfo(zipFilePath).Length;
                        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Create Zip file done!: {zipFilePath} ({zipFileSize / 1024.0 / 1024.0:f3} MB, Defrated {(1.0 - (double)zipFileSize / sumFileSize) * 100.0:f2} %)");
                    }
                    else
                    {
                        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Ignore to create Zip archive: {zipFilePath} ({nFiles} files: {sumFileSize / 1024.0 / 1024.0:f3} MB)");
                    }
                });
        }
    }
}
