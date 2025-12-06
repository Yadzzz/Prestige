using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Stakes;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;

namespace Server.Communication.Discord.Interactions
{
    public static class StakeButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            // User cancel for stakes is temporarily disabled.
            // if (e.Id.StartsWith("stake_usercancel_", StringComparison.OrdinalIgnoreCase))
            // {
            //     await HandleUserCancelAsync(client, e);
            //     return;
            // }

            var parts = e.Id.Split('_');
            if (parts.Length != 3)
                return;

            var action = parts[1];
            if (!int.TryParse(parts[2], out var stakeId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var stakesService = env.ServerManager.StakesService;
            var usersService = env.ServerManager.UsersService;

            // Only staff should be able to resolve stakes via these buttons.
            // If the interaction user is not in the staff role, block it.
            var member = e.Guild != null ? await e.Guild.GetMemberAsync(e.User.Id) : null;
            if (member == null || !member.Roles.Any(r => r.Id == Server.Infrastructure.Discord.DiscordIds.StaffRoleId))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("You are not allowed to resolve stakes.")
                        .AsEphemeral(true));
                return;
            }

            var stake = stakesService.GetStakeById(stakeId);
            if (stake == null)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Stake not found.").AsEphemeral(true));
                return;
            }

            if (stake.Status != StakeStatus.Pending)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("This stake has already been processed.").AsEphemeral(true));
                return;
            }

            var newStatus = stake.Status;
            if (string.Equals(action, "win", StringComparison.OrdinalIgnoreCase))
            {
                newStatus = StakeStatus.Won;
            }
            else if (string.Equals(action, "lose", StringComparison.OrdinalIgnoreCase))
            {
                newStatus = StakeStatus.Lost;
            }
            else if (string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                newStatus = StakeStatus.Cancelled;
            }
            else
            {
                return;
            }

            stakesService.UpdateStakeStatus(stake.Id, newStatus);

            env.ServerManager.LogsService.Log(
                source: nameof(StakeButtonHandler),
                level: "Info",
                userIdentifier: stake.Identifier,
                action: "StakeResolved",
                message: $"Stake resolved id={stake.Id} status={newStatus} amountK={stake.AmountK}",
                exception: null,
                metadataJson: $"{{\"referenceId\":{stake.Id},\"kind\":\"Stake\",\"amountK\":{stake.AmountK},\"status\":\"{newStatus}\"}}");

            usersService.TryGetUser(stake.Identifier, out var user);

            long feeK = 0;
            long payoutK = 0;

            if (user != null)
            {
                if (newStatus == StakeStatus.Won)
                {
                    // stake amount was already removed when the stake was created.
                    // On win, user should get back stake + win amount.
                    feeK = (long)Math.Round(stake.AmountK * 0.10m, MidpointRounding.AwayFromZero);
                    payoutK = stake.AmountK - feeK;

                    if (payoutK < 0)
                    {
                        payoutK = 0;
                    }

                    // net change compared to before stake: +payoutK
                    usersService.AddBalance(stake.Identifier, stake.AmountK + payoutK);
                    // increment win streak, reset lose streak
                    UpdateStakeStreak(stake.Identifier, incrementWin: true);
                }
                else if (newStatus == StakeStatus.Lost)
                {
                    // user already paid stake up-front; nothing more to change
                    // increment lose streak, reset win streak
                    UpdateStakeStreak(stake.Identifier, incrementWin: false);
                }
                else if (newStatus == StakeStatus.Cancelled)
                {
                    // staff-cancelled: return the locked stake amount
                    usersService.AddBalance(stake.Identifier, stake.AmountK);
                }

                usersService.TryGetUser(stake.Identifier, out user);
            }

            var balanceText = user != null ? GpFormatter.Format(user.Balance) : null;
            var winStreakText = user != null ? user.StakeStreak.ToString() : null;
            var loseStreakText = user != null ? user.StakeLoseStreak.ToString() : null;
            var amountText = GpFormatter.Format(stake.AmountK);
            var feeText = feeK > 0 ? GpFormatter.Format(feeK) : null;
            var payoutText = payoutK > 0 ? GpFormatter.Format(payoutK) : null;
            var staffDisplay = user?.DisplayName ?? user?.Username ?? stake.Identifier;

            var resultTitle = newStatus switch
            {
                StakeStatus.Won => "🏆 Stake Won",
                StakeStatus.Lost => "⚔️ Stake Lost",
                StakeStatus.Cancelled => "❌ Stake Cancelled",
                _ => "Stake"
            };

            var resultDescription = newStatus switch
            {
                StakeStatus.Won =>
                    feeK > 0 && payoutK > 0
                        ? $"Congratulations 🎉\nYou won the **{amountText}** stake."
                        : $"Congratulations 🎉\nYou won the **{amountText}** stake.",
                StakeStatus.Lost => $"You lost the **{amountText}** stake.",
                StakeStatus.Cancelled => $"The **{amountText}** stake was cancelled.",
                _ => string.Empty
            };

            // Staff-facing embed: describe what happened to the user using their identifier
            var staffThumbnail = newStatus == StakeStatus.Won
                ? "https://i.imgur.com/qmkJM3O.gif"
                : newStatus == StakeStatus.Lost
                    ? "https://i.imgur.com/DtaZNgy.gif"
                    : "https://i.imgur.com/lTUFG2C.gif";

            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle(resultTitle)
                .WithDescription(newStatus switch
                {
                    StakeStatus.Won => $"User: {staffDisplay} won the **{amountText}** stake.",
                    StakeStatus.Lost => $"User: {staffDisplay} lost the **{amountText}** stake.",
                    StakeStatus.Cancelled => $"User: {staffDisplay} – the **{amountText}** stake was cancelled.",
                    _ => resultDescription
                })
                .WithColor(newStatus == StakeStatus.Won ? DiscordColor.Green : newStatus == StakeStatus.Lost ? DiscordColor.Red : DiscordColor.Orange)
                .WithThumbnail(staffThumbnail)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrEmpty(balanceText))
            {
                staffEmbed.AddField("Balance", balanceText, true);
            }

            if (newStatus == StakeStatus.Won && feeK > 0 && payoutK > 0)
            {
                staffEmbed
                    .AddField("Stake", amountText, true)
                    .AddField("Fee (10%)", feeText, true)
                    .AddField("Payout", payoutText, true);
            }

            var staffComponents = new DiscordComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Success, $"stake_win_{stake.Id}", "Win", disabled: true, emoji: new DiscordComponentEmoji("🏆")),
                new DiscordButtonComponent(ButtonStyle.Secondary, $"stake_cancel_{stake.Id}", "Cancel", disabled: true, emoji: new DiscordComponentEmoji("🔁")),
                new DiscordButtonComponent(ButtonStyle.Danger, $"stake_lose_{stake.Id}", "Lose", disabled: true, emoji: new DiscordComponentEmoji("❌"))
            };

            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(staffEmbed)
                    .AddComponents(staffComponents));

            // User-facing embed: send a new message showing result, streak and balance
            if (stake.UserChannelId.HasValue)
            {
                var envInner = env;
                try
                {
                    var userChannel = await client.GetChannelAsync(stake.UserChannelId.Value);

                    var userThumbnail = newStatus == StakeStatus.Won
                        ? "https://i.imgur.com/qmkJM3O.gif"
                        : newStatus == StakeStatus.Lost
                            ? "https://i.imgur.com/DtaZNgy.gif"
                            : "https://i.imgur.com/lTUFG2C.gif";

                    // total win = original stake + net profit after fee
                    var totalWinK = stake.AmountK + payoutK;
                    var totalWinText = GpFormatter.Format(totalWinK);

                    var userEmbed = new DiscordEmbedBuilder()
                        .WithTitle(resultTitle)
                        .WithDescription(newStatus == StakeStatus.Won && payoutK > 0
                            ? $"Congratulations 🎉\nYou won the **{totalWinText}** stake."
                            : resultDescription)
                        .WithColor(newStatus == StakeStatus.Won ? DiscordColor.SpringGreen :
                                   newStatus == StakeStatus.Lost ? DiscordColor.Red : DiscordColor.Orange)
                        .WithThumbnail(userThumbnail)
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    if (!string.IsNullOrEmpty(balanceText))
                    {
                        // Common top row: win/lose streak and balance
                        if (!string.IsNullOrEmpty(winStreakText))
                        {
                            userEmbed.AddField("Win Streak", winStreakText, true);
                        }
                        if (!string.IsNullOrEmpty(loseStreakText))
                        {
                            userEmbed.AddField("Lose Streak", loseStreakText, true);
                        }
                        userEmbed.AddField("Balance", balanceText, true);

                        // Extra field for lost outcome
                        if (newStatus == StakeStatus.Lost)
                        {
                            userEmbed.AddField("You lost", $"**{amountText}**", true);
                        }
                    }

                    await userChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .WithContent($"<@{stake.Identifier}>")
                        .AddEmbed(userEmbed));

                    // Also disable the original user cancel button on the pending stake message, if present
                    if (stake.UserMessageId.HasValue)
                    {
                        try
                        {
                            var originalMessage = await userChannel.GetMessageAsync(stake.UserMessageId.Value);

                            var disabledCancel = new DiscordButtonComponent(
                                ButtonStyle.Secondary,
                                $"stake_usercancel_{stake.Id}",
                                "Cancel",
                                disabled: true,
                                emoji: new DiscordComponentEmoji("❌"));

                            await originalMessage.ModifyAsync(builder =>
                            {
                                builder.Embed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds[0] : null;
                                builder.ClearComponents();
                                builder.AddComponents(disabledCancel);
                            });
                        }
                        catch (Exception ex)
                        {
                            ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to disable stake user cancel button: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to send stake result to user: {ex}");
                }
            }

        }

        private static async Task HandleUserCancelAsync(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            var parts = e.Id.Split('_');
            if (parts.Length != 3)
                return;

            if (!int.TryParse(parts[2], out var stakeId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var stakesService = env.ServerManager.StakesService;
            var usersService = env.ServerManager.UsersService;

            var stake = stakesService.GetStakeById(stakeId);
            if (stake == null)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Stake not found.").AsEphemeral(true));
                return;
            }

            if (stake.Status != StakeStatus.Pending || stake.Identifier != e.User.Id.ToString())
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You cannot cancel this stake.").AsEphemeral(true));
                return;
            }

            stakesService.UpdateStakeStatus(stake.Id, StakeStatus.Cancelled);
            // Give back their stake amount

            env.ServerManager.LogsService.Log(
                source: nameof(StakeButtonHandler),
                level: "Info",
                userIdentifier: stake.Identifier,
                action: "StakeUserCancelled",
                message: $"Stake user-cancelled id={stake.Id} amountK={stake.AmountK}",
                exception: null,
                metadataJson: $"{{\"referenceId\":{stake.Id},\"kind\":\"Stake\",\"amountK\":{stake.AmountK},\"cancelledBy\":\"User\"}}" );

            if (stake.UserChannelId.HasValue)
            {
                try
                {
                    var userChannel = await client.GetChannelAsync(stake.UserChannelId.Value);

                    var userEmbed = new DiscordEmbedBuilder()
                        .WithTitle("🔁 Stake Cancelled")
                        .WithDescription("Your stake request was cancelled.")
                        .WithColor(DiscordColor.Orange)
                        .WithThumbnail("https://i.imgur.com/lTUFG2C.gif")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await userChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .WithContent($"<@{stake.Identifier}>")
                        .AddEmbed(userEmbed));

                    // Disable cancel button on the original user message, if present
                    if (stake.UserMessageId.HasValue)
                    {
                        try
                        {
                            var originalMessage = await userChannel.GetMessageAsync(stake.UserMessageId.Value);

                            var disabledCancel = new DiscordButtonComponent(
                                ButtonStyle.Secondary,
                                $"stake_usercancel_{stake.Id}",
                                "Cancel",
                                disabled: true,
                                emoji: new DiscordComponentEmoji("🔁"));

                            await originalMessage.ModifyAsync(builder =>
                            {
                                builder.Embed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds[0] : null;
                                builder.ClearComponents();
                                builder.AddComponents(disabledCancel);
                            });
                        }
                        catch (Exception ex)
                        {
                            ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to disable stake user cancel button on cancel: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to send stake cancel message to user: {ex}");
                }
            }

            if (stake.StaffChannelId.HasValue && stake.StaffMessageId.HasValue)
            {
                try
                {
                    var staffChannel = await client.GetChannelAsync(stake.StaffChannelId.Value);
                    var staffMessage = await staffChannel.GetMessageAsync(stake.StaffMessageId.Value);

                    var staffEmbed = new DiscordEmbedBuilder(staffMessage.Embeds.Count > 0 ? staffMessage.Embeds[0] : new DiscordEmbedBuilder())
                        .WithTitle("🔁 Stake Cancelled")
                        .WithDescription($"The {GpFormatter.Format(stake.AmountK)} stake for {stake.Identifier} was cancelled by the user.")
                        .WithColor(DiscordColor.Orange)
                        .WithThumbnail("https://i.imgur.com/lTUFG2C.gif")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await staffMessage.ModifyAsync(b =>
                    {
                        b.Embed = staffEmbed.Build();
                        b.ClearComponents();
                    });
                }
                catch (Exception ex)
                {
                    ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to update staff stake message on cancel: {ex}");
                }
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Your stake request has been cancelled.").AsEphemeral(true));
        }

        private static void UpdateStakeStreak(string identifier, bool incrementWin)
        {
            try
            {
                using (var command = new Server.Infrastructure.Database.DatabaseCommand())
                {
                    if (incrementWin)
                    {
                        command.SetCommand("UPDATE users SET stake_streak = stake_streak + 1, stake_lose_streak = 0 WHERE identifier = @identifier");
                    }
                    else
                    {
                        command.SetCommand("UPDATE users SET stake_streak = 0, stake_lose_streak = stake_lose_streak + 1 WHERE identifier = @identifier");
                    }

                    command.AddParameter("identifier", identifier);
                    command.ExecuteQuery();
                }
            }
            catch
            {
                // Ignore streak update failures; core stake logic must still succeed.
            }
        }
    }
}
