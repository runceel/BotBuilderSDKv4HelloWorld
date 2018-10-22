using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyBot
{
    public class Startup
    {
        private bool IsProduction { get; }
        private IConfiguration Configuration { get; }
        private ILoggerFactory LoggerFactory { get; set; }

        public Startup(IHostingEnvironment env)
        {
            IsProduction = env.IsProduction();
            Configuration = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // さっき作成した appsettings.json はここで読み込んでる
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true) // appsettings.production.json とかがあれば読み込む
                .AddEnvironmentVariables() // Azure App Services にデプロイしたときにアプリケーション設定にある内容を読み込む
                .Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddBot<MyBot>(options =>
            {
                var secretKey = Configuration.GetSection("botFileSecret")?.Value;
                var botFilePath = Configuration.GetSection("botFilePath")?.Value;
                // BotConfiguration.bot を読み込んでIServiceCollection に登録
                var botConfig = BotConfiguration.Load(botFilePath ?? @".\BotConfiguration.bot", secretKey);
                services.AddSingleton(_ => botConfig ?? throw new InvalidOperationException($"The .bot config file could not be loaded. ({botConfig})"));

                // オプション。ログとエラーハンドリング
                var logger = LoggerFactory.CreateLogger<MyBot>();
                options.OnTurnError = async (context, ex) =>
                {
                    logger.LogError($"Exception caught : {ex}");
                    await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                };

                // BotConfiguration.bot から今の環境のエンドポイントの情報を取得。なかったらエラー。
                var environment = IsProduction ? "production" : "development";
                var endpointService = botConfig
                    .Services
                    .FirstOrDefault(x => x.Type == "endpoint" && x.Name == environment) as EndpointService ?? 
                        throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
                // Bot の認証情報追加
                options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

                // 開発用途限定！のインメモリーストア。本番は Blob とか使うらしい
                var storage = new MemoryStorage();
                options.State.Add(new ConversationState(storage));
                options.State.Add(new UserState(storage));
            });

            // ステート管理の MyBotAccessors 追加
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value ?? 
                    throw new InvalidOperationException("BotFrameworkOptions must be configured prior to setting up the state accessors");
                var conversationState = options.State.OfType<ConversationState>().FirstOrDefault() ??
                    throw new InvalidOperationException("ConversationState must be defined and added before adding conversation-scoped state accessors.");
                var userState = options.State.OfType<UserState>().FirstOrDefault() ??
                    throw new InvalidOperationException("UserState must be defined and added before adding user-scoped state accessors.");
                return new MyBotAccessors(conversationState, userState);
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory; // loggerFactory は必須じゃないけどログ大事なので書いてる

            // Bot Framework の有効化
            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
