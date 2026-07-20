using System.Diagnostics;
using Solace.Common.Asp.Auth;
using Solace.DB.Models;

namespace Solace.AuthServer.Features.Live.Login;

public static class LoginUtils
{
    public static LoginResponse CreateLoginResponse(Account account, CryptoSecrets cryptoSecrets, AuthSettings authSettings)
    {
        Debug.Assert(account.Username is not null);

        var tokenValidity = ValidityDatePair.Create(authSettings.UserTokenValidityMinutes);
        var token = new UserToken(
            account.Id,
            account.Username,
            Convert.ToBase64String(account.PasswordSalt),
            Convert.ToBase64String(account.PasswordHash)
        );
        var tokenString = JwtUtils.Sign(token, cryptoSecrets.LoginUserTokenSecret, tokenValidity);

        return new LoginResponse(
            account.Id,
            account.Username,
            account.FirstName ?? account.Username,
            account.LastName ?? account.Username,
            tokenString,
            tokenValidity.IssuedStr,
            tokenValidity.ExpiresStr,
            cryptoSecrets.LoginUserTokenSessionKeyBase64 // todo: random?
        );
    }
}