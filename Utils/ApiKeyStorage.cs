using SmallBazar.Services;
using Microsoft.Maui.Controls;

namespace SmallBazar.Utils;


public static class ApiKeyStorage
{
    private const string ApiKeyStorageKey = "user_api_key";

    private static IDialogService dialog;
    private static IAppLogger logger;  

    private static void InitializeServices()
    {
        dialog = dialog ?? ServiceHelper.GetService<IDialogService>();
        logger = logger ?? ServiceHelper.GetService<IAppLogger>();
    }   

    /// <summary>
    /// Enregistre la clé API dans le stockage sécurisé.
    /// </summary>
    /// <param name="apiKey">La clé API fournie par l'utilisateur.</param>
    /// <returns>Retourne vrai si la clé a été enregistrée avec succès, sinon faux.</returns>
    public static async Task<bool> SaveApiKeyAsync(string apiKey)
    {
        InitializeServices();
        try
        {
            await SecureStorage.Default.SetAsync(ApiKeyStorageKey, apiKey);
            return true;
        }
        catch (Exception ex)
        {
            await dialog.ShowMessage("Key has not been recorded!", ex.Message, "Ok");
            return false;
        }
    }

    /// <summary>
    /// Récupère la clé API stockée.
    /// </summary>
    /// <returns>Retourne la clé API si elle est trouvée, sinon null.</returns>
    public static async Task<string> GetApiKeyAsync()
    {
        InitializeServices();
        try
        {
            return await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
        }
        catch (Exception ex)
        {
            await dialog.ShowMessage("Can't read the key!", ex.Message, "Ok");
            return null;
        }
    }

    /// <summary>
    /// Supprime la clé API du stockage sécurisé.
    /// </summary>
    /// <returns>Retourne vrai si la clé a été supprimée avec succès, sinon faux.</returns>
    public static async Task<bool> DeleteApiKey()
    {
        InitializeServices();
        try
        {
            SecureStorage.Default.Remove(ApiKeyStorageKey);
            return true;
        }
        catch (Exception ex)
        {
            await dialog.ShowMessage("Can't delete the key!", ex.Message, "Ok");
            return false;
        }
    }
}

