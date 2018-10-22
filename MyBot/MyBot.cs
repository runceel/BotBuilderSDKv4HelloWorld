using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyBot
{
    public class MyBot : IBot
    {
        private ILogger Logger { get; }
        private MyBotAccessors MyBotAccessors { get; }

        private DialogSet Dialogs { get; }

        public MyBot(MyBotAccessors myBotAccessors, ILoggerFactory loggerFactory)
        {
            MyBotAccessors = myBotAccessors ?? throw new ArgumentNullException(nameof(myBotAccessors));
            Logger = loggerFactory?.CreateLogger<MyBot>() ?? throw new ArgumentNullException(nameof(loggerFactory));

            Dialogs = new DialogSet(MyBotAccessors.ConversationDialogState);

            // WaterfallDialog を登録。引数に Task<DialogTurnResult> Xxx(WaterfallStepContext, CancellationToken) の配列を渡す。
            // 今回は一か所にまとめるためにラムダで書いたけど、普通は何らかのクラスのメソッドを渡すのがいいと思う。
            Dialogs.Add(new WaterfallDialog("details", new WaterfallStep[]
            {
                (stepContext, cancellationToken) => stepContext.PromptAsync("name", new PromptOptions { Prompt = MessageFactory.Text("名前は？") }, cancellationToken),
                async (stepContext, cancellationToken) =>
                {
                    var userProfile = await MyBotAccessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
                    userProfile.Name = (string)stepContext.Result;
                    await stepContext.Context.SendActivityAsync($"ありがと！{userProfile.Name}！！", cancellationToken: cancellationToken);
                    return await stepContext.PromptAsync("confirm", new PromptOptions { Prompt = MessageFactory.Text("年齢教えてくれる？") }, cancellationToken);
                },
                async (stepContext, cancellationToken) =>
                {
                    if ((bool)stepContext.Result)
                    {
                        return await stepContext.PromptAsync("age", new PromptOptions {Prompt = MessageFactory.Text("ありがとう！年齢入れて！")}, cancellationToken);
                    }
                    else
                    {
                        // 年齢は -1 ということにして次のへ
                        return await stepContext.NextAsync(-1, cancellationToken);
                    }
                },
                async (stepContext, cancellationToken) =>
                {
                    var userProfile = await MyBotAccessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
                    userProfile.Age = (int)stepContext.Result;
                    if (userProfile.Age == -1)
                    {
                        // 年齢キャンセルされた
                        await stepContext.Context.SendActivityAsync($"ミステリアスなんだね！！", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // 年齢入れてもらった
                        await stepContext.Context.SendActivityAsync($"{userProfile.Age} 歳なんだね！！", cancellationToken: cancellationToken);
                   }

                    return await stepContext.PromptAsync("confirm", new PromptOptions { Prompt = MessageFactory.Text("あってる？") }, cancellationToken);
                },
                async (stepContext, cancellationToken) =>
                {
                    if ((bool)stepContext.Result)
                    {
                        var userProfile = await MyBotAccessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
                        if (userProfile.Age == -1)
                        {
                            await stepContext.Context.SendActivityAsync($"ミステリアスな {userProfile.Name} さんだね！", cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await stepContext.Context.SendActivityAsync($"{userProfile.Age} 歳の {userProfile.Name} さんだね！", cancellationToken: cancellationToken);
                        }
                    }
                    else
                    {
                        await stepContext.Context.SendActivityAsync($"じゃぁ、君のことは覚えないで忘れておくね！", cancellationToken: cancellationToken);
                        await MyBotAccessors.UserProfile.DeleteAsync(stepContext.Context, cancellationToken);
                    }

                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                }
            }));

            // WaterfallDialog の中で PromptAsync で呼び出してるダイアログも追加する。
            Dialogs.Add(new TextPrompt("name"));
            Dialogs.Add(new NumberPrompt<int>("age"));
            Dialogs.Add(new ConfirmPrompt("confirm"));
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            Logger.LogInformation($"{nameof(OnTurnAsync)} started");
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var dialogContext = await Dialogs.CreateContextAsync(turnContext, cancellationToken);
                var results = await dialogContext.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dialogContext.BeginDialogAsync("details", null, cancellationToken);
                }

                await MyBotAccessors.SaveChangesAsync(turnContext);
            }
            else
            {
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected");
            }

            Logger.LogInformation($"{nameof(OnTurnAsync)} ended");
        }
    }
}
