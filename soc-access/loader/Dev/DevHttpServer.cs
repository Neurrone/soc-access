using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>Answers one dev route. Implemented by the loader's builtins and by whatever the
    /// mod registers through <see cref="SongsOfConquestAccess.Loader.ModHost"/>.</summary>
    public delegate DevResponse DevRouteHandler(DevRequest request);

    /// <summary>
    /// A route and the query parameters it understands. Every route declares its parameters here -
    /// the loader's builtins and the mod's registrations alike - and a parameter this route does
    /// not name is answered 400 rather than ignored.
    ///
    /// That rule is worth the ceremony: a misspelt or misremembered parameter used to be dropped
    /// in silence, which reads exactly like the feature it names being broken (a <c>?path=</c> sent
    /// to /gui/age, whose filter is <c>?window=</c>, cost a whole session's work to the belief that
    /// filtering did not work). Routes are few and their parameters change rarely, so the list is
    /// cheap to keep honest; being wrong about it is not.
    /// </summary>
    internal sealed class DevRoute
    {
        public readonly DevRouteHandler Handler;
        private readonly string[] _allowed;

        public DevRoute(DevRouteHandler handler, params string[] allowed)
        {
            Handler = handler;
            _allowed = allowed ?? new string[0];
        }

        /// <summary>The 400 for the first parameter this route does not understand, or null when
        /// every parameter given is one of its own.</summary>
        public DevResponse Reject(DevRequest request)
        {
            for (int i = 0; i < request.Query.Count; i++)
            {
                string name = request.Query.GetKey(i);
                if (name == null)
                {
                    return Bad(
                        request,
                        "the query string has a value with no parameter name ('"
                            + request.Query[i]
                            + "')"
                    );
                }

                if (!Knows(name))
                {
                    return Bad(request, "unknown query parameter '" + name + "'");
                }
            }

            return null;
        }

        private bool Knows(string name)
        {
            foreach (string allowed in _allowed)
            {
                if (string.Compare(allowed, name, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private DevResponse Bad(DevRequest request, string what)
        {
            return DevResponse.Json(
                400,
                DevJson.Error(
                    what
                        + " on "
                        + request.Method
                        + " "
                        + request.Path
                        + "; "
                        + (
                            _allowed.Length == 0
                                ? "this route takes no query parameters"
                                : "this route takes: " + string.Join(", ", _allowed)
                        )
                )
            );
        }
    }

    public sealed class DevRequest
    {
        public string Method;
        public string Path;
        public NameValueCollection Query;
        public string Body;

        public string QueryValue(string name)
        {
            return Query[name];
        }

        public int QueryInt(string name, int fallback)
        {
            int value;
            return int.TryParse(Query[name], out value) ? value : fallback;
        }

        public long QueryLong(string name, long fallback)
        {
            long value;
            return long.TryParse(Query[name], out value) ? value : fallback;
        }
    }

    public sealed class DevResponse
    {
        public int StatusCode = 200;
        public string ContentType = "application/json; charset=utf-8";
        public byte[] Body;

        public static DevResponse Json(string json)
        {
            return Json(200, json);
        }

        public static DevResponse Json(int statusCode, string json)
        {
            return new DevResponse { StatusCode = statusCode, Body = Encoding.UTF8.GetBytes(json) };
        }

        public static DevResponse Png(byte[] png)
        {
            return new DevResponse { ContentType = "image/png", Body = png };
        }
    }

    /// <summary>
    /// Writes the small JSON payloads the dev routes answer with, straight through a
    /// JsonTextWriter. The game ships Newtonsoft.Json 9.0.1 and the mod borrows it rather than
    /// deploying its own; the streaming writer is the part of that old build we can rely on, and
    /// it also keeps a 5000-node GUI dump from being materialised as an object graph first.
    /// </summary>
    public static class DevJson
    {
        public static string Write(Action<JsonTextWriter> body)
        {
            StringWriter text = new StringWriter(CultureInfo.InvariantCulture);
            using (JsonTextWriter json = new JsonTextWriter(text))
            {
                body(json);
            }

            return text.ToString();
        }

        public static string Error(string message)
        {
            return Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("error");
                json.WriteValue(message);
                json.WriteEndObject();
            });
        }

        public static string Ok()
        {
            return Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("ok");
                json.WriteValue(true);
                json.WriteEndObject();
            });
        }
    }

    /// <summary>
    /// Loopback-only HTTP front end for the dev routes. Bound to http://127.0.0.1:port/ so it is
    /// reachable from this machine alone. One background thread accepts, and each request is
    /// answered on a pool thread: POST /wait can hold a connection for a minute and POST /eval for
    /// its settle window, and neither may stop the caller from reading /speech or /log meanwhile.
    /// Handlers therefore have to be thread-safe, which they are by construction - anything
    /// touching the game goes through <see cref="MainThreadQueue"/>, and the buffers behind the
    /// polled routes are locked.
    ///
    /// A handler that throws answers 500 and the server keeps accepting; shutdown stops and closes
    /// the listener and joins the accept thread, so a hot reload or game exit never hangs on it.
    ///
    /// Unity's Mono implements HttpListener in managed code, so there is no http.sys URL
    /// reservation to register and no elevation needed for the loopback prefix.
    /// </summary>
    internal sealed class DevHttpServer
    {
        private const int ShutdownJoinMilliseconds = 2000;

        private readonly string _address;
        private readonly DevRouteHandler _handler;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public DevHttpServer(int port, DevRouteHandler handler)
        {
            _address = "http://127.0.0.1:" + port + "/";
            _handler = handler;
        }

        public string Address
        {
            get { return _address; }
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(_address);
            _listener.Start();
            _running = true;
            _thread = new Thread(Serve) { IsBackground = true, Name = "SocAccessDevHttp" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;

            HttpListener listener = _listener;
            _listener = null;
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                    listener.Close();
                }
                catch (Exception e)
                {
                    LoaderLog.Warn("Dev server listener did not close cleanly: " + e.Message);
                }
            }

            if (_thread != null)
            {
                _thread.Join(ShutdownJoinMilliseconds);
                _thread = null;
            }
        }

        private void Serve()
        {
            HttpListener listener = _listener;
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception e)
                {
                    if (_running)
                    {
                        LoaderLog.Warn("Dev server stopped accepting requests: " + e.Message);
                    }

                    return;
                }

                ThreadPool.QueueUserWorkItem(state => Answer((HttpListenerContext)state), context);
            }
        }

        private void Answer(HttpListenerContext context)
        {
            DevResponse response;
            try
            {
                response = _handler(Read(context.Request));
            }
            catch (Exception e)
            {
                LoaderLog.Warn("Dev server request failed: " + e);
                response = DevResponse.Json(500, DevJson.Error(e.Message));
            }

            try
            {
                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = response.ContentType;
                context.Response.ContentLength64 = response.Body.Length;
                context.Response.OutputStream.Write(response.Body, 0, response.Body.Length);
                context.Response.Close();
            }
            catch (Exception e)
            {
                LoaderLog.Warn("Dev server could not send a response: " + e.Message);
            }
        }

        private static DevRequest Read(HttpListenerRequest raw)
        {
            string body = "";
            if (raw.HasEntityBody)
            {
                using (StreamReader reader = new StreamReader(raw.InputStream, raw.ContentEncoding))
                {
                    body = reader.ReadToEnd();
                }
            }

            return new DevRequest
            {
                Method = raw.HttpMethod,
                Path = raw.Url.AbsolutePath,
                Query = raw.QueryString,
                Body = body,
            };
        }
    }
}
