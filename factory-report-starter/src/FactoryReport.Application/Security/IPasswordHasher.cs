namespace FactoryReport.Application.Security;

/// <summary>
/// 密码哈希与校验。禁止明文存储或自制弱加密。
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyHashedPassword(string hashedPassword, string providedPassword);
}
