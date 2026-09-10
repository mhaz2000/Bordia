namespace BuildingBlocks.Domain.Localization;

/// <summary>
/// Stable error code identifiers used by handlers, validators, and the
/// exception middleware. Every code must have a template for each language
/// in the embedded <c>errors.{language}.json</c> resources
/// (see <see cref="ErrorCatalog"/>).
/// </summary>
public static class ErrorCodes
{
    /// <summary>Cross-service codes.</summary>
    public static class Common
    {
        public const string NotFound = "common.notFound";
        public const string ValidationFailed = "common.validationFailed";
        public const string UnexpectedError = "common.unexpectedError";
        public const string UnauthorizedAccess = "common.unauthorizedAccess";
        public const string NotAuthenticated = "common.notAuthenticated";
    }

    /// <summary>RFC 7807 ProblemDetails title codes.</summary>
    public static class Titles
    {
        public const string Validation = "title.validationError";
        public const string Conflict = "title.conflict";
        public const string NotFound = "title.notFound";
        public const string Unauthorized = "title.unauthorized";
        public const string InternalError = "title.internalServerError";
    }

    /// <summary>Identity service codes.</summary>
    public static class Identity
    {
        public const string InvalidCredentials = "identity.invalidCredentials";
        public const string AccountDeactivated = "identity.accountDeactivated";
        public const string AccountNoLongerActive = "identity.accountNoLongerActive";
        public const string InvalidRefreshToken = "identity.invalidRefreshToken";
        public const string RefreshTokenExpired = "identity.refreshTokenExpired";
        public const string CurrentPasswordIncorrect = "identity.currentPasswordIncorrect";
        public const string EmailAlreadyExists = "identity.emailAlreadyExists";
    }

    /// <summary>Lobby service codes.</summary>
    public static class Lobby
    {
        public const string RoomFull = "lobby.roomFull";
        public const string RoomNotAccepting = "lobby.roomNotAccepting";
        public const string NotMember = "lobby.notMember";
        public const string PlayerNotMember = "lobby.playerNotMember";
        public const string CannotKickSelf = "lobby.cannotKickSelf";
        public const string OnlyHostKick = "lobby.onlyHostKick";
        public const string OnlyHostStart = "lobby.onlyHostStart";
        public const string OnlyHostClose = "lobby.onlyHostClose";
        public const string OnlyHostTransfer = "lobby.onlyHostTransfer";
        public const string AllMustBeReady = "lobby.allMustBeReady";
        public const string TwoPlayersRequired = "lobby.twoPlayersRequired";
        public const string RoomNotStartable = "lobby.roomNotStartable";
        public const string GameServiceFailed = "lobby.gameServiceFailed";
    }

    /// <summary>Game service codes.</summary>
    public static class Game
    {
        public const string NotAccepting = "game.notAccepting";
        public const string NoEngineState = "game.noEngineState";
        public const string NotAPlayer = "game.notAPlayer";
        public const string ConnectionNotReady = "game.connectionNotReady";
        public const string ActionRejected = "game.actionRejected";
    }

    /// <summary>FluentValidation message codes (validators emit codes instead of English text).</summary>
    public static class Validation
    {
        public const string IdentifierRequired = "validation.identifierRequired";
        public const string IdentifierTooLong = "validation.identifierTooLong";
        public const string PasswordRequired = "validation.passwordRequired";
        public const string PasswordTooShort = "validation.passwordTooShort";
        public const string PasswordTooLong = "validation.passwordTooLong";
        public const string EmailRequired = "validation.emailRequired";
        public const string EmailInvalid = "validation.emailInvalid";
        public const string EmailTooLong = "validation.emailTooLong";
        public const string DisplayNameRequired = "validation.displayNameRequired";
        public const string DisplayNameTooLong = "validation.displayNameTooLong";
        public const string RefreshTokenRequired = "validation.refreshTokenRequired";
        public const string CurrentPasswordRequired = "validation.currentPasswordRequired";
        public const string NewPasswordDiffers = "validation.newPasswordDiffers";
        public const string RoomIdRequired = "validation.roomIdRequired";
        public const string PlayerIdRequired = "validation.playerIdRequired";
        public const string ConnectionIdRequired = "validation.connectionIdRequired";
        public const string SessionIdRequired = "validation.sessionIdRequired";
        public const string ActionTypeRequired = "validation.actionTypeRequired";
        public const string ActionTypeTooLong = "validation.actionTypeTooLong";
        public const string PayloadRequired = "validation.payloadRequired";
        public const string RoomNameRequired = "validation.roomNameRequired";
        public const string RoomNameTooLong = "validation.roomNameTooLong";
        public const string GameTypeRequired = "validation.gameTypeRequired";
        public const string GameTypeTooLong = "validation.gameTypeTooLong";
        public const string MaxPlayersRange = "validation.maxPlayersRange";
        public const string SettingsTooLong = "validation.settingsTooLong";
        public const string PlayersRequired = "validation.playersRequired";
        public const string PlayersMinTwo = "validation.playersMinTwo";
        public const string PlayerUserIdRequired = "validation.playerUserIdRequired";
        public const string PlayerDisplayNameRequired = "validation.playerDisplayNameRequired";
        public const string PlayerDuplicate = "validation.playerDuplicate";
    }
}
