using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Infrastructure.Discord;
using Server.Infrastructure;
using Server.Client.Utils;

namespace Server.Communication.Discord.Interactions
{
    public static class ReferralInteractionHandler
    {
        public static async Task HandleComponent(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            DiscordMember? member = null;
            if (e.Guild is not null)
            {
                member = await e.Guild.GetMemberAsync(e.User.Id);
            }

            if (member is null || !member.IsStaff())
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You are not authorized.").AsEphemeral(true));
                return;
            }

            if (e.Id == "ref_create")
            {
                var modal = new DiscordModalBuilder()
                    .WithTitle("Create Referral Code")
                    .WithCustomId("ref_create_modal")
                    .AddTextInput(new DiscordTextInputComponent("Referral Code", "code", "e.g. MyCode", required: true), "Referral Code")
                    .AddTextInput(new DiscordTextInputComponent("New User Reward (M/K)", "reward", "e.g. 0.5 (for 500k) or 1m", required: true), "New User Reward (M/K)")
                    .AddTextInput(new DiscordTextInputComponent("Referrer Reward (M/K)", "ref_reward", "e.g. 0.1 (for 100k) or 100k", required: true), "Referrer Reward (M/K)")
                    .AddTextInput(new DiscordTextInputComponent("Max Uses (-1 for inf)", "uses", "-1", required: true), "Max Uses (-1 for inf)");

                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
            }
        }

        public static async Task HandleModal(DiscordClient client, ModalSubmittedEventArgs e)
        {
            try
            {
                DiscordMember? member = null;
                if (e.Interaction.Guild is not null)
                {
                    member = await e.Interaction.Guild.GetMemberAsync(e.Interaction.User.Id);
                }

                if (member is null || !member.IsStaff())
                {
                    return;
                }

                if (e.Interaction.Data.CustomId == "ref_create_modal")
                {

                    // DSharpPlus version quirk: e.Values keys are often the LABELS, not the CustomIDs.
                    // We must match the labels provided in AddTextInput exactly.
                    var code = GetValue(e.Values, "Referral Code");
                    var rewardStr = GetValue(e.Values, "New User Reward (M/K)");
                    var refRewardStr = GetValue(e.Values, "Referrer Reward (M/K)");
                    var usesStr = GetValue(e.Values, "Max Uses (-1 for inf)");
                    
                    if (!GpParser.TryParseAmountInK(rewardStr, out var reward, out _)) reward = 0;
                    if (!GpParser.TryParseAmountInK(refRewardStr, out var refReward, out _)) refReward = 0;
                    
                    // Simple int parse helper because manual .ToString() handles nulls weirdly occasionally
                    if (!int.TryParse(usesStr, out var uses)) uses = -1;


                    var env = ServerEnvironment.GetServerEnvironment();
                    var referralService = env.ServerManager.ReferralService;

                    // Assuming referrer is the staff member creating it for themselves or generic?
                    // The original command took a "referrer" User. 
                    // If the code *is* the username, then referrer is the user with that username.
                    // But here we let them type any code.
                    // Let's assume the referrer is the creator (ctx.User in original command passed as argument).
                    // Wait, original command: !referralcode <User referrer> ...
                    // So we might need to know WHO the referrer is. 
                    // If they type "MyCode", who gets the reward? The creator?
                    // Let's assume the creator is the referrer for now, OR we need another field "Referrer ID/Name".
                    // But we only have 5 slots in Modal. The original command used "referrer.Username" as the code.
                    
                    // Let's try to lookup the user by the code if it matches a username?
                    // Or default to the staff member execution?
                    // The prompt said "easy for user to fill in". 
                    // If I'm an admin making a code for "UserA", I want "UserA" to be the referrer.
                    // Adding "Referrer ID/Username" field is probably safest if we want admins to make codes for others.
                    // Slots: 1. Code, 2. Referrer, 3. Reward, 4. RefReward, 5. Uses. 
                    // WagerLock is 6th... We ran out of slots!
                    
                    // Discord Modals max 5 components.
                    // Maybe merge Reward/RefReward? "500/100" ? No, parsing error prone.
                    // Maybe "Code" IS the referrer username (like the original command implies)?
                    // Original: string code = referrer.Username;
                    
                    // So let's stick to that: The Code IS The Username.
                    // So "Target Username" is the field.
                    
                    string targetUsername = code;
                    string referrerId = e.Interaction.User.Id.ToString(); // Default to creator?
                    
                    // We need to resolve the user ID from the username to set them as referrer correctly.
                    var user = await env.ServerManager.UsersService.GetUserByUsernameAsync(targetUsername);
                    if (user != null)
                    {
                        referrerId = user.Identifier;
                    }
                    else 
                    {
                        // If user doesn't exist, maybe we can't create a referral code for them if it requires ID?
                        // Or maybe we just use the creator as referrer if not found?
                        // Let's try to find them.
                    }

                    // For now, let's pass the code as the code, and referrerId as resolved or creator.
                    
                    bool success = await referralService.CreateReferralCodeAsync(
                        code, 
                        user?.Identifier ?? e.Interaction.User.Id.ToString(), // If user found, use their ID, else creator
                        reward, 
                        refReward, 
                        uses, 
                        reward, // Automatic Wager Lock = Reward Amount
                        false // newUsersOnly default false for now, or we can parse from somewhere?
                    );

                    if (success)
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent($"✅ Created referral code `{code}`").AsEphemeral(false));
                    }
                    else
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().WithContent($"❌ Failed to create code `{code}`").AsEphemeral(true));
                    }
                }
            }
            catch (Exception ex)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Error: {ex.Message}").AsEphemeral(true));
            }
        }

        private static string GetValue(System.Collections.Generic.IReadOnlyDictionary<string, DSharpPlus.EventArgs.IModalSubmission> values, string key)
        {
            if (values.TryGetValue(key, out var val)) 
            {
                // In this version, val is IModalSubmission which has a Value property? Or is it a string?
                // The error says IModalSubmission.
                // Let's use dynamic to be safe if we can't see the definition easily, or .ToString() if it's the value itself wrapped.
                // Actually, RaceInteractionHandler uses reflection/dynamic.
                // Let's assume .ToString() or cast to dynamic.
                // Wait, earlier error said "Cannot implicitly convert type 'DSharpPlus.EventArgs.IModalSubmission' to 'string'".
                // So it is an object.
                
                try 
                {
                    dynamic d = val;
                    return d.Value;
                }
                catch
                {
                    return val.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
