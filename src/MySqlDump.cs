using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace MySqlUtils
{
    public class MySqlDump : MySqlHandler
    {
        public MySqlDump(Tracer tracer)
            : base(tracer)
        {
        }

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            var resultFile = Utils.IsAzure ? @"d:\local\temp\mysqldump_temp.sql" : @"c:\temp\mysql\mysqldump_temp.sql";

            var exe = Path.Combine(BasePath, "bin", "mysqldump.exe");
            // --single-transaction aquires a REPEATABLE READ lock for the time of the dump to ensure consistency
            // http://dev.mysql.com/doc/refman/5.6/en/mysqldump.html#option_mysqldump_single-transaction
            var arguments = string.Format(@"--single-transaction -u{0} -p{1} -h{2} --port={3} --result-file={4} {5}", UserID, Password, Server, Port, resultFile, Database);
            Trace("\"{0}\" {1}", exe, arguments.Replace(Password, "*****"));

            var process = MySqlProcess.Start(exe, arguments);
            var processName = process.ProcessName;
            var processId = process.Id;

            Trace("{0}:{1} started", processName, processId);

            try
            {
                // wait for exit
                await process.WaitForExitAsync(DefaultTimeout);
            }
            catch (Exception ex)
            {
                process.Kill();

                Trace("{0}:{1} killed.", processName, processId);

                throw new InvalidOperationException(String.Format("Wait for process {0}:{1} failed with {2}", processName, processId, ex), ex);
            }

            // to flush all output
            process.WaitForExit();

            Trace("{0}:{1} exited with {2}.", processName, processId, process.ExitCode);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(String.Format("{0}:{1} exit with {2}!  {3}", processName, processId, process.ExitCode, process.Output));
            }

            var prefix = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            if (String.IsNullOrEmpty(prefix))
            {
                prefix = "mysqldump";
            }

            var fileName = String.Format("{0}-{1:yy-MM-dd-HH-mm-ss}.sql", prefix, DateTime.UtcNow);
            context.Response.ContentType = "text/plain";
            context.Response.Headers["Content-Disposition"] = "attachment; filename=" + fileName;
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            using (var fs = File.OpenRead(resultFile))
            {
                await fs.CopyToAsync(context.Response.OutputStream);
            }
            context.Response.End();
        }
    }
}