using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Utils;
using Server.Infrastructure;

namespace Server.Communication.Discord.Interactions
{
    public static class VaultInteractionHandler
    {
        public static async Task HandleButton(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            if (e.Id == "vault_crack_btn")
            {
                var modal = new DiscordModalBuilder()
                    .WithTitle("Attempt to Crack Vault")
                    .WithCustomId("vault_submit_modal")
                    // min_length and max_length for TextInputComponent are usually valid, 
                    // but the error "Must be 4 or fewer in length" suggests we are passing something else wrong or the library has a quirk.
                    // The error `{"code":"BASE_TYPE_MAX_LENGTH","message":"Must be 4 or fewer in length."}` usually refers to the CustomID or Label length,
                    // BUT "4 or fewer" is extremely short. 
                    // Wait! min_length: 4 is valid.
                    // Maybe the error 50035 relates to something else? 
                    // Actually, let's remove the constraints for a second or fix them.
                    // The previous error log says: component.value._errors: BASE_TYPE_MAX_LENGTH.
                    // Discord modal text inputs allow max_length up to 4000.
                    // However, if we are passing something wrong...
                    
                    // Matching ReferralInteractionHandler pattern: No min/max length constraints in constructor to avoid API errors.
                    // Validation will happen in HandleModal.
                    // Also using null for default value instead of empty string.
                    .AddTextInput(new DiscordTextInputComponent("Enter Code", "vault_code_input", null, required: true, style: DiscordTextInputStyle.Short), "Enter Code");

                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
            }
        }

        public static async Task HandleModal(DiscordClient client, ModalSubmittedEventArgs e)
        {
            if (e.Interaction.Data.CustomId == "vault_submit_modal")
            {
                string codeStr = GetValue(e.Values, "Enter Code"); 
                
                if (!int.TryParse(codeStr, out int guess) || codeStr.Length != 4)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("Invalid code format. 4 digits required.").AsEphemeral(true));
                    return;
                }

                var env = ServerEnvironment.GetServerEnvironment();
                var vaultService = env.ServerManager.VaultService;
                var usersService = env.ServerManager.UsersService;

                // Ensure user
                var user = await usersService.EnsureUserAsync(e.Interaction.User.Id.ToString(), e.Interaction.User.Username, e.Interaction.User.Username);
                if (user == null) return;

                // Defer response
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder().AsEphemeral(true));

                var resultMsg = await vaultService.ProcessGuessAsync(user.Identifier, user.Username, guess, e.Interaction.ChannelId);

                if (resultMsg != null)
                {
                    if (resultMsg.Contains("CRACKED"))
                    {
                        // Public announcement
                        await e.Interaction.Channel.SendMessageAsync(resultMsg);
                        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("Attempt recorded."));
                    }
                    else
                    {
                        // Error (e.g. no money)
                        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent(resultMsg));
                    }
                }
                else
                {
                    // WRONG GUESS (Service returns null)
                    await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent($"❌ Access Denied. Code `{codeStr}` was incorrect. (-10k GP)"));
                }
            }
        }

        private static string GetValue(System.Collections.Generic.IReadOnlyDictionary<string, DSharpPlus.EventArgs.IModalSubmission> values, string key)
        {
            if (values.TryGetValue(key, out var val)) 
            {
                try 
                {
                    dynamic d = val;
                    return d.Value.ToString();
                }
                catch
                {
                    return val?.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
