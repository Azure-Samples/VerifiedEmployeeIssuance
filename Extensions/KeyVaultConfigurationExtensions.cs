using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

namespace MyAccountPage
{
    public static class KeyVaultConfigurationExtensions
    {

        public static IConfigurationBuilder ConfigureAzureKeyVault(this WebApplicationBuilder builder)
        {
            var kvName = builder.Configuration["KeyVault:Name"];
            if (string.IsNullOrWhiteSpace(kvName))
                return builder.Configuration;

            var kvPrefix = builder.Configuration["KeyVault:SecretsPrefix"];
            var kvReloadIntervalInMinutes = builder.Configuration.GetValue<int>("KeyVault:ReloadIntervalInMinutes", default);

            TimeSpan? kvReloadInterval = kvReloadIntervalInMinutes == default ? null : new TimeSpan(hours: 0, minutes: kvReloadIntervalInMinutes, seconds: 0);

            var kvOptions = new AzureKeyVaultConfigurationOptions() { ReloadInterval = kvReloadInterval, Manager = new KeyVault.PrefixKeyVaultSecretManager(kvPrefix) };

            return builder.Configuration.AddAzureKeyVault(
                vaultUri: new Uri($"https://{kvName}.vault.azure.net/"),
                credential: new ChainedTokenCredential(new DefaultAzureCredential()
                ), kvOptions);
        }
    }
}
