using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MySqlUtils
{
    public class Tracer : IDisposable
    {
        private const int MaxLogFiles = 20;
        private const string LogFilePrefix = "MySqlUtils-";

        private static int _padding = 0;

        private readonly HttpContext _context;
        private readonly string _fileName;
        private readonly bool _noTrace;

        public Tracer(HttpContext context)
        {
            _context = context;
            _noTrace = context.Request.HttpMethod == "GET";
            if (_noTrace)
            {
                return;
            }

            var padding = Interlocked.Increment(ref _padding) % MaxLogFiles;
            var file = String.Format("{0}{1:yyyy-MM-dd-HH-mm-ss}-{2:D3}_{3}-{4}.log",
                LogFilePrefix,
                DateTime.UtcNow,
                padding,
                context.Request.HttpMethod,
                context.Request.RawUrl.Split('?')[0].Trim('/').Replace('/', '-'));

            if (Utils.IsAzure)
            {
                _fileName = Path.Combine(@"d:\home\logFiles\mysql", file);
            }
            else
            {
                _fileName = Path.Combine(@"c:\temp\mysql", file);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_fileName));

            Trace("Begin request {0} {1}, User-Agent: {2}", context.Request.HttpMethod, context.Request.RawUrl, context.Request.ServerVariables["HTTP_USER_AGENT"]);

            if (padding % MaxLogFiles == 1)
            {
                CleanupLogFiles();
            }
        }

        public void Dispose()
        {
            Trace("End request");
        }

        public void Trace(string message)
        {
            Trace("{0}", message);
        }

        public void Trace(object obj)
        {
            Trace("{0}", obj);
        }

        public void Trace(string format, params object[] args)
        {
            if (_noTrace)
            {
                return;
            }

            var message = String.Format("{0}, {1}", DateTime.UtcNow.ToString("o"), String.Format(format, args));
            Utils.SafeExecute(() => File.AppendAllLines(_fileName, new[] { message }));
        }

        private void CleanupLogFiles()
        {
            Task.Run(() =>
            {
                var files = Directory.GetFiles(Path.GetDirectoryName(_fileName), 
                    string.Format("{0}*.log", LogFilePrefix), SearchOption.TopDirectoryOnly);
                var toDelete = files.Length - MaxLogFiles;
                if (toDelete > 0)
                {
                    foreach (var file in files.OrderBy(f => f).Take(toDelete))
                    {
                        File.Delete(file);
                    }
                }
            });
        }
    }
}