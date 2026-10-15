using SmallBazar.Services;

namespace SmallBazar.Utils;

public static class ApiKeyValidator
{
    private static IDialogService dialog;
    private static IAppLogger logger;

    private static void InitializeServices()
    {
        dialog = dialog ?? ServiceHelper.GetService<IDialogService>();
        logger = logger ?? ServiceHelper.GetService<IAppLogger>();
    }

    public static async Task<bool> IsKeyOk(string apiKey)
    {
        InitializeServices();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await dialog.ShowMessage("Missing API Key.", "Enter a valid key to access AI functions.", "Ok");
            return false;
        }

        // Étape 1 : Vérifier la connexion Internet
        if (!Connectivity.Current.NetworkAccess.Equals(NetworkAccess.Internet))
        {
            await dialog.ShowMessage("No Internet connection.", "Check your Internet connexion and try again.", "Ok");
            return false;
        }

        // Étape 2 : Vérifier la validité de la clé API
        return await ValidateApiKey(apiKey);
    }

    private static async Task<bool> ValidateApiKey(string apiKey)
    {
        using (HttpClient client = new HttpClient())
        {
            // Définir l'en-tête d'autorisation avec la clé API
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // Effectuer une requête vers l'API d'OpenAI (ici, on utilise /models pour un appel léger)
            try
            {
                var response = await client.GetAsync("https://api.openai.com/v1/models");

                if (response.IsSuccessStatusCode)
                {
                    // Clé valide car la requête est passée
                    return true;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Clé invalide
                    Console.WriteLine("Clé API non valide.");

                    return false;
                }
                else
                {
                    // Autre erreur
                    await dialog.ShowMessage("API Key Error", $"Error code: {response.StatusCode}", "Ok");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur de connexion à l'API : {ex.Message}");
                await dialog.ShowMessage("API Connection Error", ex.Message, "Ok");
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "API Connection Error", ex.Message);
                return false;
            }
        }
    }
}
