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
            if (e.Id.StartsWith("stake_usercancel_", StringComparison.OrdinalIgnoreCase))
            {
                await HandleUserCancelAsync(client, e);
                return;
            }

            var parts = e.Id.Split('_');
            if (parts.Length != 3)
                return;

            var action = parts[1];
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

            usersService.TryGetUser(stake.Identifier, out var user);
            if (user != null)
            {
                if (newStatus == StakeStatus.Won)
                {
                    usersService.AddBalance(stake.Identifier, stake.AmountK);
                }
                else if (newStatus == StakeStatus.Lost)
                {
                    usersService.RemoveBalance(stake.Identifier, stake.AmountK);
                }

                usersService.TryGetUser(stake.Identifier, out user);
            }

            var balanceText = user != null ? GpFormatter.Format(user.Balance) : null;
            var amountText = GpFormatter.Format(stake.AmountK);
            var staffDisplay = user?.DisplayName ?? user?.Username ?? stake.Identifier;

            var resultTitle = newStatus switch
            {
                StakeStatus.Won => "🏆 Stake Won",
                StakeStatus.Lost => "⚠️ Stake Lost",
                StakeStatus.Cancelled => "🔁 Stake Cancelled",
                _ => "Stake"
            };

            var resultDescription = newStatus switch
            {
                StakeStatus.Won => $"Congratulations 🎉 You won the {amountText} stake.",
                StakeStatus.Lost => $"You lost the {amountText} stake.",
                StakeStatus.Cancelled => $"The {amountText} stake was cancelled.",
                _ => string.Empty
            };

            // Staff-facing embed: describe what happened to the user using their identifier
            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle(resultTitle)
                .WithDescription(newStatus switch
                {
                    StakeStatus.Won => $"User: {staffDisplay} won the {amountText} stake.",
                    StakeStatus.Lost => $"User: {staffDisplay} lost the {amountText} stake.",
                    StakeStatus.Cancelled => $"User: {staffDisplay} – the {amountText} stake was cancelled.",
                    _ => resultDescription
                })
                .WithColor(newStatus == StakeStatus.Won ? DiscordColor.Green : newStatus == StakeStatus.Lost ? DiscordColor.Red : DiscordColor.Orange)
                .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrEmpty(balanceText))
            {
                staffEmbed.AddField("Balance", balanceText, true);
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

            // User-facing embed: show win/lose text and balance
            if (stake.UserChannelId.HasValue && stake.UserMessageId.HasValue)
            {
                try
                {
                    var userChannel = await client.GetChannelAsync(stake.UserChannelId.Value);
                    var userMessage = await userChannel.GetMessageAsync(stake.UserMessageId.Value);

                    var userEmbed = new DiscordEmbedBuilder()
                        .WithTitle(resultTitle)
                        .WithDescription(resultDescription)
                        .WithColor(staffEmbed.Color.Value)
                        .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    if (!string.IsNullOrEmpty(balanceText))
                    {
                        userEmbed.AddField("Balance", balanceText, true);
                    }

                    await userMessage.ModifyAsync(b =>
                    {
                        b.Content = $"<@{stake.Identifier}>";
                        b.Embed = userEmbed.Build();
                        b.ClearComponents();
                    });
                }
                catch
                {
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

            if (stake.UserChannelId.HasValue && stake.UserMessageId.HasValue)
            {
                try
                {
                    var userChannel = await client.GetChannelAsync(stake.UserChannelId.Value);
                    var userMessage = await userChannel.GetMessageAsync(stake.UserMessageId.Value);

                    var userEmbed = new DiscordEmbedBuilder(userMessage.Embeds.Count > 0 ? userMessage.Embeds[0] : new DiscordEmbedBuilder())
                        .WithTitle("🔁 Stake Cancelled")
                        .WithDescription("Your stake request was cancelled.")
                        .WithColor(DiscordColor.Orange)
                        .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await userMessage.ModifyAsync(b =>
                    {
                        b.Embed = userEmbed.Build();
                        b.ClearComponents();
                    });
                }
                catch
                {
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
                        .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await staffMessage.ModifyAsync(b =>
                    {
                        b.Embed = staffEmbed.Build();
                        b.ClearComponents();
                    });
                }
                catch
                {
                }
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Your stake request has been cancelled.").AsEphemeral(true));
        }
    }
}
