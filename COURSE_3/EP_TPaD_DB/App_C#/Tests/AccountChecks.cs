using LP_PopovD_App;

int tests = 0;
void Check(bool good, string description)
{
    if (!good) throw new Exception(description);
    tests++;
}

string folder = Path.Combine(Path.GetTempPath(), "hockey-accounts-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    string path = Path.Combine(folder, "users.db");
    HockeyUserDatabase db = new(path);
    string? delivered = null;
    EmailCodePurpose lastPurpose = default;
    string? deliveredTo = null;
    Task Deliver(string email, string code, EmailCodePurpose purpose)
    {
        deliveredTo = email;
        delivered = code;
        lastPurpose = purpose;
        return Task.CompletedTask;
    }

    HockeyAccountService registration = new(db, Deliver, demoCodeMode: false);
    Check(!registration.RegistrationCodeRequested, "No code requested initially");
    await registration.RequestRegistrationAsync("HockeyPlayer", "strong-password-0123", "player@example.com");
    Check(registration.RegistrationCodeRequested, "Registration code requested");
    Check(deliveredTo == "player@example.com" && delivered?.Length == 6 &&
        delivered.All(char.IsAsciiDigit) && lastPurpose == EmailCodePurpose.Registration,
        "Six-digit registration code delivered");
    try
    {
        await registration.CompleteRegistrationAsync("000000" == delivered ? "000001" : "000000");
        throw new Exception("Registration accepted wrong OTP");
    }
    catch (InvalidOperationException) { tests++; }
    Check(await db.VerifyLoginAsync("HockeyPlayer", "strong-password-0123") == null,
        "No database record before email confirmation");
    await registration.CompleteRegistrationAsync(delivered!);
    Check(!registration.RegistrationCodeRequested, "OTP consumed");
    Check(await db.VerifyLoginAsync("HockeyPlayer", "strong-password-0123") == "player@example.com",
        "Verified registration stored in SQLite");
    Check(await db.VerifyLoginAsync("HockeyPlayer", "wrong-password") == null,
        "Wrong password rejected");
    Check(await db.VerifyLoginAsync("hockeyplayer", "strong-password-0123") == "player@example.com",
        "Login comparison case-insensitive");
    byte[] bytes = File.ReadAllBytes(path);
    Check(!System.Text.Encoding.UTF8.GetString(bytes).Contains("strong-password-0123"),
        "Plaintext password absent from SQLite file");

    HockeyAccountService second = new(db, Deliver, demoCodeMode: false);
    try
    {
        await second.RequestRegistrationAsync("HOCKEYPLAYER", "some-other-password", "different@example.com");
        throw new Exception("Duplicate login accepted");
    }
    catch (InvalidOperationException) { tests++; }

    try
    {
        await second.RequestLoginAsync("HockeyPlayer", "bad-pass");
        throw new Exception("Wrong login credentials accepted");
    }
    catch (InvalidOperationException) { tests++; }

    HockeyAccountService login = new(db, Deliver, demoCodeMode: false);
    await login.RequestLoginAsync("HockeyPlayer", "strong-password-0123");
    Check(login.LoginCodeRequested && deliveredTo == "player@example.com" &&
        lastPurpose == EmailCodePurpose.Login, "Login OTP only to stored email");
    try
    {
        login.CompleteLogin("000000" == delivered ? "000001" : "000000");
        throw new Exception("Invalid login OTP accepted");
    }
    catch (InvalidOperationException) { tests++; }
    login.CompleteLogin(delivered!);
    Check(!login.LoginCodeRequested, "Login OTP consumed and successful");
    // Демонстрационный режим по умолчанию: никакой SMTP-отправки,
    // любое НЕПУСТОЕ значение кода, запись в настоящую SQLite.
    int unexpectedDeliveries = 0;
    Task NoMail(string destination, string oneTimeCode, EmailCodePurpose purpose)
    {
        unexpectedDeliveries++;
        return Task.CompletedTask;
    }
    HockeyAccountService demoRegistration = new(db, NoMail);
    await demoRegistration.RequestRegistrationAsync(
        "DemoHockey", "demo-strong-password", "demo@example.com");
    Check(demoRegistration.RegistrationCodeRequested,
        "Demo registration opens code step without email");
    Check(unexpectedDeliveries == 0, "Demo never sends email");
    try
    {
        await demoRegistration.CompleteRegistrationAsync("   ");
        throw new Exception("Blank demo code accepted");
    }
    catch (InvalidOperationException) { tests++; }
    Check(await db.VerifyLoginAsync("DemoHockey", "demo-strong-password") == null,
        "Account is not created until any nonempty demo code");
    await demoRegistration.CompleteRegistrationAsync("любые буквы и символы!");
    Check(await db.VerifyLoginAsync("DemoHockey", "demo-strong-password") == "demo@example.com",
        "Demo registration persists account after arbitrary code");
    Check(!demoRegistration.RegistrationCodeRequested, "Demo registration step consumed");

    HockeyAccountService demoLogin = new(db, NoMail);
    try
    {
        await demoLogin.RequestLoginAsync("DemoHockey", "incorrect");
        throw new Exception("Demo bypassed database password check");
    }
    catch (InvalidOperationException) { tests++; }
    await demoLogin.RequestLoginAsync("DemoHockey", "demo-strong-password");
    Check(demoLogin.LoginCodeRequested && unexpectedDeliveries == 0,
        "Demo login checks saved credentials and does not email");
    demoLogin.CompleteLogin("непустой код 42!");
    Check(!demoLogin.LoginCodeRequested && unexpectedDeliveries == 0,
        "Any nonblank demo code finishes login without SMTP");

    Console.WriteLine($"Passed: {tests} SQLite and OTP checks.");
}
finally
{
    Directory.Delete(folder, recursive: true);
}
