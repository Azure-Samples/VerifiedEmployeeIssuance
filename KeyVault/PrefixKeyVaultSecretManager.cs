using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;
using Microsoft.IdentityModel.Tokens;

namespace KeyVault
{
    public class PrefixKeyVaultSecretManager : KeyVaultSecretManager
    {
        private readonly string _prefix;


        public PrefixKeyVaultSecretManager(string? prefix)
        {
            prefix ??= string.Empty;
            _prefix = prefix.IsNullOrEmpty() || prefix.EndsWith("--") ? prefix : $"{prefix}--";
        }

        public override bool Load(SecretProperties secret)
        {
            return secret.Name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase);
        }

        public override string GetKey(KeyVaultSecret secret)
        {
            return secret.Name
                .Substring(_prefix.Length)
                .Replace("--", ConfigurationPath.KeyDelimiter);
        }
    }
}
