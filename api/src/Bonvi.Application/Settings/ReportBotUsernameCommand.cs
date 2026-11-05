using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Settings;

/// <summary>
/// The bot reports the username <c>getMe</c> gave it. Written by the bot because the token can
/// change to another bot from the settings screen, and only the bot talks to Telegram.
/// </summary>
public sealed record ReportBotUsernameCommand(string? Username);

public sealed class ReportBotUsernameCommandHandler
{
    private readonly ISystemSettingRepository _settings;
    private readonly IUnitOfWork _unitOfWork;

    public ReportBotUsernameCommandHandler(ISystemSettingRepository settings, IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ReportBotUsernameCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!TelegramLinks.IsUsername(command.Username))
        {
            return Error.Validation("sozlama.bot_nomi_notogri", "Telegram nomi noto'g'ri.");
        }

        var existing = await _settings.FindAsync(SystemSettingKeys.BotUsername, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            var created = SystemSetting.CreateServerValue(SystemSettingKeys.BotUsername, command.Username);
            if (created.IsFailure)
            {
                return created.Error;
            }

            _settings.Add(created.Value);
        }
        else if (!string.Equals(existing.Value, command.Username, StringComparison.Ordinal))
        {
            var applied = existing.SetServerValue(command.Username);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }
        else
        {
            return Result.Success();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
