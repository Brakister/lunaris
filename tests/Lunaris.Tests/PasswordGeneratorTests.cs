using Lunaris.Core.Utilities;

namespace Lunaris.Tests;

public class PasswordGeneratorTests
{
    private const string AllowedDefault = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*()-_=+[]{};:,.?";

    [Fact]
    public void Generates_requested_length()
    {
        Assert.Equal(12, PasswordGenerator.Generate(12).Length);
        Assert.Equal(64, PasswordGenerator.Generate(64).Length);
    }

    [Fact]
    public void Clamps_length()
    {
        Assert.Equal(4, PasswordGenerator.Generate(2).Length);
        Assert.Equal(4, PasswordGenerator.Generate(1).Length);
        Assert.Equal(256, PasswordGenerator.Generate(999).Length);
    }

    [Fact]
    public void Uses_only_allowed_charset_by_default()
    {
        var password = PasswordGenerator.Generate(200);
        Assert.All(password, c => Assert.Contains(c, AllowedDefault));
    }

    [Fact]
    public void Default_password_contains_each_class()
    {
        var password = PasswordGenerator.Generate(200);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
    }

    [Fact]
    public void Respects_excluded_classes()
    {
        var password = PasswordGenerator.Generate(200, includeUppercase: false, includeSymbols: false);
        Assert.All(password, c =>
        {
            Assert.True(char.IsLower(c) || char.IsDigit(c));
            Assert.False(char.IsUpper(c));
        });
    }

    [Fact]
    public void Two_generated_passwords_differ()
    {
        Assert.NotEqual(PasswordGenerator.Generate(32), PasswordGenerator.Generate(32));
    }
}