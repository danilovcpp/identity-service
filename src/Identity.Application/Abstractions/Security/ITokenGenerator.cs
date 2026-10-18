namespace Identity.Application.Abstractions.Security;

public interface ITokenGenerator
{
    string Generate();
    string Hash(string token);
}
