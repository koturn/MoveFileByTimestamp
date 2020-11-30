using System;
using System.IO;
using System.Linq;


namespace MoveFileByTimestamp
{
    class Program
    {
        private static readonly TimeSpan DefaultOffsetTs;
        private static string[] DefaultExts;

        static Program()
        {
            DefaultOffsetTs = new TimeSpan(5, 0, 0);
            DefaultExts = new[] { ".png", ".jpg", "snip" };
        }

        static void Main(string[] args)
        {
            var ot = DefaultOffsetTs;
            var exts = DefaultExts;

            Directory.EnumerateFiles(".")
                .AsParallel()
                .Where(fileName => exts.Any(ext => fileName.EndsWith(ext)))
                .Select(fileName => (FileName: fileName, LastWriteTime: File.GetLastWriteTime(fileName)))
                .Select(tpl => (tpl.FileName, tpl.LastWriteTime, OffsetLastWriteTime: (tpl.LastWriteTime - ot).Date))
                .GroupBy(tpl => tpl.OffsetLastWriteTime)
                .ForAll(group =>
                {
                    var ymd = group.Key.ToString("yyyyMMdd");
                    Directory.CreateDirectory(ymd);

                    foreach (var fileName in group.Select(tpl => tpl.FileName))
                    {
                        var dstFilePath = Path.Combine(
                            Path.GetDirectoryName(fileName),
                            ymd,
                            Path.GetFileName(fileName));
                        if (!File.Exists(dstFilePath))
                        {
                            File.Move(fileName, dstFilePath);
                        }
                    }
                });
        }
    }
}
