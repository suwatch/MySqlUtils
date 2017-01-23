using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MySqlUtils
{
    public static class Utils
    {
        static bool _isAzure = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        static TimeSpan _mySqlTimeout = TimeSpan.FromSeconds(0);

        public static bool IsAzure
        {
            get { return _isAzure; }
        }

        public static TimeSpan MySqlTimeout
        {
            get
            {
                if (_mySqlTimeout <= TimeSpan.FromSeconds(0))
                {
                    var value = Environment.GetEnvironmentVariable("MYSQLUTILS_TIMEOUT_SECS");
                    var secs = 0;
                    if (!string.IsNullOrEmpty(value) && Int32.TryParse(value, out secs) && secs > 0)
                    {
                        _mySqlTimeout = TimeSpan.FromSeconds(secs);
                    }
                    else
                    {
                        _mySqlTimeout = TimeSpan.FromSeconds(600);
                    }
                }

                return _mySqlTimeout;
            }
        }

        public static string GetPath(string format)
        {
            return String.Format(format, Utils.IsAzure ? "/MySqlUtils" : String.Empty);
        }

        public static bool SafeExecute(Action action, Tracer tracer = null)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                if (tracer != null)
                {
                    tracer.Trace("Handled Exception: {0}", ex);
                }
                return false;
            }
        }

        public static async Task<bool> SafeExecute(Func<Task> action, Tracer tracer = null)
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                if (tracer != null)
                {
                    tracer.Trace("Handled Exception: {0}", ex);
                }
                return false;
            }
        }

        public static Task WaitHandleAsync(this WaitHandle handle, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<object>();
            var registration = ThreadPool.RegisterWaitForSingleObject(handle, (state, timedOut) =>
            {
                var localTcs = (TaskCompletionSource<object>)state;
                if (timedOut)
                {
                    localTcs.TrySetException(new TimeoutException("Exceeed timeout " + timeout));
                    //localTcs.TrySetCanceled();
                }
                else
                {
                    localTcs.TrySetResult(null);
                }
            }, tcs, timeout, executeOnlyOnce: true);
            tcs.Task.ContinueWith((_, state) => ((RegisteredWaitHandle)state).Unregister(null), registration, TaskScheduler.Default);
            return tcs.Task;
        }
    }
}
