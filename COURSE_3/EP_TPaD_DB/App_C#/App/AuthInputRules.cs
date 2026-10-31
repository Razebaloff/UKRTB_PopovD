using System.Net.Mail;

namespace LP_PopovD_App
{
    internal static class AuthInputRules
    {
        internal static (int Field, string Message) Validate(bool registration, string login, string password, string email)
        {
            if (string.IsNullOrWhiteSpace(login)) return (0, "Введите логин.");
            if (string.IsNullOrWhiteSpace(password)) return (1, "Введите пароль.");
            if (registration && (!MailAddress.TryCreate(email.Trim(), out MailAddress? address) ||
                address.Address != email.Trim()))
                return (2, "Проверьте адрес электронной почты.");
            return (-1, "");
        }
    }
}
