using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using NLog;

namespace Exceptions
{
    public class ConverterProgram
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public static void Main(params string[] args)
        {
            try
            {
                var filenames = args.Any() ? args : new[] { "text.txt" };
                var settings = LoadSettings();
                ConvertFiles(filenames, settings);
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        private static void ConvertFiles(string[] filenames, Settings settings)
        {
            var tasks = filenames
                .Select(fn => Task.Run(() => ConvertFile(fn, settings)))
                .ToArray();
            Task.WaitAll(tasks);
        }

        private static Settings LoadSettings()
        {
            var serializer = new XmlSerializer(typeof(Settings));
            var content = "";
            var settings = Settings.Default;

            try
            {
                content = File.ReadAllText("settings.xml");
                settings = (Settings) serializer.Deserialize(new StringReader(content));
            }
            catch (FileNotFoundException e)
            {
                log.Error(e, "Файл настроек .* отсутствует.");
            }
            catch (Exception e)
            {
                throw new Exception("Не удалось прочитать файл настроек", e);
            }

            return settings;
        }

        private static void ConvertFile(string filename, Settings settings)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(settings.SourceCultureName);
            if (settings.Verbose)
            {
                log.Info("Processing file " + filename);
                log.Info("Source Culture " + Thread.CurrentThread.CurrentCulture.Name);
            }
            IEnumerable<string> lines;
            try
            {
                lines = PrepareLines(filename);
            }
            catch
            {
                log.Error($"File {filename} not found");
                return;
            }
            var convertedLines = lines
                .Select(ConvertLine)
                .Select(s => s.Length + " " + s);
            File.WriteAllLines(filename + ".out", convertedLines);
        }

        private static IEnumerable<string> PrepareLines(string filename)
        {
            var lineIndex = 0;
            IEnumerable<string> lines = new List<string>();

            try
            {
                lines = File.ReadLines(filename);
            }
            catch (Exception e)
            {
                log.Error(e, $"Не удалось сконвертировать {filename}");
            }

            foreach (var line in lines)
            {
                if (line == "") continue;
                yield return line.Trim();
                lineIndex++;
            }
            yield return lineIndex.ToString();
        }

        public static string ConvertLine(string arg)
        {
            var result = "";
            TryConvertAsDouble(arg, out result);

            try
            {
                result = ConvertAsDateTime(arg);
            }
            catch
            {
                try
                {
                    result = ConvertAsCharIndexInstruction(arg);
                }
                catch
                {
                    log.Error("Некорректная строка");
                }
            }

            return result;
        }

        private static string ConvertAsCharIndexInstruction(string s)
        {
            var parts = s.Split();

            if (parts.Length < 2)
                throw new AggregateException();

            var charIndex = int.Parse(parts[0]);
            if ((charIndex < 0) || (charIndex >= parts[1].Length))
                throw new AggregateException();

            var text = parts[1];
            return text[charIndex].ToString();
        }

        private static string ConvertAsDateTime(string arg)
        {
            return DateTime.Parse(arg).ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryConvertAsDouble(string arg, out string result)
        {
            result = double.Parse(arg).ToString(CultureInfo.InvariantCulture);

            return result != null;
        }
    }
}