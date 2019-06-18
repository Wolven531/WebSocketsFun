using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebSocketsFun
{
    public class Startup
    {
        private ILogger<Startup> _logger;

        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            Configuration = configuration;
            _logger = logger;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // WebSockets impl inspired by:
        // article: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-2.2
        // example: https://github.com/aspnet/AspNetCore.Docs/blob/master/aspnetcore/fundamentals/websockets/samples/2.x/WebSocketsSample/Startup.cs
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseHsts();

            // #if NoOptions
            //     #region UseWebSockets
            //             app.UseWebSockets();
            //     #endregion
            // #endif

//#if UseOptions
            #region UseWebSocketsWithOptions
             var webSocketOptions = new WebSocketOptions() 
             {
                 KeepAliveInterval = TimeSpan.FromSeconds(120),
                 ReceiveBufferSize = 4 * 1024
             };

             app.UseWebSockets(webSocketOptions);
            #endregion
//#endif

            //// #if UseOptionsAllowedOrigins
            //    #region UseWebSocketsOptionsAllowedOrigins
            //            var webSocketOptions = new WebSocketOptions()
            //            {
            //                KeepAliveInterval = TimeSpan.FromSeconds(120),
            //                ReceiveBufferSize = 4 * 1024
            //            };
            //            webSocketOptions.AllowedOrigins.Add("https://client.com");
            //            webSocketOptions.AllowedOrigins.Add("https://www.client.com");

            //            app.UseWebSockets(webSocketOptions);
            //    #endregion
            //// #endif

            app.Use(async (context, next) =>
            {
                if (context.Request.Path != "/ws")
                {
                    await next();
                    return;
                }
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    _logger.LogCritical("Received request at web socket path that was not a WebSocket request, returning 400...");
                    context.Response.StatusCode = 400;
                    return;
                }
                _logger.LogCritical("Received request proper WebSocket request, awaiting transition of request to connection...");
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                _logger.LogCritical("Transition complete, starting Echo...");
                await Echo(context, webSocket);
            });

            app.UseHttpsRedirection();
            app.UseMvc();
        }

        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    CancellationToken.None);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }
}
