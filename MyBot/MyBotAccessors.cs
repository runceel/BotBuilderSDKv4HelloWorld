using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Threading.Tasks;

namespace MyBot
{
    public class MyBotAccessors
    {
        private ConversationState ConversationState { get; }
        private UserState UserState { get; }

        public IStatePropertyAccessor<DialogState> ConversationDialogState { get; }
        public IStatePropertyAccessor<UserProfile> UserProfile { get; }

        public MyBotAccessors(ConversationState conversationState, UserState userState)
        {
            ConversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            UserState = userState ?? throw new ArgumentNullException(nameof(userState));
            ConversationDialogState = ConversationState.CreateProperty<DialogState>($"{nameof(MyBotAccessors)}.{nameof(ConversationDialogState)}");
            UserProfile = UserState.CreateProperty<UserProfile>($"{nameof(MyBotAccessors)}.{nameof(UserProfile)}");
        }

        public Task SaveConversationStateChangesAsync(ITurnContext turnContext) => ConversationState.SaveChangesAsync(turnContext);
        public Task SaveUserStateChangesAsync(ITurnContext turnContext) => UserState.SaveChangesAsync(turnContext);

        public async Task SaveChangesAsync(ITurnContext turnContext)
        {
            await SaveConversationStateChangesAsync(turnContext);
            await SaveUserStateChangesAsync(turnContext);
        }
    }
}
