﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using NLog;
using RemoteFork.Properties;
using RemoteFork.Requestes;
using Unosquare.Labs.EmbedIO;
using Unosquare.Net;

namespace RemoteFork.Server {
    internal class RequestDispatcher : WebModuleBase {
        private static readonly ILogger Log = LogManager.GetLogger("RequestDispatcher", typeof(RequestDispatcher));

        private static readonly List<Route> Routes = new List<Route> {
            new Route {
                CanHandle = c => string.Equals(c.RequestPath(), TestRequestHandler.UrlPath, StringComparison.OrdinalIgnoreCase),
                Handler = new TestRequestHandler()
            },
            new Route {
                CanHandle = c => Settings.Default.Dlna
                                 && string.Equals(c.RequestPath(), RootRequestHandler.TreePath, StringComparison.OrdinalIgnoreCase)
                                 && c.InQueryString(null)
                                 && c.Request.QueryString.GetValues(null)?.FirstOrDefault(s => string.Equals(s, RootRequestHandler.RootPath, StringComparison.OrdinalIgnoreCase)) != null,
                Handler = new RootRequestHandler()
            },
            new Route {
                CanHandle = c => Settings.Default.Dlna
                                 && string.Equals(c.RequestPath(), RootRequestHandler.TreePath, StringComparison.OrdinalIgnoreCase)
                                 && c.InQueryString(null)
                                 && c.Request.QueryString.GetValues(null)?.FirstOrDefault(s => s.StartsWith(Uri.UriSchemeFile)) != null,
                Handler = new DlnaDirectoryRequestHandler()
            },
            new Route {
                CanHandle = c => string.Equals(c.RequestPath(), RootRequestHandler.TreePath, StringComparison.OrdinalIgnoreCase)
                                 && c.InQueryString(null)
                                 && c.Request.QueryString.GetValues(null)?.FirstOrDefault(s => string.Equals(s, UserUrlsRequestHandler.ParamUrls, StringComparison.OrdinalIgnoreCase)) != null,
                Handler = new UserUrlsRequestHandler()
            },
            new Route {
                CanHandle = c => string.Equals(c.RequestPath(), RootRequestHandler.TreePath, StringComparison.OrdinalIgnoreCase)
                                 && c.InQueryString(null)
                                 && c.Request.QueryString.GetValues(null)?.FirstOrDefault(s => PluginRequestHandler.PluginParamRegex.IsMatch(s ?? string.Empty)) != null,
                Handler = new PluginRequestHandler()
            },
            new Route {
                CanHandle = c => string.Equals(c.RequestPath(), ParseLinkRequestHandler.UrlPath, StringComparison.OrdinalIgnoreCase),
                Handler = new ParseLinkRequestHandler()
            },
            new Route {
                CanHandle = c => Settings.Default.Dlna
                                 && string.Equals(c.RequestPath(), RootRequestHandler.RootPath, StringComparison.OrdinalIgnoreCase)
                                 && c.InQueryString(null)
                                 && c.Request.QueryString.GetValues(null)?.FirstOrDefault(s => s.StartsWith(Uri.UriSchemeFile)) != null,
                Handler = new DlnaFileRequestHandler()
            }
        };

        public WebServer _webServer;
        public RequestDispatcher(WebServer Server) {
            _webServer = Server;
            AddHandler(ModuleMap.AnyPath, Unosquare.Labs.EmbedIO.Constants.HttpVerbs.Any, Handle2);
          
        }

        private Task<bool> Handle2(HttpListenerContext server, CancellationToken ctx)
        {
            //_webServer.RunAsync();
            Console.WriteLine("RequestDispatcher");
            try
            {
             //   server.Response.SendChunked=true;
                Handle(server, ctx);
            }catch(Exception ex) {
                Console.WriteLine(ex);
            }
            return Task.FromResult(true);
        }

        private void Handle(HttpListenerContext context, CancellationToken ctx) {
            
           // new Task(() =>            {
                 Console.WriteLine("RequestDispatcher2");
                Log.Debug("Processing url: {0}", HttpUtility.UrlDecode(context.Request.RawUrl));

                context.Response.Headers.Add("Server", $"RemoteFork/{Assembly.GetExecutingAssembly().GetName().Version}");
                context.Response.KeepAlive = false;
                var route = Routes.FirstOrDefault(r => r.CanHandle(context));

                if (route?.Handler != null)
                {
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                    context.Response.StatusCode = HttpStatusCode.Ok.ToInteger();

                    try
                    {
                        Console.WriteLine(context.Request.RawUrl);
                        route.Handler.Handle(context);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);

                        context.Response.StatusCode = HttpStatusCode.InternalServerError.ToInteger();
                    }
                }
                else
                {
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    if (context.Request.RawUrl.IndexOf("/acestream") == 0)
                    {
                        Console.WriteLine("acestream:" + context.Request.RawUrl);
                        context.Response.StatusCode = HttpStatusCode.Ok.ToInteger();
                        var Handler = new AceStream();
                        Handler.Handle(context, true);


                    }
                    else if (context.Request.RawUrl.IndexOf("/proxym3u8") == 0)
                    {
                        Console.WriteLine("Proxy m3u8:" + context.Request.RawUrl);
                        context.Response.StatusCode = HttpStatusCode.Ok.ToInteger();
                        var Handler = new ProxyM3u8RequestHandler();
                        Handler.Handle(context, true);


                    }
                    else
                    {
                        Console.WriteLine("URL:" + context.Request.RawUrl);

                        Log.Debug("Resource not found: {0}", HttpUtility.UrlDecode(context.Request.RawUrl));

                        BaseRequestHandler.WriteResponse(context.Response, HttpStatusCode.NotFound, $"Resource Not found: {HttpUtility.UrlDecode(context.Request.RawUrl)}");

                    }
                }
          //  });
            Console.WriteLine("end RequestDispatcher");
            
        }

        internal class Route {
            internal Predicate<HttpListenerContext> CanHandle = r => true;

            public IRequestHandler Handler { get; set; }
        }

        public override string Name => "RequestDispatcher";
    }

    
}